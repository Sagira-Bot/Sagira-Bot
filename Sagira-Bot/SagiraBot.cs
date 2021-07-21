using System;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.Threading.Tasks;
using Interactivity;
using System.IO;
using Sagira.Services;
using Discord.Net;
using Newtonsoft.Json;

namespace Sagira
{
    /// <summary>
    /// Very bare bones and generic implementation of a Discord.Net bot.
    /// Secrets hidden in .env file (in same directory as binaries).
    /// Implementation of InteractivityService present to handle reactions as buttons and user input.
    /// </summary>
    class SagiraBot
    {
        private DiscordSocketClient _disClient;
        private CommandService _cmdService;
        private IServiceProvider _diService;
        private InteractionService _interactionService; //Slash-Commands and the Like
        private string _token;
        public char CommandPrefix;
        //public static void Main(string[] args) => new SagiraBot().MainAsync().GetAwaiter().GetResult();
        public async Task RunBot()
        {
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
            _token = config._discordBotToken;
            CommandPrefix = config._defaultBotCommandPrefix;
            ItemHandler handler = new ItemHandler(config._bungieApiKey);

            _disClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Debug,
                MessageCacheSize = 50,
                AlwaysAcknowledgeInteractions = false,
            });
            _interactionService = new InteractionService(_disClient, handler, config._debugServerID);
            _disClient.InteractionCreated += _interactionService.Client_InteractionCreated;
            _disClient.Ready += _interactionService.OnClientReady;
            _cmdService = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Debug,
                CaseSensitiveCommands = false,
            });
            
            // Subscribe the logging handler to both the client and the CommandService.
            _disClient.Log += Log;
            _cmdService.Log += Log;
            _diService = (new ServiceHandler(_disClient, _cmdService, handler, _interactionService)).BuildServiceProvider();
            await MainAsync();
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
            await _disClient.LoginAsync(TokenType.Bot, _token);
            await _disClient.StartAsync();

            // Wait infinitely so your bot actually stays connected.
            await Task.Delay(-1);
        }

        private async Task InitCommands()
        {
            await _cmdService.AddModulesAsync(Assembly.GetEntryAssembly(), _diService);
            _disClient.MessageReceived += HandleCommandAsync;
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            // Bail out if it's a System Message.
            var msg = arg as SocketUserMessage;
            if (msg == null) return;

            // We don't want the bot to respond to itself or other bots.
            if (msg.Author.Id == _disClient.CurrentUser.Id || msg.Author.IsBot) return;

            // Create a number to track where the prefix ends and the command begins
            int pos = 0;

            if (msg.HasCharPrefix(CommandPrefix, ref pos) || msg.HasMentionPrefix(_disClient.CurrentUser, ref pos))
            {
                // Create a Command Context.
                var context = new SocketCommandContext(_disClient, msg);

                // Execute the command. (result does not indicate a return value, 
                // rather an object stating if the command executed successfully).
                var result = await _cmdService.ExecuteAsync(context, pos, _diService);

                if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
                    await msg.Channel.SendMessageAsync(result.ErrorReason);
            }
        }

        

    }
}
