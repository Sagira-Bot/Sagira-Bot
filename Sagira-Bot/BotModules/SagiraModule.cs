using Discord.Commands;
using Sagira.Services;
using ItemData = BungieSharper.Entities.Destiny.Definitions.DestinyInventoryItemDefinition;

namespace Sagira.Modules
{
    public class SagiraModule : ModuleBase<SocketCommandContext>
    {
		public ItemHandler sagira;
		private InteractionService interactions;
		public SagiraModule(ItemHandler sagiraInstance, InteractionService interact)
        {
			sagira = sagiraInstance;
			interactions = interact;
        }
		
	}
}
