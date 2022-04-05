using HeicToJpg.Logging;
using ImageMagick;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentBag<string> existingDirectories = new();
        private readonly HeicConvertOptions options;
        private TransformManyBlock<string, string> firstBlock;
        private TransformBlock<string, FileToConvert> step1GetNewFileName;
        private TransformBlock<FileToConvert, FileToConvert> step2CheckOutputDir;
        private TransformBlock<FileToConvert, ConvertCompletion> step3ConvertFile;
        private BatchBlock<ConvertCompletion> step4BatchBuffer;
        private ActionBlock<ConvertCompletion[]> step5NotifyComplete;

        public HeicConversionPipeline(HeicConvertOptions options)
        {
            this.options = options;
        }
        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var pipeline = CreatePipeline(cancellationToken);

            pipeline.Post(options.InputDir);
            firstBlock.Complete();

            await Task.WhenAll(step1GetNewFileName.Completion, step2CheckOutputDir.Completion, step3ConvertFile.Completion, step4BatchBuffer.Completion, step5NotifyComplete.Completion);
        }

        private ITargetBlock<string> CreatePipeline(CancellationToken cancellationToken = default)
        {
            var outputExtension = ImageFormatDefaultExtensionService.GetExtensionFor(options.ConvertToFormat);

            var fileProcessingOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            var largeExecutionOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                CancellationToken = cancellationToken
            };

            var largeBufferOptions = new DataflowBlockOptions
            {
                CancellationToken = cancellationToken
            };

            firstBlock = new TransformManyBlock<string, string>(inputFolder =>
            {
                string[] files = options.Recursive
                    ? Directory.GetFiles(options.InputDir, "*.heic", SearchOption.AllDirectories)
                    : Directory.GetFiles(options.InputDir, "*.heic", SearchOption.TopDirectoryOnly);
                if (options.Verbose)
                {
                    NonBlockConsoleLogger.WriteLine("Retrieved {0} HEIC files from '{1}'{2}", files.Length, options.InputDir, options.Recursive ? " recursively" : string.Empty);
                }

                return files;
            }, largeExecutionOptions);

            step1GetNewFileName = new TransformBlock<string, FileToConvert>(filename =>
            {
                var newFileName = $"{Path.GetFileNameWithoutExtension(filename)}.{outputExtension}";
                var newFullPath = Path.Combine(options.OutputDir, GetPathRelativeToParent(options.InputDir, filename));

                return new FileToConvert(filename, newFullPath, newFileName);
            }, largeExecutionOptions);

            step2CheckOutputDir = new TransformBlock<FileToConvert, FileToConvert>(fileRecord =>
            {
                CreateDirectoryIfRequiredAsync(fileRecord.ConvertedPath, !options.Quiet || options.Verbose);

                return fileRecord;
            }, largeExecutionOptions);

            step3ConvertFile = new TransformBlock<FileToConvert, ConvertCompletion>(async fileRecord =>
            {
                try
                {
                    using var imageToConvert = new MagickImage(fileRecord.OriginalFilename);
                    imageToConvert.Format = options.ConvertToFormat;
                    await imageToConvert.WriteAsync(Path.Combine(fileRecord.ConvertedPath, fileRecord.ConvertedFileName));

                    return new ConvertCompletion(fileRecord.OriginalFilename, fileRecord.ConvertedFileName);
                }
                catch (Exception ex)
                {
                    return new ConvertCompletion(fileRecord.OriginalFilename, fileRecord.ConvertedFileName, ex);
                }
            }, fileProcessingOptions);

            step4BatchBuffer = new BatchBlock<ConvertCompletion>(10);

            step5NotifyComplete = new ActionBlock<ConvertCompletion[]>(completions =>
            {
                if (options.Quiet && !options.Verbose)
                {
                    return;
                }

                var sb = new StringBuilder();

                for (var i = 0; i < completions.Length; i++)
                {
                    var completion = completions[i];
                    if (options.Verbose)
                    {
                        sb.Append('[');
                        sb.Append(DateTime.Now);
                        sb.Append("]: ");
                    }

                    if (completion.Exception is null)
                    {
                        sb.AppendFormat("{0} was converted to {1}.", completion.OriginalFilename, completion.ConvertedFileName);
                    }
                    else
                    {
                        sb.AppendFormat("{0} could not be converted. Error: {1}", completion.OriginalFilename, completion.Exception.Message);
                    }

                    if (i != completions.Length - 1)
                    {
                        sb.AppendLine();
                    }
                }

                NonBlockConsoleLogger.WriteLine(sb.ToString());
            }, largeExecutionOptions);

            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            firstBlock.LinkTo(step1GetNewFileName, linkOptions);
            step1GetNewFileName.LinkTo(step2CheckOutputDir, linkOptions);
            step2CheckOutputDir.LinkTo(step3ConvertFile, linkOptions);
            step3ConvertFile.LinkTo(step4BatchBuffer, linkOptions);
            step4BatchBuffer.LinkTo(step5NotifyComplete, linkOptions);

            return firstBlock;
        }

        private void CreateDirectoryIfRequiredAsync(string path, bool showMessage)
        {
            if (existingDirectories.Contains(path))
            {
                return;
            }

            try
            {
                if (!Directory.Exists(path))
                {
                    if (showMessage)
                    {
                        NonBlockConsoleLogger.WriteLine("{0} does not exist, creating...", path);
                    }

                    Directory.CreateDirectory(path);
                }
                existingDirectories.Add(path);

            }
            catch
            {
            }
        }

        private static string GetPathRelativeToParent(string inputDir, string fileName)
        {
            var newPath = Path.GetDirectoryName(fileName).Replace(inputDir, string.Empty);
            newPath = newPath.TrimStart(Path.DirectorySeparatorChar).TrimStart(Path.AltDirectorySeparatorChar);
            return newPath;
        }

        private record ConvertCompletion(string OriginalFilename, string ConvertedFileName, Exception Exception = default);

        private record FileToConvert(string OriginalFilename, string ConvertedPath, string ConvertedFileName);
    }
}