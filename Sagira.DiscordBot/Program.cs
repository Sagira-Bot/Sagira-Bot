namespace Sagira.DiscordBot
{
    internal class Program
    {
        private static void Main()
        {
            new Bot().BotProgramAsync().GetAwaiter().GetResult();
        }
    }
}