using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using Formatting = Newtonsoft.Json.Formatting;

namespace csp;

public static class ImageExtensions
{
    public static class ImageHelper
    {
        public static Bitmap LoadFromResource(Uri resourceUri)
        {
            return new Bitmap(AssetLoader.Open(resourceUri));
        }

        public static async Task<Bitmap?> LoadFromWeb(Uri url)
        {
            using var httpClient = new HttpClient();
            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsByteArrayAsync();
                return new Bitmap(new MemoryStream(data));
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"An error occurred while downloading image '{url}' : {ex.Message}");
                return null;
            }
        }
    }
    
    public static Bitmap ConvertToAvaloniaBitmap(this SKBitmap bitmap)
    {
        using var bm = bitmap.PeekPixels();
        using var img = SKImage.FromPixels(bm);
        using var enc = img.Encode();
        using var stream = enc.AsStream();
        return new Bitmap(stream);
    }
    
    public static SKBitmap ConvertToSKBitmap(this Bitmap bitmap)
    {
        using (var memoryStream = new MemoryStream())
        {
            // Save Avalonia Bitmap to MemoryStream
            bitmap.Save(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            // Load MemoryStream into SKBitmap
            return SKBitmap.Decode(memoryStream);
        }
    }

    public static unsafe SKBitmap NormalizeColor(this SKBitmap SourceBitmap)
    {
        var srcColor = SourceBitmap.ColorType;
        var srcAlpha = SourceBitmap.AlphaType;

        if (srcColor == SKColorType.Bgra8888) return SourceBitmap;
        // Ensure we don't need to normalize it.

        SKBitmap OutputBitmap = new(SourceBitmap.Width, SourceBitmap.Height);

        var srcPtr = (byte*)SourceBitmap.GetPixels().ToPointer();
        var dstPtr = (byte*)OutputBitmap.GetPixels().ToPointer();

        var width = OutputBitmap.Width;
        var height = OutputBitmap.Height;

        var outColor = OutputBitmap.ColorType;
        
        Console.WriteLine(srcColor);

        for (var row = 0; row < height; row++)
        for (var col = 0; col < width; col++)
            if (srcColor == SKColorType.Gray8 || srcColor == SKColorType.Alpha8)
            {
                var b = *srcPtr++;
                *dstPtr++ = b;
                *dstPtr++ = b;
                *dstPtr++ = b;
                *dstPtr++ = 255;
            }
            else if (srcColor == SKColorType.Rgba8888)
            {
                var r = *srcPtr++;
                var g = *srcPtr++;
                var b = *srcPtr++;
                var a = *srcPtr++;
                *dstPtr++ = b;
                *dstPtr++ = g;
                *dstPtr++ = r;
                *dstPtr++ = a;
            }
            else if (srcColor == SKColorType.Argb4444)
            {
                var r = *srcPtr++;
                var g = *srcPtr++;
                var b = *srcPtr++;
                var a = *srcPtr++;
                *dstPtr++ = (byte)(b * 2);
                *dstPtr++ = (byte)(g * 2);
                *dstPtr++ = (byte)(r * 2);
                *dstPtr++ = a;
            }

        SourceBitmap.Dispose();
        SourceBitmap = OutputBitmap;

        return SourceBitmap;
    }
}

public class Config
{
    public static string FolderPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoDraw");

    public static string ConfigPath = Path.Combine(FolderPath, "config.json");
    public static string ThemesPath = Path.Combine(FolderPath, "Themes");

    public static void init()
    {
        if (!File.Exists(ConfigPath)) return;

        //Utils.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Styles"), ThemesPath);
    }

    public static string? GetEntry(string entry)
    {
        if (!File.Exists(ConfigPath)) return null;
        var json = File.ReadAllText(ConfigPath);
        var parse = JObject.Parse(json);
        return (string?)parse[entry];
    }
}

public static class Utils
{
    public static string LogFolder = Path.Combine(Config.FolderPath, "logs");
    public static string LogsPath = Path.Combine(LogFolder, $"{DateTime.Now:dd.MM.yyyy}.txt");
    public static bool LoggingEnabled = Config.GetEntry("logsEnabled") == "True";
    public static StreamWriter? LogObject;

    public static void Copy(string sourceDirectory, string targetDirectory)
    {
        var diSource = new DirectoryInfo(sourceDirectory);
        var diTarget = new DirectoryInfo(targetDirectory);
        CopyAll(diSource, diTarget);
    }

    public static void CopyAll(DirectoryInfo source, DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        // Copy each file into the new directory.
        foreach (var fi in source.GetFiles())
        {
            Console.WriteLine(@"Copying {0}\{1}", target.FullName, fi.Name);
            fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
        }

        // Copy each subdirectory using recursion.
        foreach (var diSourceSubDir in source.GetDirectories())
        {
            var nextTargetSubDir =
                target.CreateSubdirectory(diSourceSubDir.Name);
            CopyAll(diSourceSubDir, nextTargetSubDir);
        }
    }
}