using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sagira.Modules;
using System.Threading;

namespace Sagira.Services
{
    public class InteractionService
    {
        private readonly DiscordSocketClient DisClient;
        private readonly ItemHandler Handler;
        private readonly TimeSpan DefaultTimeout;
        private readonly ulong DebugServerID = 0;
        public InteractionService(DiscordSocketClient Cli, ItemHandler Items, ulong DebugID = 0)
        {
            DisClient = Cli;
            Handler = Items;
            DebugServerID = DebugID;
            DefaultTimeout = TimeSpan.FromSeconds(60);           
        }

        public async Task OnClientReady()
        {           
            List<SlashCommandBuilder> Commands = new();

            Commands.Add(new SlashCommandBuilder()
                .WithName("rolls")
                .WithDescription("Lists all of a weapon's possible rolls")
                .AddOption("weapon-name", ApplicationCommandOptionType.String, "The gun whose rolls you want to search for", required: true));
            Commands.Add(new SlashCommandBuilder()
                .WithName("year1")
                .WithDescription("Lists a weapon's static roll")
                .AddOption("weapon-name", ApplicationCommandOptionType.String, "The gun whose roll you want to search for", required: true));
            Commands.Add(new SlashCommandBuilder()
                .WithName("curated")
                .WithDescription("Lists a weapon's curated roll")
                .AddOption("weapon-name", ApplicationCommandOptionType.String, "The gun whose curated roll you want to search for", required: true));

            try
            {
                //await DeleteSlashCommands(DebugServerID);
                foreach (var cmd in Commands)
                {
                    if (DebugServerID != 0)
                    {
          
                        await DisClient.Rest.CreateGuildCommand(cmd.Build(), DebugServerID);
                    }
                    else
                    {
                        await DisClient.Rest.CreateGlobalCommand(cmd.Build());
                    }
                }
                    
            }
            catch (ApplicationCommandException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Error, Formatting.Indented);
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
                    await (new ItemModule(Handler, this)).RollsAsync(command);
                    break;
                case "year1":
                    await (new ItemModule(Handler, this)).RollsAsync(command, 1);
                    break;
                case "curated":
                    await (new ItemModule(Handler, this)).RollsAsync(command, 0, true);
                    break;
            }
        }

        private async Task DeleteSlashCommands(ulong guildId)
        {
            //await DisClient.Rest.DeleteAllGlobalCommandsAsync();
            var cmds = await DisClient.Rest.GetGuildApplicationCommands(guildId);
            foreach (var cmd in cmds)
                await cmd.DeleteAsync();
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
            var timeoutTask = Task.Delay(timeout ?? DefaultTimeout);

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
                DisClient.InteractionCreated += HandleInteraction;

                var result = await Task.WhenAny(componentTask, cancelTask).ConfigureAwait(false);

                return result == componentTask
                    ? await componentTask.ConfigureAwait(false)
                    : null;
            }
            finally
            {
                DisClient.InteractionCreated -= HandleInteraction;
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
            var timeoutTask = Task.Delay(timeout ?? DefaultTimeout);


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
                DisClient.InteractionCreated += HandleInteraction;

                var result = await Task.WhenAny(componentTask, cancelTask).ConfigureAwait(false);

                return result == componentTask
                    ? await componentTask.ConfigureAwait(false)
                    : null;
            }
            finally
            {
                DisClient.InteractionCreated -= HandleInteraction;
                cancellationRegistration.Dispose();
            }
        }
    }
}
