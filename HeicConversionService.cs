using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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

        public async Task RunConversionAsync()
        {
            var files = GetFiles();
            var outputExtension = ImageFormatDefaultExtensionService.GetExtensionFor(options.ConvertToFormat);
            await Parallel.ForEachAsync(files, async (fileName, _) => await ConvertFileAsync(fileName, options.ConvertToFormat, outputExtension));
        }

        private async Task ConvertFileAsync(string fileName, MagickFormat convertToFormat, string outputExtension)
        {
            if (options.Verbose)
            {
                Console.WriteLine("Beginning conversion of '{0}'", fileName);
            }

            try
            {
                using var imageToConvert = new MagickImage(fileName);
                imageToConvert.Format = convertToFormat;
                var convertedFileName = $"{Path.GetFileNameWithoutExtension(fileName)}.{outputExtension}";
                var convertedFullPath = Path.Combine(Path.Combine(options.OutputDir, await GetPathRelativeToParent(fileName)), convertedFileName);
                if (!options.Quiet || options.Verbose)
                {
                    Console.WriteLine("Writing '{0}' to disk", convertedFullPath);
                }

                if (!Directory.Exists(Path.GetDirectoryName(convertedFullPath)))
                {
                    var directory = Path.GetDirectoryName(convertedFullPath);
                    if (options.Verbose)
                    {
                        Console.WriteLine("Output directory '{0}' does not exist, creating...", directory);
                    }
                    Directory.CreateDirectory(directory);
                }

                await imageToConvert.WriteAsync(convertedFullPath);
                if (options.Verbose)
                {
                    Console.WriteLine("End conversion of '{0}'", fileName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to write file '{0}', an error occurred: {1}", fileName, ex.Message);
            }
        }

        private ValueTask<string> GetPathRelativeToParent(string fileName)
        {
            var newPath = Path.GetDirectoryName(fileName).Replace(options.InputDir, string.Empty);
            newPath = newPath.TrimStart(Path.DirectorySeparatorChar).TrimStart(Path.AltDirectorySeparatorChar);
            return new ValueTask<string>(newPath);
        }

        private IEnumerable<string> GetFiles()
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

            if (options.Verbose)
            {
                Console.WriteLine("Retrieved {0} HEIC files from '{1}'{2}", files.Length, options.InputDir, options.Recursive ? " recursively" : string.Empty);
            }

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