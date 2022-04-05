using System;
using System.Threading.Tasks;

namespace HeicToJpg.Logging
{
    public class NonBlockConsoleLogger
    {
        private readonly HeicConvertOptions options;

        public NonBlockConsoleLogger(HeicConvertOptions options)
        {
            this.options = options;
        }

        public async Task WriteLine(string value, bool verboseWrite = false, params object[] values)
        {
            if (options.Quiet && !options.Verbose)
            {
                return;
            }

            if (verboseWrite && !options.Verbose)
            {
                return;
            }

            await Console.Out.WriteLineAsync(string.Format(value, values));
        }
    }
}