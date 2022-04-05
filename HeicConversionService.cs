using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HeicToJpg.Logging;
using ImageMagick;

namespace HeicToJpg
{
    public class HeicConversionService
    {
        private readonly HeicConvertOptions options;
        private readonly NonBlockConsoleLogger logger;

        public HeicConversionService(HeicConvertOptions options)
        {
            ValidateOptions(options);
            this.options = options;
            this.logger = new NonBlockConsoleLogger(options);
        }

        public async Task RunConversionAsync(CancellationToken cancellationToken = default)
        {
            var watch = Stopwatch.StartNew();
            var outputExtension = ImageFormatDefaultExtensionService.GetExtensionFor(options.ConvertToFormat);
            var files = await GetFiles();

            var parallelSettings = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };
            await Parallel.ForEachAsync(files, parallelSettings, async (fileName, token) => await ConvertFileAsync(fileName, options.ConvertToFormat, outputExtension, token));
            watch.Stop();
            await logger.WriteLine("HEIC Conversion ended after {0:g}", false, watch.Elapsed);
        }



        private async Task CreateDirectoryIfRequired(string fileName){
            var path = Path.GetDirectoryName(fileName);
            if(existingDirectories.Contains(path)){
                return;
            }

            if(!Directory.Exists(path))
            {
                await logger.WriteLine("Output directory '{0}' does not exist, creating...", true, path);
                    Directory.CreateDirectory(path);
            }

            existingDirectories.Add(path);
        }

        private BlockingCollection<string> existingDirectories = new BlockingCollection<string>();

        private async Task ConvertFileAsync(string fileName, MagickFormat convertToFormat, string outputExtension, CancellationToken cancellationToken = default)
        {
            try
            {
                using var imageToConvert = new MagickImage(fileName);
                imageToConvert.Format = convertToFormat;
                var convertedFileName = $"{Path.GetFileNameWithoutExtension(fileName)}.{outputExtension}";
                var convertedFullPath = Path.Combine(Path.Combine(options.OutputDir, await GetPathRelativeToParent(fileName)), convertedFileName);

                await CreateDirectoryIfRequired(convertedFullPath);
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }
                await imageToConvert.WriteAsync(convertedFullPath);
                await logger.WriteLine("{0}File '{1}' Converted to '{2}'", false, options.Verbose ? $"[{DateTime.Now}]: " : string.Empty, fileName, convertedFullPath);
            }
            catch (Exception ex)
            {
                await logger.WriteLine("Unable to write file '{0}', an error occurred: {1}", false, fileName, ex.Message);
            }
        }

        private ValueTask<string> GetPathRelativeToParent(string fileName)
        {
            var newPath = Path.GetDirectoryName(fileName).Replace(options.InputDir, string.Empty);
            newPath = newPath.TrimStart(Path.DirectorySeparatorChar).TrimStart(Path.AltDirectorySeparatorChar);
            return new ValueTask<string>(newPath);
        }

        private async Task<IEnumerable<string>> GetFiles()
        {
            string[] files;
            if (options.Recursive)
            {
                files = Directory.GetFiles(options.InputDir, "*.heic", SearchOption.AllDirectories);
            }
            else
            {
                files = Directory.GetFiles(options.InputDir, "*.heic", SearchOption.TopDirectoryOnly);
            }


            await logger.WriteLine("Retrieved {0} HEIC files from '{1}'{2}", true, files.Length, options.InputDir, options.Recursive ? " recursively" : string.Empty);
            return files;
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