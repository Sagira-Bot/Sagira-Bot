using System;
using Microsoft.Extensions.DependencyInjection;
using Discord.Commands;
using Discord.WebSocket;

namespace Sagira.Services
{

    public class ServiceHandler
    {
        
        private readonly CommandService Commands;
        private readonly DiscordSocketClient DiscClient;
        private readonly InteractionService Interactions;
        private readonly ItemHandler Handler;
        
        // Ask if there are existing CommandService and DiscordSocketClient
        // instance. If there are, we retrieve them and add them to the
        // DI container; if not, we create our own.
        public ServiceHandler(DiscordSocketClient client = null, CommandService commands = null, InteractionService intr = null, ItemHandler Handle = null)
        {
            Commands = commands ?? new CommandService();
            DiscClient = client ?? new DiscordSocketClient();
            Handler = Handle ?? new ItemHandler();
            Interactions = intr ?? new InteractionService(DiscClient, Handler);    
        }

        public IServiceProvider BuildServiceProvider() => new ServiceCollection()
            .AddSingleton(DiscClient)
            .AddSingleton(Commands)
            .AddSingleton(Handler)
            .AddSingleton(Interactions)
            .BuildServiceProvider();
    }
}