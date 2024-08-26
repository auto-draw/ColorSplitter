using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using ImageMagick;
using SkiaSharp;

namespace csp;

public class ImageSplitting
{
    public static byte Colors = 12; // The total amount of colors to split to.
    //TODO: Smoothing
    public static byte Smoothing = 0; // Smoothing to be applied
    public static Color backgroundColor = default; // The Alpha Background Color
    private static Dictionary<Color, int> colorDictionary = new(); // The Color Directory, listing all colors.

    public static MagickImage? mImage; // Magick Image, for ImageMagick Library.

    // Handles Quantization of an Image
    public static (SKBitmap,Dictionary<Color, int>) colorQuantize(SKBitmap bitmap)
    {
        // Encode into ImageMagick Image
        var enc = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var stream = enc.AsStream();
        mImage = new MagickImage(stream);
        
        // Generate Quantization Image, with Background Color applied to transparent background.
        mImage.BackgroundColor = new MagickColor(backgroundColor.R,backgroundColor.G,backgroundColor.B);
        mImage.Quantize(new QuantizeSettings()
            { Colors = Colors, DitherMethod = DitherMethod.No, TreeDepth = 8});
        mImage.Alpha(AlphaOption.Remove);

        // Write ImageMagick Image back into SkiaSharp Image
        byte[] imageBytes;
        using (MemoryStream ms = new MemoryStream())
        {
            mImage.Write(ms);
            imageBytes = ms.ToArray();
        }
        
        SKBitmap newBitmap = SKBitmap.Decode(imageBytes);

        
        // Write Colors to the Color Dictionary
        colorDictionary.Clear();
        foreach (var (key, value) in mImage.Histogram())
        {
            colorDictionary.Add(new Color(key.A,key.R,key.G,key.B),value);
        }
        
        // Return the SkiaSharp Image and Color Directory
        return (newBitmap,colorDictionary);
    }

    // Gets the Layers from the latest MagickImage
    public static Dictionary<SKBitmap, string> getLayers(bool lowRes)
    {
        // Generates a Dictionary of <Image,Color>
        Dictionary<SKBitmap, string> Layers = new Dictionary<SKBitmap, string>();

        // Decode ImageMagick to SkiaSharp Image
        byte[]? imageBytes;
        using (MemoryStream ms = new MemoryStream())
        {
            mImage.Write(ms);
            imageBytes = ms.ToArray();
        }

        SKBitmap _Bitmap = SKBitmap.Decode(imageBytes).NormalizeColor();

        // If lowRes, we only handle a 64x64 image, for previews.
        if (lowRes)
        {
            _Bitmap = _Bitmap.Resize(new SKSizeI(64, 64), SKFilterQuality.None);
        }

        // Loop through each image in color dictionary, get the layer of the color, and the Hex Color, add to dictionary.
        foreach (var (key, value) in colorDictionary)
        {
            Layers.Add(getLayer(_Bitmap, key),key.R.ToString("X2") + key.G.ToString("X2") + key.B.ToString("X2"));
        }

        // Return fetched layers
        return Layers;
    }
    
    // Get Layer from a Bitmap, based on Color.
    public static unsafe SKBitmap getLayer(SKBitmap _Bitmap, Color color)
    {
        // Generate a new SkiaSharp bitmap based on original image size.
        SKBitmap OutputBitmap = new(_Bitmap.Width, _Bitmap.Height);

        // Get Memory Pointers for both Original and New Bitmaps
        var srcPtr = (byte*)_Bitmap.GetPixels().ToPointer();
        var dstPtr = (byte*)OutputBitmap.GetPixels().ToPointer();

        // Fetch & store Width and Height, for performance.
        var width = _Bitmap.Width;
        var height = _Bitmap.Height;

        // Loop through all rows & columns
        for (var row = 0; row < height; row++)
        for (var col = 0; col < width; col++)
        {
            // Fetch Original Image's Color from Memory, in BGRA8888 format.
            var b = *srcPtr++;
            var g = *srcPtr++;
            var r = *srcPtr++;
            var a = *srcPtr++;
            // If Color Matches, write to OutputBitmap Memory the same color.
            if (r == color.R && g == color.G && b == color.B)
            {
                *dstPtr++ = b;
                *dstPtr++ = g;
                *dstPtr++ = r;
                *dstPtr++ = a;
            }
            // Else, write Transparent pixel.
            else
            {
                *dstPtr++ = 0;
                *dstPtr++ = 0;
                *dstPtr++ = 0;
                *dstPtr++ = 0;
            }
        }
        
        // Return the Output Bitmap.
        return OutputBitmap;
    }
}