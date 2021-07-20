using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Results;
using Sagira.Services;
using ItemData = BungieSharper.Entities.Destiny.Definitions.DestinyInventoryItemDefinition;

namespace Sagira.Commands
{
	//[Group("itemcommands")]	
	public class ItemCommands : CommandGroup
    {
        private readonly InteractionContext _context;
        private readonly IDiscordRestWebhookAPI _webhookAPI;
        private readonly ItemHandler Handler;
		private readonly Dictionary<string, string> ColorDict = new Dictionary<string, string>
		{
			{ "Arc", "#7AECF3" },
			{ "Solar", "#F36F21" },
			{ "Void", "#B283CC" },
			{ "Kinetic", "#FFFFFF" },
			{ "Stasis", "#4D88FF" }
		};
		/// <summary>
		/// Initializes a new instance of the <see cref="ItemCommands"/> class.
		/// </summary>
		/// <param name="webhookAPI">The webhook API.</param>
		/// <param name="context">The command context.</param>
		
		public ItemCommands(IDiscordRestWebhookAPI webhookAPI, InteractionContext context, ItemHandler HandlerInstance)
        {
            _webhookAPI = webhookAPI;
            _context = context;
            Handler = HandlerInstance;
        }
		
        [Command("rolls")]
        [Description("Posts possible perks for the searched weapon. Prioritized Random Rolls.")]
		public async Task<IResult> PostY2RollsAsync([Description("The Weapon Name")] string GunName)
		{
			return await GenerateItemEmbedAsync(GunName, 0);

		}
		[Command("y1")]
		[Description("Posts Perks of the Y1 version of the searched weapon")]
		public async Task<IResult> PostY1RollsAsync([Description("The Weapon Name")] string GunName)
		{
			return await GenerateItemEmbedAsync(GunName, 1);
		}

		[Command("curated")]
		[Description("Posts Perks of the Curated version of the searched weapon")]
		public async Task<IResult> PostCuratedRollsAsync([Description("The Weapon Name")] string GunName)
		{
			return await GenerateItemEmbedAsync(GunName, 0, true);

		}
		[Command("speedtest")]
		[Description("Posts Perks of the Curated version of the searched weapon")]
		public async Task<IResult> PostestAsync([Description("The Weapon Name")] string GunName)
		{
			return await GenerateItemEmbedAsync(GunName, 0, true);

		}
		public async Task<IResult> GenerateItemEmbedAsync([Description("The Weapon Name")] string GunName, int Year = 0, bool isCurated = false)
		{
			int gunSelection = 0;
			List<ItemData> ItemList = Handler.GenerateItemList(GunName.ToLower(), Year);
			if (ItemList == null || ItemList.Count == 0)
			{
				var err = await _webhookAPI.CreateFollowupMessageAsync(
					_context.ApplicationID,
					_context.Token,
					$"Couldn't find{(Year != 0 ? $" Year {Year}" : "")} Weapon: {GunName}"
					);
				return Result.FromError(err);
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
			var dColor = ColorTranslator.FromHtml(ColorDict[ele]);
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

			//Start from 1 to skip intrinsic. Per Column create an in-line embed field with perks.
			//Bold curated perks, and add a * after their name to indicate unrollable curated perk.
			List<EmbedField> fields = new List<EmbedField>();
			for (int i = 1; i < PerkDict.Length; i++)
			{
				string PerkColumnStr = "";
				if (PerkDict[i] != null && PerkDict[i].Count > 0)
				{
					foreach (KeyValuePair<string, string> perk in PerkDict[i])
					{
						if (state == 0)
						{
							if (perk.Value.Contains("curated"))
							{
								PerkColumnStr += $"**{perk.Key}**";
								if (perk.Value.Contains("0"))
									PerkColumnStr += " *";
							}
							else
							{
								PerkColumnStr += perk.Key;
							}
							PerkColumnStr += System.Environment.NewLine;
						}
						else if (((state == 2 || state == 3) && perk.Value.Contains("curated")) || state == 1)
						{
							PerkColumnStr += $"{perk.Key}{System.Environment.NewLine}";
						}
					}
					if (PerkColumnStr != "")
					{
						fields.Add(new EmbedField(Name: $"Column {i}", Value: PerkColumnStr, IsInline: true));
						if (i % 2 == 0)
						{
							fields.Add(new EmbedField(Name: "\u200b", Value: "\u200b", IsInline: false));
						}
					}
				}
			}

			var Embed = new Embed()
			{
				Thumbnail = new EmbedThumbnail($"https://www.bungie.net{ItemList[gunSelection].DisplayProperties.Icon}"),
				Title = $"{ItemList[gunSelection].DisplayProperties.Name}",
				Description = $"{ItemList[gunSelection].Inventory.TierTypeName} {ele} {ItemList[gunSelection].ItemTypeDisplayName} {System.Environment.NewLine} Intrinsic: {PerkDict[0].FirstOrDefault(intrin => intrin.Value.ToLower() == "intrinsic").Key}",
				Colour = dColor,
				Footer = new EmbedFooter(disclaimer),
				Fields = fields
			};

			var reply = await _webhookAPI.CreateFollowupMessageAsync
			(
				_context.ApplicationID,
				_context.Token,
				embeds: new[] { Embed },
				ct: this.CancellationToken
			);

			return !reply.IsSuccess
				? Result.FromError(reply)
				: Result.FromSuccess();

		}
	}


}