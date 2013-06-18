using Nito.AsyncEx;
using System;

namespace Gulp
{
    class Program
    {
        private static Spider spider = new Spider();

        static void Main(string[] args)
        {
            AsyncContext.Run(() => Fetch());
        }

        private static async void Fetch()
        {
            spider.OnLog += (s, e) =>
                Log(e.Value.Kind, e.Value.Message, e.Value.Color);

            spider.OnFailure += (s, e) =>
            {
                foreach (var error in e.Value.InnerExceptions)
                    Log(LogKind.Failure, error.Message, ConsoleColor.DarkRed);
            };

            var initialLink = new Link(new Uri("http://www.bbc.co.uk/"), "http://www.bbc.co.uk/news/", LinkKind.HTML);

            spider.Fetch(initialLink);

            Console.ReadKey();

            await spider.Cancel();

            Console.WriteLine();
            Console.Write("Press any key to shutdown...");

            Console.ReadKey();
        }

        private static void Log(LogKind kind, string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;

            Console.WriteLine(kind.ToString().PadRight(10) + message);
        }
    }
}
