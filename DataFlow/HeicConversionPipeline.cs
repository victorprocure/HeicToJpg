using HeicToJpg.Logging;
using ImageMagick;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace HeicToJpg.DataFlow
{
    public class HeicConversionPipeline
    {
        private readonly HeicConvertOptions options;
        private TransformManyBlock<string, (DirectoryInfo, FileInfo)> firstBlock;
        private TransformBlock<(DirectoryInfo, FileInfo), FileToConvert> step1GetNewFileName;
        private TransformBlock<FileToConvert, FileToConvert> step2CheckOutputDir;
        private TransformBlock<FileToConvert, ConvertCompletion> step3ConvertFile;
        private ActionBlock<ConvertCompletion> step4NotifyComplete;

        public HeicConversionPipeline(HeicConvertOptions options)
        {
            ResourceLimits.LimitMemory(new Percentage(90));
            this.options = options;
        }
        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var pipeline = CreatePipeline(cancellationToken);

            await pipeline.SendAsync(options.InputDir);
            firstBlock.Complete();

            await Task.WhenAll(step1GetNewFileName.Completion, step2CheckOutputDir.Completion, step3ConvertFile.Completion, step4NotifyComplete.Completion);
        }

        private ITargetBlock<string> CreatePipeline(CancellationToken cancellationToken = default)
        {
            var outputExtension = ImageFormatDefaultExtensionService.GetExtensionFor(options.ConvertToFormat);
            var outputDirectoryInfo = new DirectoryInfo(options.OutputDir);
            var fileProcessingOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            var largeExecutionOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            var largeBufferOptions = new DataflowBlockOptions
            {
                CancellationToken = cancellationToken
            };

            firstBlock = new TransformManyBlock<string, (DirectoryInfo, FileInfo)>(inputFolder =>
            {
                var directory = new DirectoryInfo(inputFolder);
                if (!directory.Exists)
                {
                    NonBlockConsoleLogger.WriteLine("{0} folder does not exist", inputFolder);
                    return Enumerable.Empty<(DirectoryInfo, FileInfo)>();
                }

                var filesInfo = directory.GetFiles("*.heic", new EnumerationOptions { RecurseSubdirectories = options.Recursive });

                if (options.Verbose)
                {
                    NonBlockConsoleLogger.WriteLine("Retrieved {0} HEIC files from '{1}'{2}", filesInfo.Length, options.InputDir, options.Recursive ? " recursively" : string.Empty);
                }

                return filesInfo.Select(fi => (directory, fi));
            }, largeExecutionOptions);

            step1GetNewFileName = new TransformBlock<(DirectoryInfo, FileInfo), FileToConvert>(inputFileInfo =>
            {
                var convertedFileInfo = GetConvertFileInfo(inputFileInfo.Item2, inputFileInfo.Item1, outputDirectoryInfo, outputExtension);

                return new FileToConvert(inputFileInfo.Item2, convertedFileInfo);
            }, largeExecutionOptions);

            step2CheckOutputDir = new TransformBlock<FileToConvert, FileToConvert>(fileRecord =>
            {
                CreateDirectoryIfRequiredAsync(fileRecord.ConvertedFile, !options.Quiet || options.Verbose);

                return fileRecord;
            }, largeExecutionOptions);

            step3ConvertFile = new TransformBlock<FileToConvert, ConvertCompletion>(async fileRecord =>
            {
                try
                {
                    using var imageToConvert = new MagickImage(fileRecord.OriginalFile, MagickFormat.Heic);
                    imageToConvert.Format = options.ConvertToFormat;
                    await imageToConvert.WriteAsync(fileRecord.ConvertedFile);

                    return new ConvertCompletion(fileRecord.OriginalFile, fileRecord.ConvertedFile);
                }
                catch (Exception ex)
                {
                    return new ConvertCompletion(fileRecord.OriginalFile, fileRecord.ConvertedFile, ex);
                }
            }, fileProcessingOptions);

            step4NotifyComplete = new ActionBlock<ConvertCompletion>(completion =>
            {
                if (options.Quiet && !options.Verbose)
                {
                    return;
                }

                var sb = new StringBuilder();
                    if (options.Verbose)
                    {
                        sb.Append('[');
                        sb.Append(DateTime.Now);
                        sb.Append("]: ");
                    }

                    if (completion.Exception is null)
                    {
                        sb.AppendFormat("{0} was converted to {1}.", completion.OriginalFile.FullName, completion.ConvertedFile.FullName);
                    }
                    else
                    {
                        sb.AppendFormat("{0} could not be converted. Error: {1}", completion.OriginalFile.FullName, completion.Exception.Message);
                    }

                NonBlockConsoleLogger.WriteLine(sb.ToString());
            }, largeExecutionOptions);

            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            firstBlock.LinkTo(step1GetNewFileName, linkOptions);
            step1GetNewFileName.LinkTo(step2CheckOutputDir, linkOptions);
            step2CheckOutputDir.LinkTo(step3ConvertFile, linkOptions);
            step3ConvertFile.LinkTo(step4NotifyComplete, linkOptions);

            return firstBlock;
        }

        private void CreateDirectoryIfRequiredAsync(FileInfo fileInfo, bool showMessage)
        {
            if (!fileInfo.Directory.Exists)
            {
                if (showMessage)
                {
                    NonBlockConsoleLogger.WriteLine("{0} does not exist, creating...", fileInfo.DirectoryName);
                }

                fileInfo.Directory.Create();
            }
        }


        private static FileInfo GetConvertFileInfo(FileInfo originalFile, DirectoryInfo inputDirectory, DirectoryInfo outputDirectory, string convertFileExtension)
        {
            var newFileName = Path.ChangeExtension(originalFile.Name, convertFileExtension);
            var originalDirectory = originalFile.DirectoryName;
            var newDirectory = originalDirectory.Replace(inputDirectory.FullName, outputDirectory.FullName, true, null);

            var convertedFileInfo = new FileInfo(Path.Combine(newDirectory, newFileName));

            return convertedFileInfo;
        }

        private record ConvertCompletion(FileInfo OriginalFile, FileInfo ConvertedFile, Exception Exception = default);

        private record FileToConvert(FileInfo OriginalFile, FileInfo ConvertedFile);
    }
}