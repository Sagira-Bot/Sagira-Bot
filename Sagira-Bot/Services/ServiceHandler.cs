using System;
using Microsoft.Extensions.DependencyInjection;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;

namespace Sagira
{

    public class ServiceHandler
    {
        
        private readonly CommandService Commands;
        private readonly DiscordSocketClient DiscClient;
        private readonly InteractivityService Interactivity;
        private readonly ItemHandler Handler;
        // Ask if there are existing CommandService and DiscordSocketClient
        // instance. If there are, we retrieve them and add them to the
        // DI container; if not, we create our own.
        public ServiceHandler(ItemHandler handlr, DiscordSocketClient client = null, CommandService commands = null, InteractivityService intr = null)
        {
            Commands = commands ?? new CommandService();
            DiscClient = client ?? new DiscordSocketClient();
            Interactivity = intr ?? new InteractivityService(DiscClient);
            Handler = handlr;         
        }

        public IServiceProvider BuildServiceProvider() => new ServiceCollection()
            .AddSingleton(DiscClient)
            .AddSingleton(Commands)
            .AddSingleton(Interactivity)
            .AddSingleton(Handler)
            .BuildServiceProvider();
    }
}