using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sagira.Modules;
using System.Threading;
using Discord.Net;

namespace Sagira.Services
{
    public class InteractionService
    {
        private readonly DiscordSocketClient _disClient;
        private readonly ItemHandler _handler;
        private readonly TimeSpan _defaultTimeout;
        private readonly ulong _debugServerID = 0;

        public InteractionService(DiscordSocketClient cli, ItemHandler items, ulong debugID = 0, TimeSpan? defaultTimeout = null)
        {
            _disClient = cli;
            _handler = items;
            _debugServerID = debugID;
            _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(120);           
        }

        public async Task OnClientReady()
        {           
            List<SlashCommandBuilder> commands = new List<SlashCommandBuilder>();
            commands.Add(new SlashCommandBuilder()
                .WithName("compare-stats")
                .WithDescription("Compares two weapon's stats")
                .AddOption("first-weapon", ApplicationCommandOptionType.String, "One of two weapons you want to compare the stats of", isRequired: true)
                .AddOption("second-weapon", ApplicationCommandOptionType.String, "One of two weapons you want to compare the stats of", isRequired: true));
            commands.Add(new SlashCommandBuilder()
                .WithName("stats")
                .WithDescription("Lists a weapon's stats")
                .AddOption("weapon-name", ApplicationCommandOptionType.String, "The weapon whose stat you want to search for", isRequired: true));
            commands.Add(new SlashCommandBuilder()
                .WithName("rolls")
                .WithDescription("Lists all of a weapon's possible rolls")
                .AddOption("weapon-name", ApplicationCommandOptionType.String, "The gun whose rolls you want to search for", isRequired: true));
            commands.Add(new SlashCommandBuilder()
                .WithName("year1")
                .WithDescription("Lists a weapon's static roll")
                .AddOption("weapon-name", ApplicationCommandOptionType.String, "The gun whose roll you want to search for", isRequired: true));
            commands.Add(new SlashCommandBuilder()
                .WithName("curated")
                .WithDescription("Lists a weapon's curated roll")
                .AddOption("weapon-name", ApplicationCommandOptionType.String, "The gun whose curated roll you want to search for", isRequired: true));
          /*  commands.Add(new SlashCommandBuilder()
                .WithName("botinfo")
                .WithDescription("Lists information about this bot"));
          */

            try
            {
                //await DeleteSlashCommands();
                foreach (var cmd in commands)
                {
                    if (_debugServerID != 0)
                    {
                        
                        Console.WriteLine("Loading Guild Commands");
                        await _disClient.Rest.CreateGuildCommand(cmd.Build(), _debugServerID);
                    }
                    else
                    {
                        Console.WriteLine("Loading Global Commands");
                        await _disClient.Rest.CreateGlobalCommand(cmd.Build());
                    }
                }
                    
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.HttpCode, Formatting.Indented);
                Console.WriteLine(json);
            }
        }

        public async Task Client_InteractionCreated(SocketInteraction arg)
        {
             _ = Task.Run(async () =>
            {
                if (arg is SocketSlashCommand command)
                {
                    await HandleSlashCommand(command);
                }
            });
        }

        private async Task HandleSlashCommand(SocketSlashCommand command)
        {
            
            switch (command.Data.Name)
            {
                case "rolls":
                    await (new ItemModule(_handler, this)).RollsAsync(command);
                    break;
                case "year1":
                    await (new ItemModule(_handler, this)).RollsAsync(command, 1);
                    break;
                case "curated":
                    await (new ItemModule(_handler, this)).RollsAsync(command, 0, true);
                    break;
                case "stats":
                    await (new ItemModule(_handler, this)).StatsAsync(command);
                    break;
                case "compare-stats":
                    await (new ItemModule(_handler, this)).CompareStatsAsync(command);
                    break;
                case "botinfo":
                    await command.RespondAsync("This bot currently has the slash commands: stats, rolls, year1, and curated. These commands will each pull a gun and all relevant perks or stats based on the command used.");
                    break;
            }
            return;
        }

        private async Task DeleteSlashCommands()
        {
            //await DisClient.Rest.DeleteAllGlobalCommandsAsync();
            if (_debugServerID != 0)
            {
                var cmds = await _disClient.Rest.GetGuildApplicationCommands(_debugServerID);
                foreach (var cmd in cmds)
                    await cmd.DeleteAsync();
            }

        }

        /// <summary>
        /// Retrieves the next incoming Message component interaction that passes the <paramref name="filter"/>.
        /// </summary>
        /// <param name="filter">The <see cref="Predicate{SocketMessageComponent}"/> which the component has to pass.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the request.</param>
        /// <returns>The <see cref="SocketMessageComponent"/> that matches the provided filter.</returns>
        public async Task<SocketMessageComponent> NextButtonAsync(Predicate<SocketMessageComponent> CompFilter = null, Predicate<SocketInteraction> InteractionFilter = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            CompFilter ??= m => true;
            InteractionFilter ??= m => true;

            var cancelSource = new TaskCompletionSource<bool>();
            var componentSource = new TaskCompletionSource<SocketMessageComponent>();
            var cancellationRegistration = cancellationToken.Register(() => cancelSource.SetResult(true));

            var componentTask = componentSource.Task;
            var cancelTask = cancelSource.Task;
            var timeoutTask = Task.Delay(timeout ?? _defaultTimeout);

            Task CheckComponent(SocketMessageComponent comp)
            {
                if (CompFilter.Invoke(comp))
                {
                    componentSource.SetResult(comp);
                }

                return Task.CompletedTask;
            }

            Task HandleInteraction(SocketInteraction arg)
            {
                if (InteractionFilter.Invoke(arg))
                {
                    if (arg is SocketMessageComponent comp)
                    {
                        return CheckComponent(comp);
                    }
                }
                return Task.CompletedTask;
            }

            try
            {
                _disClient.InteractionCreated += HandleInteraction;

                var result = await Task.WhenAny(componentTask, cancelTask).ConfigureAwait(false);

                return result == componentTask
                    ? await componentTask.ConfigureAwait(false)
                    : null;
            }
            finally
            {
                _disClient.InteractionCreated -= HandleInteraction;
                cancellationRegistration.Dispose();
            }
        }

        /// <summary>
        /// Retrieves the next incoming Message component interaction that passes the <paramref name="filter"/>.
        /// </summary>
        /// <param name="filter">The <see cref="Predicate{SocketMessageComponent}"/> which the component has to pass.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the request.</param>
        /// <returns>The <see cref="SocketMessageComponent"/> that matches the provided filter.</returns>
        public async Task<SocketMessageComponent> NextButtonEventAsync(Predicate<SocketInteraction> filter = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            filter ??= m => true;

            var cancelSource = new TaskCompletionSource<bool>();
            var componentSource = new TaskCompletionSource<SocketMessageComponent>();
            var cancellationRegistration = cancellationToken.Register(() => cancelSource.SetResult(true));

            var componentTask = componentSource.Task;
            var cancelTask = cancelSource.Task;
            var timeoutTask = Task.Delay(timeout ?? _defaultTimeout);


            Task CheckComponent(SocketInteraction intr)
            {
                if (filter.Invoke(intr))
                {
                    if(intr is SocketMessageComponent comp)
                        componentSource.SetResult(comp);
                }

                return Task.CompletedTask;
            }

            Task HandleInteraction(SocketInteraction arg)
            {
                if (arg is SocketMessageComponent comp)
                {
                    return CheckComponent(arg);
                }
                return Task.CompletedTask;
            }

            try
            {
                _disClient.InteractionCreated += HandleInteraction;

                var result = await Task.WhenAny(componentTask, cancelTask, timeoutTask).ConfigureAwait(false);

                return result == componentTask
                    ? await componentTask.ConfigureAwait(false)
                    : null;
            }
            finally
            {
                _disClient.InteractionCreated -= HandleInteraction;
                cancellationRegistration.Dispose();
            }
        }
    }
}
