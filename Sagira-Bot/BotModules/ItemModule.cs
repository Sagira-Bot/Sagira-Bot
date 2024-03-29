﻿using System;
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

		private async Task<ItemData> SelectItem(SocketSlashCommand command, string gunName, int year = 0, bool isCurated = false)
        {
            int gunSelection = 0;

            List<ItemData> itemList = _handler.GenerateItemList(gunName.ToLower(), year);
            if (itemList == null || itemList.Count == 0)
            {
				await command.ModifyOriginalResponseAsync((msg) =>
				{
					msg.Content = $"Could not find: \"{gunName}\"";
				});
                return null;
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
                        options.WithButton($"{itemList[i].DisplayProperties.Name}", $"{i}", ButtonStyle.Primary, row: (i / 4));
                        gunIndexes[itemList[i].DisplayProperties.Name] = i;
                    }
					await command.ModifyOriginalResponseAsync((msg) =>
					{
						msg.Content = $"{command.User.Mention} - Search results for: \"{gunName}\"";
						msg.Components = options.Build();
					});
                    var Response = await _interactions.NextButtonAsync(InteractionFilter: (x => x.User.Id == command.User.Id), CompFilter: (x => x.Message.Id == command.GetOriginalResponseAsync().Result.Id), timeout: TimeSpan.FromSeconds(15));
					await command.ModifyOriginalResponseAsync((msg) =>
					{
						msg.Components = null;
						msg.Content = "Processing";
					});
					try
                    {
                        gunSelection = Int32.Parse(Response.Data.CustomId);
					}
                    catch (Exception e)
                    {
						await command.ModifyOriginalResponseAsync((msg) =>
						{
							msg.Content = $"No search result selected in time. Exception: {e}";
						});
                        return null;
                    }
				}
                else
                {
					await command.ModifyOriginalResponseAsync((msg) =>
					{
						msg.Content = $"{command.User.Mention} 's search for {gunName} produced too many results. Please be more specific.";
					});
                    return null;
                }
            }
			return itemList[gunSelection];
        }

		private async Task<ComponentBuilder> buildLinkButtons(uint Hash)
        {
			var resourceLinks = new ComponentBuilder();
			resourceLinks.WithButton(new ButtonBuilder()
				.WithLabel("Light.gg")
				.WithStyle(ButtonStyle.Link)
				.WithUrl(@"https://www.light.gg/db/items/" + Hash));
			resourceLinks.WithButton(new ButtonBuilder()
				.WithLabel("D2 Gunsmith")
				.WithStyle(ButtonStyle.Link)
				.WithUrl(@"https://d2gunsmith.com/w/" + Hash));
			return resourceLinks;
		}

		//DamageTypes 1 = Kinetic, 2 = Arc, 3 = Solar, 4 = Void, 6 = Stasis		
		private async Task<String> pickElement(int DefaultDamageType)
        {
			string ele = "Kinetic";
			switch (DefaultDamageType)
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
			return ele;
		}

		private async Task<EmbedBuilder> initEmbed(ItemData selectedItem, string disclaimer = "")
        {
			string ele = pickElement((int)selectedItem.DefaultDamageType).Result;
			var dColor = ColorTranslator.FromHtml(Constants.ColorDict[ele]);

			var gunInfo = new EmbedBuilder
			{
				ThumbnailUrl = $"https://www.bungie.net{selectedItem.DisplayProperties.Icon}",
				Title = $"{selectedItem.DisplayProperties.Name}",
				Description = $"{selectedItem.Inventory.TierTypeName} {ele} {selectedItem.ItemTypeDisplayName} {System.Environment.NewLine}Intrinsic: {_handler.GetIntrinsicOnly(selectedItem)}",
				Color = (Discord.Color)dColor,
				Footer = disclaimer == "" ? null : new EmbedFooterBuilder().WithText($"{disclaimer}")
			};

			return gunInfo;
		}
		public async Task RollsAsync(SocketSlashCommand command, int year = 0, bool isCurated = false)
		{
			await command.RespondAsync("Processing");

			string gunName = (string)command.Data.Options.First().Value;
			ItemData selectedItem = SelectItem(command, gunName, year, isCurated).Result;

			if(selectedItem == null)
            {
				return;
            }

			//Console.WriteLine($"Selected Gun Hash: {ItemList[gunSelection].Hash}");
			Dictionary<string, string>[] PerkDict = _handler.GeneratePerkDict(selectedItem);
			//Rich Embed starts here -- \u200b is 0 width space
	

			//State 0 = Default, Regular y2 gun or random roll exotic. 1 = Non-Random Exotic. 2 = Year 1 gun. 3 = Curated of Any gun that isn't exotic. 
			int state = 0;
			if (selectedItem.Inventory.TierTypeName.ToLower() == "exotic" && !_handler.RandomExotics.ContainsKey(gunName.ToLower()))
				state = 1;
			else if (year == 1 || !_handler.IsRandomRollable(selectedItem))
				state = 2;
			else if (isCurated)
				state = 3;

			//Console.WriteLine($"{ItemList[gunSelection].DisplayProperties.Name} Year: {ItemList[gunSelection].Year} State: {state} ");
			string disclaimer = $"Not all curated rolls actually drop in-game.{System.Environment.NewLine}* indicates perks that are only available on the curated roll.";

			switch (state)
            {
				case 1:
					disclaimer = "";
					break;
				case 2:
					disclaimer = $"This version of {selectedItem.DisplayProperties.Name} does not have random rolls.";
					break;
				case 3:
					disclaimer = $"Not all curated rolls actually drop in-game.";
					break;	
			}
			
			if (state != 1)
			{
				disclaimer += $"{System.Environment.NewLine}Bolded perks indicate curated roll.";
			}
			//Initialize EmbedBuilder with our context.
			EmbedBuilder gunInfo = initEmbed(selectedItem, disclaimer).Result;

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
						gunInfo.AddField(new EmbedFieldBuilder().WithName(i != 5 ? $"Column {i}" : "Origin Perk").WithValue(reply).WithIsInline(true));
						if (i % 2 == 0)
						{
							gunInfo.AddField(new EmbedFieldBuilder().WithName(Constants.BlankChar).WithValue(Constants.BlankChar).WithIsInline(false));
						}
					}
				}
			}

			ComponentBuilder resourceLinks = buildLinkButtons(selectedItem.Hash).Result;
			
			await command.ModifyOriginalResponseAsync((msg) =>
			{
				msg.Content = null;
				msg.Components = resourceLinks.Build();
				msg.Embed = gunInfo.Build();
			});
			return;
		}
		
		public async Task StatsAsync(SocketSlashCommand command)
        {
			await command.DeferAsync();

			string gunName = (string)command.Data.Options.First().Value;
			ItemData selectedItem = SelectItem(command, gunName).Result;
			Dictionary<string, int> statDict =_handler.GenerateStatDict(selectedItem);

			EmbedBuilder gunInfo = initEmbed(selectedItem, "Stats will vary based on selected perks and masterwork").Result;

			string weaponStats = "";
			foreach (var entry in statDict)
            {
				weaponStats += $"{entry.Key} = {entry.Value} {System.Environment.NewLine}";
			}
            if (statDict.ContainsKey("Recoil"))
            {
				weaponStats += $"Recoil Direction = {_handler.DetermineRecoilDirection(statDict["Recoil"])}";
			}

			gunInfo.AddField(new EmbedFieldBuilder().WithName("Weapon Stats").WithValue(weaponStats).WithIsInline(true));

			ComponentBuilder resourceLinks = buildLinkButtons(selectedItem.Hash).Result;
			await command.ModifyOriginalResponseAsync((msg) =>
			{
				msg.Content = null;
				msg.Components = resourceLinks.Build();
				msg.Embed = gunInfo.Build();
			});
			return;
		}

		public async Task CompareStatsAsync(SocketSlashCommand command)
		{
			await command.DeferAsync();

			string gunNameA = (string)command.Data.Options.ElementAt(0).Value;
			string gunNameB = (string)command.Data.Options.ElementAt(1).Value;
			Console.WriteLine($"Gun A: {gunNameA} |||| Gun B: {gunNameB}");

			ItemData selectedItemA = SelectItem(command, gunNameA).Result;
			ItemData selectedItemB = SelectItem(command, gunNameB).Result;

			gunNameA = selectedItemA.DisplayProperties.Name;
			gunNameB = selectedItemB.DisplayProperties.Name;

			if (selectedItemA.ItemTypeDisplayName != selectedItemB.ItemTypeDisplayName)
			{
				await command.ModifyOriginalResponseAsync((msg) =>
				{
					msg.Content = $"Cannot compare items of different types: **{gunNameA}** is a **{selectedItemA.ItemTypeDisplayName}** and **{gunNameB}** is a **{selectedItemB.ItemTypeDisplayName}**";
					msg.Components = null;
				});
				return;
			}

			Dictionary<string, int> statDictA = _handler.GenerateStatDict(selectedItemA);
			Dictionary<string, int> statDictB = _handler.GenerateStatDict(selectedItemB);
             
			EmbedBuilder gunInfo = new EmbedBuilder
			{
				ThumbnailUrl = $"https://www.bungie.net{selectedItemA.DisplayProperties.Icon}",
				Title = $"{selectedItemA.DisplayProperties.Name} vs {selectedItemB.DisplayProperties.Name}",
				Footer = new EmbedFooterBuilder().WithText($"Stats will vary based on selected perks and masterwork")
			};

			Console.WriteLine("A");
			foreach (var entry in statDictA)
			{
				string statName = entry.Key;
				int statValueA = entry.Value;
                if (!statDictB.ContainsKey(statName))
                {
					continue;
                }
				int statValueB = statDictB[statName];
				int statDifference = statValueA - statValueB;

				if(statDifference == 0) { continue; }

				string isolatedPrints = $"({gunNameA} = {statValueA} | {gunNameB} = {statValueB})";
				string comparison = "";
				if(statDifference > 0)
                {
					comparison = $"{gunNameA} has **{statDifference} more {statName}** than {gunNameB}";
                }
				else if(statDifference < 0)
                {
					comparison = $"{gunNameA} has **{Math.Abs(statDifference)} less {statName}** than {gunNameB}";
				}
				gunInfo.AddField(new EmbedFieldBuilder().WithName(statName).WithValue($"{comparison}{System.Environment.NewLine}{isolatedPrints}{System.Environment.NewLine}").WithIsInline(false));
			}

			if (statDictA.ContainsKey("Recoil"))
			{
				string recoilDirA = _handler.DetermineRecoilDirection(statDictA["Recoil"]);
				string recoilDirB = _handler.DetermineRecoilDirection(statDictB["Recoil"]);
				if (!recoilDirA.Equals(recoilDirB))
                {
					gunInfo.AddField(new EmbedFieldBuilder().WithName("Recoil Direction").WithValue($"(**{gunNameA}** {recoilDirA} | **{gunNameB}** {recoilDirB}){System.Environment.NewLine}").WithIsInline(false));
				}
			}
			await command.ModifyOriginalResponseAsync((msg) =>
			{
				msg.Content = null;
				msg.Components = null;
				msg.Embed = gunInfo.Build();
			});
			return;
		}
	}	
}

