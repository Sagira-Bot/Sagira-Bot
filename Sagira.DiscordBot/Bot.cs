using BungieSharper.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Gateway;
using Remora.Discord.Gateway.Extensions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sagira.DiscordBot
{
    internal class Bot
    {
        internal async Task BotProgramAsync()
        {
            var versionString = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? throw new NullReferenceException();
            Console.Title = "Sagira.DiscordBot v" + versionString;

            var cancellationSource = new CancellationTokenSource();

            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationSource.Cancel();
            };

            var services = ConfigureServices();

            var config = services.GetRequiredService<SagiraConfiguration>();

            try
            {
                await config.Load();
            }
            catch (Exception ex) when (ex is FileNotFoundException)
            {
                await SagiraConfiguration.SaveConfig(new SagiraConfiguration());
                Console.WriteLine($"Please populate entries in \"{SagiraConfiguration.DefaultFileName}\"\nPress ENTER to exit.");
                Console.ReadLine();
                return;
            }

            var gatewayClient = services.GetRequiredService<DiscordGatewayClient>();

            var runResult = await gatewayClient.RunAsync(cancellationSource.Token);

            await services.DisposeAsync();
        }

        private static ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton<SagiraConfiguration>()
                .AddSingleton<BungieClientConfig>()
                .AddSingleton<BungieApiClient>()
                .AddLogging
                (
                    c => c
                        .AddConsole()
                        .AddFilter("System.Net.Http.HttpClient.*.LogicalHandler", LogLevel.Warning)
                        .AddFilter("System.Net.Http.HttpClient.*.ClientHandler", LogLevel.Warning)
                )
                .AddDiscordGateway(x => x.GetRequiredService<SagiraConfiguration>()._discordBotToken)
                .AddDiscordCommands()
                .BuildServiceProvider();
        }
    }
}