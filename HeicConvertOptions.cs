using CommandLine;
using ImageMagick;
namespace HeicToJpg
{
    public class HeicConvertOptions
    {
        [Option('v', "verbose", Required = false, HelpText = "Set output logging to verbose")]
        public bool Verbose { get; set; }

        [Option('q', "quiet", Required = false, HelpText = "Hide all output logging")]
        public bool Quiet { get; set; }

        [Option('o', "outdir", Required = true, HelpText = "Output directory for converted HEIC files")]
        public string OutputDir { get; set; }

        [Option('i', "inputdir", Required = true, HelpText = "Input directory containing HEIC files")]
        public string InputDir { get; set; }

        [Option('f', "format", Required = false, Default = MagickFormat.Jpg, HelpText = "File format to convert HEIC to")]
        public MagickFormat ConvertToFormat { get; set; }

        [Option('r', "recursive", Required = false, HelpText = "Recursively get all images from subdirectories")]
        public bool Recursive { get; set; }
    }
}