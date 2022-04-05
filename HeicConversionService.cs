using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HeicToJpg.DataFlow;
using HeicToJpg.Logging;
using ImageMagick;

namespace HeicToJpg
{
    public class HeicConversionService
    {
        private readonly HeicConvertOptions options;

        public HeicConversionService(HeicConvertOptions options)
        {
            ValidateOptions(options);
            this.options = options;
        }

        public async Task RunConversionAsync(CancellationToken cancellationToken = default)
        {
            var watch = Stopwatch.StartNew();
            var pipeline = new HeicConversionPipeline(options);
            await pipeline.ExecuteAsync(cancellationToken);

            watch.Stop();
            if (!options.Quiet || options.Verbose)
            {
                Console.WriteLine("HEIC Conversion ended after {0:g}", watch.Elapsed);
            }
        }

        private static void ValidateOptions(HeicConvertOptions options)
        {
            if (!Directory.Exists(options.InputDir))
            {
                throw new InvalidOperationException($"Input directory: '{options.InputDir}' does not exist");
            }

            if (!Directory.Exists(options.OutputDir))
            {
                throw new InvalidOperationException($"Output directory: '{options.OutputDir}' does not exist");
            }

            if (options.Verbose && options.Quiet)
            {
                Console.WriteLine("Both verbose and Quiet options set, Quiet is ignored");
            }
        }
    }
}