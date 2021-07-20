using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Commands.Extensions;
using Remora.Commands.Extensions;
using Remora.Discord.Caching.Extensions;
using Remora.Results;
using Sagira.Commands;

namespace Sagira.Remora
{
    class ServiceHandlerRemora
    {
            private readonly SagiraConfiguration SagiraConf;
            private readonly ItemHandler Handler;

            public ServiceHandlerRemora(SagiraConfiguration conf = null, ItemHandler handl = null)
            {
                SagiraConf = conf ?? new SagiraConfiguration();
                Handler = handl ?? new ItemHandler();
            }

            internal ServiceProvider ConfigureServices()
            {
                return new ServiceCollection()
                .AddSingleton(SagiraConf)
                .AddSingleton(Handler)
                .AddLogging
                (
                    c => c
                        .AddConsole()
                        .AddFilter("System.Net.Http.HttpClient.*.LogicalHandler", LogLevel.Warning)
                        .AddFilter("System.Net.Http.HttpClient.*.ClientHandler", LogLevel.Warning)
                )
                .AddDiscordGateway(x => SagiraConf._discordBotToken)
                .AddDiscordCommands(true)
                .AddCommandGroup<ItemCommands>()
                .AddDiscordCaching()
                .BuildServiceProvider();
            }
    }
}
