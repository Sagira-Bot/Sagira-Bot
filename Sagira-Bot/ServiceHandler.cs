using System;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;
using Discord.Commands;
using Interactivity;


namespace Sagira_Bot
{
    public class ServiceHandler
    {
        private readonly CommandService Commands;
        private readonly DiscordSocketClient DiscClient;
        private readonly InteractivityService Interactivity;

        // Ask if there are existing CommandService and DiscordSocketClient
        // instance. If there are, we retrieve them and add them to the
        // DI container; if not, we create our own.
        public ServiceHandler(DiscordSocketClient client = null, CommandService commands = null, InteractivityService intr = null)
        {
            Commands = commands ?? new CommandService();
            DiscClient = client ?? new DiscordSocketClient();
            Interactivity = intr ?? new InteractivityService(DiscClient);          
        }

        public IServiceProvider BuildServiceProvider() => new ServiceCollection()
            .AddSingleton(DiscClient)
            .AddSingleton(Commands)
            .AddSingleton(Interactivity)
            .AddSingleton(new ItemHandler())
            .BuildServiceProvider();
    }
}
