using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using ColorSplitter.Algorithms;
using SkiaSharp;

namespace ColorSplitter;

public class ImageSplitting
{
    public static byte Colors = 12; // The total amount of colors to split to.
    public static Color backgroundColor = default; // The Alpha Background Color
    private static Dictionary<Color, int> colorDictionary = new(); // The Color Directory, listing all colors.
    private static SKBitmap quantizedBitmap = new();

    public enum Algorithm
    {
        KMeans
    }

    // Handles Quantization of an Image
    public static (SKBitmap,Dictionary<Color, int>) colorQuantize(SKBitmap bitmap, Algorithm algorithm = Algorithm.KMeans, object? argument = null, bool lab = true)
    {
        SKBitmap accessedBitmap = bitmap.Copy();
        switch (algorithm)
        {
            case Algorithm.KMeans:
                int Iterations = argument == null ? 4 : (int)argument;
                var kMeans = new KMeans(Colors, Iterations);
                (quantizedBitmap, colorDictionary) = kMeans.applyKMeans(accessedBitmap, lab);
                break;
        }
        
        
        // Don't even bother asking what int in colorDictionary was used for before, my guess is it was the total amount of that color?? :shrug:
        return (quantizedBitmap, colorDictionary);
    }

    // Gets the Layers from the latest MagickImage
    public static Dictionary<SKBitmap, string> getLayers(bool lowRes)
    {
        // Generates a Dictionary of <Image,Color>
        Dictionary<SKBitmap, string> Layers = new Dictionary<SKBitmap, string>();

        SKBitmap _Bitmap = quantizedBitmap.Copy();

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