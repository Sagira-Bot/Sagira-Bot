using System;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.Threading.Tasks;
using dotenv.net;
using Interactivity;

namespace Sagira_Bot
{
    /// <summary>
    /// Very bare bones and generic implementation of a Discord.Net bot.
    /// Secrets hidden in .env file (in same directory as binaries).
    /// Implementation of InteractivityService present to handle reactions as buttons and user input.
    /// </summary>
    class SagiraBot
    {
        private readonly DiscordSocketClient DisClient;
        private readonly CommandService CmdService;
        private readonly IServiceProvider DIServices;
        private readonly InteractivityService Interactivity;
        private readonly string Token;
        public readonly string Prefix;
        public static void Main(string[] args) => new SagiraBot().MainAsync().GetAwaiter().GetResult();

        public SagiraBot()
        {
            DotEnv.Load();
            var envs = DotEnv.Read();
            Token = envs["BTOKEN"];
            Prefix = envs["PREFIX"];
            DisClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Debug,
                MessageCacheSize = 50,
            });

            CmdService = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Debug,
                CaseSensitiveCommands = false,
            });
            Interactivity = new InteractivityService(DisClient);
            // Subscribe the logging handler to both the client and the CommandService.
            DisClient.Log += Log;
            CmdService.Log += Log;
            DIServices = (new ServiceHandler(DisClient, CmdService, Interactivity)).BuildServiceProvider();
        }


        private static Task Log(LogMessage message)
        {
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }
            Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message} {message.Exception}");
            Console.ResetColor();
            return Task.CompletedTask;
        }

        private async Task MainAsync()
        {
            // Centralize the logic for commands into a separate method.
            await InitCommands();

            // Login and connect.
            await DisClient.LoginAsync(TokenType.Bot, Token);
            await DisClient.StartAsync();

            // Wait infinitely so your bot actually stays connected.
            await Task.Delay(-1);
        }

        private async Task InitCommands()
        {
            await CmdService.AddModulesAsync(Assembly.GetEntryAssembly(), DIServices);
            DisClient.MessageReceived += HandleCommandAsync;
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            // Bail out if it's a System Message.
            var msg = arg as SocketUserMessage;
            if (msg == null) return;

            // We don't want the bot to respond to itself or other bots.
            if (msg.Author.Id == DisClient.CurrentUser.Id || msg.Author.IsBot) return;

            // Create a number to track where the prefix ends and the command begins
            int pos = 0;

            if (msg.HasCharPrefix(Prefix.ToCharArray()[0], ref pos) || msg.HasMentionPrefix(DisClient.CurrentUser, ref pos))
            {
                // Create a Command Context.
                var context = new SocketCommandContext(DisClient, msg);

                // Execute the command. (result does not indicate a return value, 
                // rather an object stating if the command executed successfully).
                var result = await CmdService.ExecuteAsync(context, pos, DIServices);

                if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
                    await msg.Channel.SendMessageAsync(result.ErrorReason);
            }
        }
    }
}
