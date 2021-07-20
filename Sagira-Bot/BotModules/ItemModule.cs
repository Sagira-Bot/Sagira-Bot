using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using System.Threading.Tasks;
using Sagira.Services;
using ItemData = BungieSharper.Entities.Destiny.Definitions.DestinyInventoryItemDefinition;
using Discord.WebSocket;
using System.Drawing;

namespace Sagira.Modules
{
    public class ItemModule
    {
		
		public ItemHandler Handler;
		public InteractionService interactions;
		private readonly Constants Consts;
		public ItemModule(ItemHandler HandlerInstance, InteractionService intr)
		{
			Handler = HandlerInstance;
			interactions = intr;
			Consts = new Constants();
		}

		public async Task RollsAsync(SocketSlashCommand command, int Year = 0, bool isCurated = false)
		{
			int gunSelection = 0;
			string GunName = (string)command.Data.Options.First().Value;

			List<ItemData> ItemList = Handler.GenerateItemList(GunName.ToLower(), Year);
			if (ItemList == null || ItemList.Count == 0)
			{
				await command.RespondAsync($"Couldn't find{(Year != 0 ? $" Year {Year}" : "")} Weapon: {GunName}");
				return;
			}

			//Handle Vague Searches -- Tell user to react to pick the gun they meant.
			if (ItemList.Count > 1)
			{
				if (ItemList.Count < 8)
				{
					Dictionary<Emoji, string> gunList = new Dictionary<Emoji, string>();
					Dictionary<string, int> gunIndexes = new Dictionary<string, int>();
					var EmbedSelection = new EmbedBuilder()
					{
						Title = $"Search Results for: \"{GunName}\"",
						Description = $"Please Select Desired Gun"
					};
					var Components = new ComponentBuilder();
					for (int i = 0; i < ItemList.Count; i++)
					{
						Components.WithButton($"{ItemList[i].DisplayProperties.Name}", $"{i}", ButtonStyle.Primary, row: (i/4));
						gunIndexes[ItemList[i].DisplayProperties.Name] = i;
					}
					var msg = await command.Channel.SendMessageAsync(text:$"Search results for: \"{GunName}\" ", isTTS: false, component: Components.Build());
					var Response = await interactions.NextButtonAsync(InteractionFilter: (x => x.User.Id == command.User.Id), CompFilter: (x => x.Message.Id == msg.Id));
                    try
                    {
						gunSelection = Int32.Parse(Response.Data.CustomId);
						await msg.DeleteAsync();
					}
					catch(Exception e)
                    {
						await command.RespondAsync($"No search selected in time.");
						return;
					}
				}
				else
				{
					await command.RespondAsync($"{command.User.Mention} 's search for {GunName} produced too many results. Please be more specific.");
					return;
				}
			}

			
			//Console.WriteLine($"Selected Gun Hash: {ItemList[gunSelection].Hash}");
			Dictionary<string, string>[] PerkDict = Handler.GeneratePerkDict(ItemList[gunSelection]);
			//Rich Embed starts here -- \u200b is 0 width space
			//DamageTypes 1 = Kinetic, 2 = Arc, 3 = Solar, 4 = Void, 6 = Stasis			
			string ele = "Kinetic";
			switch ((int)ItemList[gunSelection].DefaultDamageType)
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
			var dColor = ColorTranslator.FromHtml(Consts.ColorDict[ele]);
			//State 0 = Default, Regular y2 gun or random roll exotic. 1 = Non-Random Exotic. 2 = Year 1 gun. 3 = Curated of Any gun that isn't exotic. 
			int state = 0;
			if (ItemList[gunSelection].Inventory.TierTypeName.ToLower() == "exotic" && !Handler.RandomExotics.ContainsKey(GunName.ToLower()))
				state = 1;
			else if (Year == 1 || !Handler.IsRandomRollable(ItemList[gunSelection]))
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
						Embed.AddField(new EmbedFieldBuilder().WithName($"Column {i}").WithValue(reply).WithIsInline(true));
						if (i % 2 == 0)
						{
							Embed.AddField(new EmbedFieldBuilder().WithName("\u200b").WithValue("\u200b").WithIsInline(false));
						}
					}
				}
			}
			var ResourceLinks = new ComponentBuilder();
				ResourceLinks.WithButton(new ButtonBuilder().WithLabel("Light.gg").WithStyle(ButtonStyle.Link).WithUrl(@"https://www.light.gg/db/items/" + ItemList[gunSelection].Hash)); 
				ResourceLinks.WithButton(new ButtonBuilder().WithLabel("D2 Gunsmith").WithStyle(ButtonStyle.Link).WithUrl(@"https://d2gunsmith.com/w/" + ItemList[gunSelection].Hash)); 
			await command.Channel.SendMessageAsync("", false, embed: Embed.Build(), component: ResourceLinks.Build());
			return;
		}
	}	
}

