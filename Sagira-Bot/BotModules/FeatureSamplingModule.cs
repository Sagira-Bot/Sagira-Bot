using System;
using System.Collections.Generic;
using System.Linq;
using Discord.Commands;
using Discord;
using Interactivity;
using Interactivity.Selection;
using System.Threading.Tasks;
using Sagira.Services;
using ItemData = BungieSharper.Entities.Destiny.Definitions.DestinyInventoryItemDefinition;
using System.Drawing;
using Discord.WebSocket;

namespace Sagira.BotModules
{
    class FeatureSamplingModule : ModuleBase<SocketCommandContext>
    {
        private async Task MyMessageComponentHandler(SocketInteraction arg)
        {
            // Parse the arg
            var parsedArg = (SocketMessageComponent)arg;
            // Get the custom ID 
            var customId = parsedArg.Data.CustomId;
            // Get the user
            var user = (SocketGuildUser)arg.User;
            // Get the guild
            var guild = user.Guild;

            // Respond with the update message response type. This edits the original message if you have set AlwaysAcknowledgeInteractions to false.
            // You can also use "ephemeral" so that only the original user of the interaction sees the message
            await parsedArg.RespondAsync($"Clicked {parsedArg.Data.CustomId}!", type: InteractionResponseType.UpdateMessage, ephemeral: true);

            // You can also followup with a second message
            await parsedArg.FollowupAsync($"Clicked {parsedArg.Data.CustomId}!", type: InteractionResponseType.ChannelMessageWithSource, ephemeral: true);

            //If you are using selection dropdowns, you can get the selected label and values using these:
            var selectedLabel = ((SelectMenu)parsedArg.Message.Components.First().Components.First()).Options.FirstOrDefault(x => x.Value == parsedArg.Data.Values.FirstOrDefault())?.Label;
            var selectedValue = parsedArg.Data.Values.First();
        }
    }
}
