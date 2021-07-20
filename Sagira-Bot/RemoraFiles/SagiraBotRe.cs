using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remora.Discord.Commands.Services;
using Remora.Discord.Gateway;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sagira.Services;
namespace Sagira.Remora
{
    internal class Bot
    {
        internal async Task BotProgramAsync()
        {
            var versionString = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? throw new NullReferenceException();
            Console.Title = "Sagira.DiscordBot v" + versionString;

            var config = new SagiraConfiguration();
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
            
            var Handler = new ItemHandler(config._bungieApiKey);
            var Services = (new ServiceHandlerRemora(config, Handler)).ConfigureServices();
            var cancellationSource = new CancellationTokenSource();
            var log = Services.GetRequiredService<ILogger<Program>>();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationSource.Cancel();
            };

            //Snowflake? debugServer = new Snowflake(214094904861786113);
            var slashService = Services.GetRequiredService<SlashService>();
            var checkSlashSupport = slashService.SupportsSlashCommands();
            if (!checkSlashSupport.IsSuccess)
            {
                log.LogWarning
                (
                    "The registered commands of the bot don't support slash commands: {Reason}",
                    checkSlashSupport.Error.Message
                );
            }
            else
            {
                var updateSlash = await slashService.UpdateSlashCommandsAsync(null, cancellationSource.Token);
                if (!updateSlash.IsSuccess)
                {
                    log.LogWarning("Failed to update slash commands: {Reason}", updateSlash.Error.Message);
                }
            }


            var gatewayClient = Services.GetRequiredService<DiscordGatewayClient>();
            var runResult = await gatewayClient.RunAsync(cancellationSource.Token);
            await Services.DisposeAsync();
        }

    }
}