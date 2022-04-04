using System;
using System.IO;
using System.Threading.Tasks;
using ImageMagick;
using CommandLine;
namespace HeicToJpg
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var commandLineParser = new Parser(with =>
            {
                with.EnableDashDash = true;
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = Console.Out;
                with.AutoHelp = true;
            });
            var result = commandLineParser.ParseArguments<HeicConvertOptions>(args);

            await result.MapResult(async options =>
            {
                var conversionService = new HeicConversionService(options);
                try
                {
                    await conversionService.RunConversionAsync();
                    return 0;
                }
                catch(Exception ex)
                {
                    if(!options.Quiet || options.Verbose){
                        Console.WriteLine("An error occurred: {0}", ex.Message);
                    }

                    return 1;
                }
            }, e => Task.FromResult(1));
        }
    }
}
