using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;
using Discord;
using System.Threading.Tasks;

namespace Sagira_Bot.BotModules
{
    public class SagiraModule : ModuleBase<SocketCommandContext>
    {
		public Sagira sagira;

		public SagiraModule(Sagira sagiraInstance)
        {
			sagira = sagiraInstance;
        }

		[Command("rolls")]
		[Alias("y1", "curated", "perks", "y2", "gun")]
		[Summary("Takes gun name, and optional param year (1 or 2), and generates all possible perks")]
		public Task RollsAsync([Remainder] string GunName)
        {
			int Year = 2;
			bool isCurated = false;
			if (Context.Message.Content.ToLower().IndexOf("y1") == 1)
				Year = 1;
			else if (Context.Message.Content.ToLower().IndexOf("curated") == 1)
				isCurated = true;
				List<ItemData> ItemList = sagira.GenerateItemList(GunName, Year);
			if(ItemList == null || ItemList.Count == 0)
            {
				return ReplyAsync($"Couldn't find perks for: {GunName}");
			}
			List<ItemData>[] PerkColumns = sagira.GeneratePerkColumns(ItemList[0], isCurated);
			//Rich Embed starts here -- \u200b is 0 width space
			//DamageTypes 1 = Kinetic, 2 = Arc, 3 = Solar, 4 = Void, 6 = Stasis
			var dColor = Discord.Color.LightGrey;
			string ele = "Kinetic";
			switch (PerkColumns[0][0].DefaultDamageType)
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
			var Embed = new EmbedBuilder
			{
				ThumbnailUrl = $"https://www.bungie.net{PerkColumns[0][0].DisplayProperties.Icon}",
				Title = $"{PerkColumns[0][0].DisplayProperties.Name}",
				Description = $"{PerkColumns[0][0].Inventory.TierTypeName} {ele} {PerkColumns[0][0].ItemTypeDisplayName}",
				Color = dColor

			};

			
			for(int i = 1; i < PerkColumns.Length; i++)
            {
				if(PerkColumns[i] != null && PerkColumns[i].Count > 0)
                {
					string reply = "";
					foreach (ItemData perk in PerkColumns[i])
					{
						reply += $"{perk.DisplayProperties.Name}{System.Environment.NewLine}";

					}
					Embed.AddField(new EmbedFieldBuilder().WithName($"Column {i}").WithValue(reply).WithIsInline(true));
					if(i%2 == 0)
                    {
						Embed.AddField(new EmbedFieldBuilder().WithName("\u200b").WithValue("\u200b").WithIsInline(false));
					}
				}
			}
			return ReplyAsync("", false, Embed.Build());
		}
	}
}
