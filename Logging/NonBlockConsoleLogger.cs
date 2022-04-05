using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace HeicToJpg.Logging
{
    public static class NonBlockConsoleLogger
    {
        private static BlockingCollection<(string message, object[] values)> messageCollection = new();
        static NonBlockConsoleLogger()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    PrintMessage(messageCollection.Take());
                }
            });
        }

        public static void WriteLine(string message, params object[] values)
            => messageCollection.Add((message, values));

        public static void WriteLine(string message)
            => messageCollection.Add((message, null));

        public static void Flush()
        {
            while (messageCollection.TryTake(out var values))
            {
                PrintMessage(messageCollection.Take());
            }
        }

        private static void PrintMessage((string message, object[] values) values)
        {
            if (!string.IsNullOrEmpty(values.message))
            {
                if (values.values is null)
                {
                    Console.WriteLine(values.message);
                }
                else
                {
                    Console.WriteLine(values.message, values.values);
                }
            }
        }
    }
}