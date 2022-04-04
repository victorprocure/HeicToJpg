using ImageMagick;

namespace HeicToJpg
{
    public static class ImageFormatDefaultExtensionService
    {
        public static string GetExtensionFor(MagickFormat imageFormat)
         => imageFormat switch
         {
             MagickFormat.Ai => "ai",
             MagickFormat.APng => "apng",
             MagickFormat.Avi => "avi",
             MagickFormat.Bmp or MagickFormat.Bmp2 or MagickFormat.Bmp3 => "bmp",
             MagickFormat.Brf => "brf",
             MagickFormat.Eps or MagickFormat.Eps2 or MagickFormat.Eps3 or MagickFormat.Epsi or MagickFormat.Epsf => "eps",
             MagickFormat.Gif or MagickFormat.Gif87 => "gif",
             MagickFormat.Jpeg => "jpeg",
             MagickFormat.Jpg => "jpg",
             MagickFormat.Png or MagickFormat.Png00 or MagickFormat.Png8 or MagickFormat.Png24 or MagickFormat.Png32 or MagickFormat.Png48 or MagickFormat.Png64 => "png",
             _ => imageFormat.ToString().ToLowerInvariant()
         };
    }
}