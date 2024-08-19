using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using ImageMagick;
using SkiaSharp;

namespace csp;

public class ImageSplitting
{
    public static byte Colors = 12;
    public static byte Smoothing = 0;
    public static Color backgroundColor = default;
    private static Dictionary<Color, int> colorDictionary = new();

    public static MagickImage? mImage;

    public static (SKBitmap,Dictionary<Color, int>) colorQuantize(SKBitmap bitmap)
    {
        Console.WriteLine(bitmap);
        var enc = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        Console.WriteLine(enc);
        var stream = enc.AsStream();
        Console.WriteLine(stream);
        mImage = new MagickImage(stream);

        mImage.Quantize(new QuantizeSettings()
            { Colors = Colors, DitherMethod = DitherMethod.No });
        mImage.BackgroundColor = new MagickColor(backgroundColor.R,backgroundColor.G,backgroundColor.B);
        mImage.Alpha(AlphaOption.Remove);

        byte[] imageBytes;
        using (MemoryStream ms = new MemoryStream())
        {
            mImage.Write(ms);
            imageBytes = ms.ToArray();
        }

        SKBitmap newBitmap = SKBitmap.Decode(imageBytes);

        colorDictionary.Clear();
        foreach (var (key, value) in mImage.Histogram())
        {
            colorDictionary.Add(new Color(key.A,key.R,key.G,key.B),value);
        }
        
        return (newBitmap,colorDictionary);
    }

    public static Dictionary<SKBitmap, string> getLayers(bool lowRes)
    {
        Dictionary<SKBitmap, string> Layers = new Dictionary<SKBitmap, string>();

        byte[]? imageBytes;
        using (MemoryStream ms = new MemoryStream())
        {
            mImage.Write(ms);
            imageBytes = ms.ToArray();
        }

        SKBitmap _Bitmap = SKBitmap.Decode(imageBytes).NormalizeColor();

        if (lowRes)
        {
            _Bitmap = _Bitmap.Resize(new SKSizeI(64, 64), SKFilterQuality.None);
        }

        foreach (var (key, value) in colorDictionary)
        {
            Layers.Add(getLayer(_Bitmap, key),key.R.ToString("X2") + key.G.ToString("X2") + key.B.ToString("X2"));
        }

        return Layers;
    }
    
    public static unsafe SKBitmap getLayer(SKBitmap _Bitmap, Color color)
    {
        SKBitmap OutputBitmap = new(_Bitmap.Width, _Bitmap.Height);

        var srcPtr = (byte*)_Bitmap.GetPixels().ToPointer();
        var dstPtr = (byte*)OutputBitmap.GetPixels().ToPointer();

        var width = _Bitmap.Width;
        var height = _Bitmap.Height;

        for (var row = 0; row < height; row++)
        for (var col = 0; col < width; col++)
        {
            var b = *srcPtr++;
            var g = *srcPtr++;
            var r = *srcPtr++;
            var a = *srcPtr++;
            if (r == color.R && g == color.G && b == color.B)
            {
                *dstPtr++ = b;
                *dstPtr++ = g;
                *dstPtr++ = r;
                *dstPtr++ = a;
            }
            else
            {
                *dstPtr++ = 0;
                *dstPtr++ = 0;
                *dstPtr++ = 0;
                *dstPtr++ = 0;
            }
        }
        
        return OutputBitmap;
    }
}