using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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
    public static bool RemoveStrayPixels = false; // Flag to enable/disable stray pixel removal

    public enum Algorithm
    {
        KMeans,
        MedianCut
    }

    // Handles Quantization of an Image
    public static (SKBitmap,Dictionary<Color, int>) colorQuantize(SKBitmap bitmap, Algorithm algorithm = Algorithm.KMeans, object? argument = null, int argument2 = 0, bool lab = true)
    {
        SKBitmap accessedBitmap = bitmap.Copy();
        switch (algorithm)
        {
            case Algorithm.KMeans:
                int Iterations = argument == null ? 4 : (int)argument;
                var kMeans = new KMeans(Colors, Iterations);
                kMeans.InitializationAlgorithm = (KMeans.ClusterAlgorithm)argument2;
                // Use the super-pixel enhanced variant for smoother regions.
                (quantizedBitmap, colorDictionary) = kMeans.ApplyKMeansWithSuperpixels(accessedBitmap, superPixelCount: Colors * 80, LAB: lab);
                break;
            case Algorithm.MedianCut:
                var medianCut = new MedianCut();
                (quantizedBitmap, colorDictionary) = medianCut.Quantize(accessedBitmap, Colors, lab);
                break;
        }
        
        // Apply stray pixel removal if enabled
        if (RemoveStrayPixels)
        {
            quantizedBitmap = removeStrayPixels(quantizedBitmap);
        }
        
        // Don't even bother asking what int in colorDictionary was used for before, my guess is it was the total amount of that color?? :shrug:
        return (quantizedBitmap, colorDictionary);
    }

    // Removes stray pixels by replacing them with the most common neighboring color
    public static unsafe SKBitmap removeStrayPixels(SKBitmap bitmap)
    {
        // Create a copy of the bitmap to work with
        SKBitmap outputBitmap = bitmap.Copy();
        
        // Get dimensions
        int width = bitmap.Width;
        int height = bitmap.Height;
        
        // Skip processing if the image is too small
        if (width <= 2 || height <= 2)
            return outputBitmap;
            
        // Get pointers to pixel data
        var srcPtr = (byte*)bitmap.GetPixels().ToPointer();
        var dstPtr = (byte*)outputBitmap.GetPixels().ToPointer();
        
        // Create a temporary array to store the original image data
        byte[] imageData = new byte[width * height * 4];
        System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), imageData, 0, imageData.Length);
        
        // Define adjacent directions (up, right, down, left)
        int[] dx = { 0, 1, 0, -1 };
        int[] dy = { -1, 0, 1, 0 };
        
        // Process each pixel (excluding the border pixels)
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                // Calculate pixel index
                int pixelIndex = (y * width + x) * 4;
                
                // Get current pixel color
                byte b = imageData[pixelIndex];
                byte g = imageData[pixelIndex + 1];
                byte r = imageData[pixelIndex + 2];
                byte a = imageData[pixelIndex + 3];
                
                // Skip transparent pixels
                if (a == 0)
                    continue;
                
                // Check if pixel is isolated (no adjacent pixels of same color)
                bool isStrayPixel = true;
                
                // Check the 4 adjacent neighbors
                for (int i = 0; i < 4 && isStrayPixel; i++)
                {
                    // Calculate neighbor position
                    int nx = x + dx[i];
                    int ny = y + dy[i];
                    
                    // Calculate neighbor index
                    int neighborIndex = (ny * width + nx) * 4;
                    
                    // If any adjacent neighbor has the same color, it's not isolated
                    if (imageData[neighborIndex] == b && 
                        imageData[neighborIndex + 1] == g && 
                        imageData[neighborIndex + 2] == r)
                    {
                        isStrayPixel = false;
                    }
                }
                
                // Replace isolated pixels with most common adjacent color
                if (isStrayPixel)
                {
                    // Count occurrences of each adjacent color
                    Dictionary<(byte, byte, byte), int> colorCount = new Dictionary<(byte, byte, byte), int>();
                    
                    // Loop through the 4 adjacent neighbors
                    for (int i = 0; i < 4; i++)
                    {
                        // Calculate neighbor position
                        int nx = x + dx[i];
                        int ny = y + dy[i];
                        
                        // Calculate neighbor index
                        int neighborIndex = (ny * width + nx) * 4;
                        
                        // Skip transparent neighbors
                        if (imageData[neighborIndex + 3] == 0)
                            continue;
                            
                        // Fetch neighbor color
                        byte nb = imageData[neighborIndex];
                        byte ng = imageData[neighborIndex + 1];
                        byte nr = imageData[neighborIndex + 2];
                        
                        // Add to color count
                        var colorKey = (nr, ng, nb);
                        if (colorCount.ContainsKey(colorKey))
                            colorCount[colorKey]++;
                        else
                            colorCount[colorKey] = 1;
                    }
                    
                    // Find the most common color
                    (byte, byte, byte) mostCommonColor = (0, 0, 0);
                    int maxCount = 0;
                    
                    foreach (var colorEntry in colorCount)
                    {
                        if (colorEntry.Value > maxCount)
                        {
                            maxCount = colorEntry.Value;
                            mostCommonColor = colorEntry.Key;
                        }
                    }
                    
                    // Replace the stray pixel with the most common neighboring color
                    int currentDstIndex = (y * width + x) * 4;
                    dstPtr[currentDstIndex] = mostCommonColor.Item3;     // B
                    dstPtr[currentDstIndex + 1] = mostCommonColor.Item2; // G
                    dstPtr[currentDstIndex + 2] = mostCommonColor.Item1; // R
                    // Keep the original alpha value
                    dstPtr[currentDstIndex + 3] = a;
                }
            }
        }
        
        // Return the Output Bitmap
        return outputBitmap;
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