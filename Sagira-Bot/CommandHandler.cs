using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.Threading.Tasks;
using dotenv.net;

namespace Sagira_Bot
{
    public class Initialize
    {
        private readonly CommandService Commands;
        private readonly DiscordSocketClient DiscClient;

        // Ask if there are existing CommandService and DiscordSocketClient
        // instance. If there are, we retrieve them and add them to the
        // DI container; if not, we create our own.
        public Initialize(DiscordSocketClient client = null, CommandService commands = null)
        {
            Commands = commands ?? new CommandService();
            DiscClient = client ?? new DiscordSocketClient();
        }

        public IServiceProvider BuildServiceProvider() => new ServiceCollection()
            .AddSingleton(DiscClient)
            .AddSingleton(Commands)
            .AddSingleton(new Sagira())
            .BuildServiceProvider();
    }


    class CommandHandler
    {
        private readonly DiscordSocketClient DiscClient;
        private readonly CommandService Commands;
        private readonly IServiceProvider DIServices;
        public readonly string Prefix;
        // Retrieve client and CommandService instance via ctor
        public CommandHandler(DiscordSocketClient client, CommandService commands, IServiceProvider services)
        {
            DiscClient = client;
            Commands = commands;
            DIServices = services;
            DotEnv.Load();
            Prefix = DotEnv.Read()["PREFIX"];
        }

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            DiscClient.MessageReceived += HandleCommandAsync;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. 
            await Commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                            services: DIServices);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasCharPrefix(Prefix.ToCharArray()[0], ref argPos) ||
                message.HasMentionPrefix(DiscClient.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return;

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(DiscClient, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await Commands.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: DIServices);
        }
    }
}
