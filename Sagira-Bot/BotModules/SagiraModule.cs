using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Discord.Commands;
using Discord;
using Interactivity;
using Interactivity.Confirmation;
using Interactivity.Pagination;
using Interactivity.Selection;
using System.Threading.Tasks;

namespace Sagira_Bot.BotModules
{
    public class SagiraModule : ModuleBase<SocketCommandContext>
    {
		public Sagira sagira;
		public InteractivityService interactions;
		string[] NumberEmoji = new string[] { ":zero:", ":one:", ":two:", ":three:", ":four:", ":five:", ":six:", ":seven:", ":eight:", ":nine:" };
		string[] NumberUnicodes = new string[] { "0️⃣", "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣" };
		public SagiraModule(Sagira sagiraInstance, InteractivityService interact)
        {
			sagira = sagiraInstance;
			interactions = interact;
        }

		/// <summary>
		/// Take gun name -> Search for it
		/// If search vague -> Prompt user to select one of various, unless it's TOO vague, then tell user to be more specific
		/// Once user answers prompt via reaction -> Set state based on chosen gun and params. 
		/// Once state is set -> Start generating embed and build it up as we parse our perk dictionary
		/// </summary>
		/// <param name="GunName"></param>
		/// <returns></returns>
		[Command("rolls", RunMode = RunMode.Async)]
		[Alias("y1", "curated")]
		[Summary("Takes gun name, and optional param year (1 or 2), and generates all possible perks")]
		public async Task RollsAsync([Remainder] string GunName)
        {
			int Year = 0;
			int gunSelection = 0;
			bool isCurated = false;
			//Check based on alias which context we should work with.
			if (Context.Message.Content.ToLower().IndexOf("y1") == 1)
				Year = 1;
			else if (Context.Message.Content.ToLower().IndexOf("curated") == 1)
				isCurated = true;
			List<ItemData> ItemList = sagira.GenerateItemList(GunName.ToLower(), Year);
			if(ItemList == null || ItemList.Count == 0)
            {
				await ReplyAsync($"Couldn't find{(Year != 0 ? $" Year {Year}" : "")} Weapon: {GunName}");
				return;
			}

			//Handle Vague Searches -- Tell user to react to pick the gun they meant.
			if(ItemList.Count > 1)
            {
				if (ItemList.Count < 7) //This will be configurable per server eventually. For the sake of time, we're keeping it at 6 (6s) due to preemptive rate limits.
				{
					string Title = $"Search Results for: {GunName}";
					string Description = $"Please Select Desired Gun";
					Dictionary<Emoji, string> gunList = new Dictionary<Emoji, string>();
					Dictionary<string, int> gunIndexes = new Dictionary<string, int>();
					for (int i = 0; i < ItemList.Count; i++)
					{
						gunList[new Emoji(NumberUnicodes[i + 1])] = $"{ItemList[i].DisplayProperties.Name}";
						gunIndexes[ItemList[i].DisplayProperties.Name] = i;
					}
					var builder = new ReactionSelectionBuilder<string>()
									.WithSelectables(gunList).WithUsers(Context.User).WithDeletion(DeletionOptions.AfterCapturedContext | DeletionOptions.Invalids).WithTitle(Title);
					var result = await interactions.SendSelectionAsync(builder.Build(), Context.Channel, TimeSpan.FromSeconds(50));

					if (result.IsSuccess)
					{
						gunSelection = gunIndexes[result.Value.ToString()];
					}
				}
				else
				{
					await ReplyAsync($"{Context.User.Mention} 's search for {GunName} produced too many results. Please be more specific.");
					return;
				}
			}

			Dictionary<string, string>[] PerkDict = sagira.GeneratePerkDict(ItemList[gunSelection]);
			//Rich Embed starts here -- \u200b is 0 width space
			//DamageTypes 1 = Kinetic, 2 = Arc, 3 = Solar, 4 = Void, 6 = Stasis
			var dColor = Discord.Color.LightGrey;
			string ele = "Kinetic";
			switch (ItemList[gunSelection].DefaultDamageType)
            {
				case 1:
					dColor = Discord.Color.LightGrey;
					ele = "Kinetic";
					break;
				case 2:
					dColor = Discord.Color.Blue;
					ele = "Arc";
					break;
				case 3:
					dColor = Discord.Color.Orange;
					ele = "Solar";
					break;
				case 4:
					dColor = Discord.Color.Purple;
					ele = "Void";
					break;
				case 6:
					dColor = Discord.Color.DarkBlue;
					ele = "Stasis";
					break;
            }
			//State 0 = Default, Regular y2 gun or random roll exotic. 1 = Non-Random Exotic. 2 = Year 1 gun. 3 = Curated of Any gun that isn't exotic. 
			int state = 0;
			if (ItemList[gunSelection].Inventory.TierTypeName.ToLower() == "exotic" && !sagira.RandomExotics.ContainsKey(GunName.ToLower()))
				state = 1;
			else if (Year == 1 || ItemList[gunSelection].Year == 1 )
				state = 2;
			else if (isCurated)
				state = 3;

			//Console.WriteLine($"{ItemList[gunSelection].DisplayProperties.Name} Year: {ItemList[gunSelection].Year} State: {state} ");
			string disclaimer = $"Not all curated rolls actually drop in-game.{System.Environment.NewLine}* indicates perks that are only available on the curated roll.";
			if (state == 1 || state == 2)
				disclaimer = $"This version of {ItemList[gunSelection].DisplayProperties.Name} does not have random rolls, all perks are selectable.";
			if (state == 3)
				disclaimer = $"Not all curated rolls actually drop in-game.";

			//Initialize EmbedBuilder with our context.
			var Embed = new EmbedBuilder
			{
				ThumbnailUrl = $"https://www.bungie.net{ItemList[gunSelection].DisplayProperties.Icon}",
				Title = $"{ItemList[gunSelection].DisplayProperties.Name}",
				Description = $"{ItemList[gunSelection].Inventory.TierTypeName} {ele} {ItemList[gunSelection].ItemTypeDisplayName} {System.Environment.NewLine} Intrinsic: {PerkDict[0].FirstOrDefault(intrin => intrin.Value.ToLower() == "intrinsic").Key}",
				Color = dColor,
				Footer = new EmbedFooterBuilder().WithText(disclaimer)
			};
			//Start from 1 to skip intrinsic. Per Column create an in-line embed field with perks.
			//Bold curated perks, and add a * after their name to indicate unrollable curated perk.
			for(int i = 1; i < PerkDict.Length; i++)
            {
				string reply = "";
				if(PerkDict[i] != null && PerkDict[i].Count > 0)
                {
					foreach (KeyValuePair<string, string> perk in PerkDict[i])
					{
						if (state == 0)
						{
							if (perk.Value.Contains("curated"))
							{
								reply += $"**{perk.Key}**";
								if (perk.Value.Contains("0"))
									reply += " *";
							}
							else
							{
								reply += perk.Key;
							}
							reply += System.Environment.NewLine;
						}
						else if (((state == 2 || state == 3) && perk.Value.Contains("curated")) || state == 1)
						{
							reply += $"{perk.Key}{System.Environment.NewLine}";
						}
					}
					if(reply != "")
                    {
						Embed.AddField(new EmbedFieldBuilder().WithName($"Column {i}").WithValue(reply).WithIsInline(true));
						if (i % 2 == 0)
						{
							Embed.AddField(new EmbedFieldBuilder().WithName("\u200b").WithValue("\u200b").WithIsInline(false));
						}
					}
				}		
			}
			await ReplyAsync("", false, Embed.Build());
			return;
		}

		[Command("help")]
		[Summary("Spits out usage string")]
		public Task HelpAsync()
		{
			return ReplyAsync($"The general usage of this bot is to display potential rolls for desired weapons.{System.Environment.NewLine}Basic usage: {Context.Message.Content[0]}rolls WEAPON-NAME {System.Environment.NewLine}You may also use: [{Context.Message.Content[0]}Curated and {Context.Message.Content[0]}y1]");
		}
	}


}
