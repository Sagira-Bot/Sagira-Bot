namespace Sagira
{
    internal class Program
    {
        private static void Main()
        {
            new SagiraBot().RunBot().GetAwaiter().GetResult();

        }
    }
}