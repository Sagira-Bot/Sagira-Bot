using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using System.Threading.Tasks;
using Sagira.Services;
using Discord.WebSocket;
using System.Drawing;
using ItemData = BungieSharper.Entities.Destiny.Definitions.DestinyInventoryItemDefinition;

namespace Sagira.Modules
{
    public class ItemModule
    {
		
		private readonly ItemHandler _handler;
		private readonly InteractionService _interactions;

		public ItemModule(ItemHandler handlerInstance, InteractionService intr)
		{
			_handler = handlerInstance;
			_interactions = intr;
		}

		public async Task RollsAsync(SocketSlashCommand command, int year = 0, bool isCurated = false)
		{
			await command.AcknowledgeAsync();
			int gunSelection = 0;
			string gunName = (string)command.Data.Options.First().Value;

			List<ItemData> itemList = _handler.GenerateItemList(gunName.ToLower(), year);
			if (itemList == null || itemList.Count == 0)
			{
				await command.FollowupAsync(null, text: $"Couldn't find{(year != 0 ? $" Year {year}" : "")} Weapon: {gunName}");
				return;
			}

			//Handle Vague Searches -- Tell user to react to pick the gun they meant.
			if (itemList.Count > 1)
			{
				if (itemList.Count < 8)
				{
					Dictionary<Emoji, string> gunList = new Dictionary<Emoji, string>();
					Dictionary<string, int> gunIndexes = new Dictionary<string, int>();
					var EmbedSelection = new EmbedBuilder()
					{
						Title = $"Search Results for: \"{gunName}\"",
						Description = $"Please Select Desired Gun"
					};
					var options = new ComponentBuilder();
					for (int i = 0; i < itemList.Count; i++)
					{
						options.WithButton($"{itemList[i].DisplayProperties.Name}", $"{i}", ButtonStyle.Primary, row: (i/4));
						gunIndexes[itemList[i].DisplayProperties.Name] = i;
					}
					var msg = await command.Channel.SendMessageAsync(text:$"Search results for: \"{gunName}\" ", isTTS: false, component: options.Build());
					var Response = await _interactions.NextButtonAsync(InteractionFilter: (x => x.User.Id == command.User.Id), CompFilter: (x => x.Message.Id == msg.Id), timeout: TimeSpan.FromSeconds(60));
                    try
                    {
						gunSelection = Int32.Parse(Response.Data.CustomId);
						await msg.DeleteAsync();
					}
					catch(Exception e)
                    {
						await msg.DeleteAsync();
						await command.FollowupAsync(null, text: $"No search result selected in time.");
						return;
					}
				}
				else
				{
					await command.FollowupAsync(null, text: $"{command.User.Mention} 's search for {gunName} produced too many results. Please be more specific.");
					return;
				}
			}

			
			//Console.WriteLine($"Selected Gun Hash: {ItemList[gunSelection].Hash}");
			Dictionary<string, string>[] PerkDict = _handler.GeneratePerkDict(itemList[gunSelection]);
			//Rich Embed starts here -- \u200b is 0 width space
			//DamageTypes 1 = Kinetic, 2 = Arc, 3 = Solar, 4 = Void, 6 = Stasis			
			string ele = "Kinetic";
			switch ((int)itemList[gunSelection].DefaultDamageType)
			{
				case 1:
					ele = "Kinetic";
					break;
				case 2:
					ele = "Arc";
					break;
				case 3:
					ele = "Solar";
					break;
				case 4:
					ele = "Void";
					break;
				case 6:
					ele = "Stasis";
					break;
			}
			var dColor = ColorTranslator.FromHtml(Constants.ColorDict[ele]);
			//State 0 = Default, Regular y2 gun or random roll exotic. 1 = Non-Random Exotic. 2 = Year 1 gun. 3 = Curated of Any gun that isn't exotic. 
			int state = 0;
			if (itemList[gunSelection].Inventory.TierTypeName.ToLower() == "exotic" && !_handler.RandomExotics.ContainsKey(gunName.ToLower()))
				state = 1;
			else if (year == 1 || !_handler.IsRandomRollable(itemList[gunSelection]))
				state = 2;
			else if (isCurated)
				state = 3;

			//Console.WriteLine($"{ItemList[gunSelection].DisplayProperties.Name} Year: {ItemList[gunSelection].Year} State: {state} ");
			string disclaimer = $"Not all curated rolls actually drop in-game.{System.Environment.NewLine}* indicates perks that are only available on the curated roll.";
			if (state == 1 || state == 2)
				disclaimer = $"This version of {itemList[gunSelection].DisplayProperties.Name} does not have random rolls, all perks are selectable.";
			if (state == 3)
				disclaimer = $"Not all curated rolls actually drop in-game.";

			//Initialize EmbedBuilder with our context.
			var gunInfo = new EmbedBuilder
			{
				ThumbnailUrl = $"https://www.bungie.net{itemList[gunSelection].DisplayProperties.Icon}",
				Title = $"{itemList[gunSelection].DisplayProperties.Name}",
				Description = $"{itemList[gunSelection].Inventory.TierTypeName} {ele} {itemList[gunSelection].ItemTypeDisplayName} {System.Environment.NewLine} Intrinsic: {PerkDict[0].FirstOrDefault(intrin => intrin.Value.ToLower() == "intrinsic").Key}",
				Color = (Discord.Color)dColor,
				Footer = new EmbedFooterBuilder().WithText(disclaimer)
			};
			//Start from 1 to skip intrinsic. Per Column create an in-line embed field with perks.
			//Bold curated perks, and add a * after their name to indicate unrollable curated perk.
			for (int i = 1; i < PerkDict.Length; i++)
			{
				string reply = "";
				if (PerkDict[i] != null && PerkDict[i].Count > 0)
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
					if (reply != "")
					{
						gunInfo.AddField(new EmbedFieldBuilder().WithName($"Column {i}").WithValue(reply).WithIsInline(true));
						if (i % 2 == 0)
						{
							gunInfo.AddField(new EmbedFieldBuilder().WithName(Constants.BlankChar).WithValue(Constants.BlankChar).WithIsInline(false));
						}
					}
				}
			}
			var resourceLinks = new ComponentBuilder();
				resourceLinks.WithButton(new ButtonBuilder()
					.WithLabel("Light.gg")
					.WithStyle(ButtonStyle.Link)
					.WithUrl(@"https://www.light.gg/db/items/" + itemList[gunSelection].Hash));
				resourceLinks.WithButton(new ButtonBuilder()
					.WithLabel("D2 Gunsmith")
					.WithStyle(ButtonStyle.Link)
					.WithUrl(@"https://d2gunsmith.com/w/" + itemList[gunSelection].Hash));

			await command.FollowupAsync(new Embed[] { gunInfo.Build() }, component: resourceLinks.Build()); 
			return;
		}
	}	
}

