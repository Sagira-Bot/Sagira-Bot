using System;
using Microsoft.Extensions.DependencyInjection;
using Discord.Commands;
using Discord.WebSocket;

namespace Sagira.Services
{

    public class ServiceHandler
    {
        
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _discClient;
        private readonly ItemHandler _handler;
        private readonly InteractionService _interactions;
   
        // Ask if there are existing CommandService and DiscordSocketClient
        // instance. If there are, we retrieve them and add them to the
        // DI container; if not, we create our own.
        public ServiceHandler(DiscordSocketClient client = null, CommandService commands = null, ItemHandler handler = null, InteractionService intr = null)
        {
            _commands = commands ?? new CommandService();
            _discClient = client ?? new DiscordSocketClient();
            _handler = handler ?? new ItemHandler();
            _interactions = intr ?? new InteractionService(_discClient, _handler);    
        }

        public IServiceProvider BuildServiceProvider() => new ServiceCollection()
            .AddSingleton(_discClient)
            .AddSingleton(_commands)
            .AddSingleton(_handler)
            .AddSingleton(_interactions)
            .BuildServiceProvider();
    }
}