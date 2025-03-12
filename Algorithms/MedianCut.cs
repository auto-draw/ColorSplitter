using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using SkiaSharp;

namespace ColorSplitter.Algorithms;

public class MedianCut
{
    public (SKBitmap quantizedBitmap, Dictionary<Color, int> colorCounts) Quantize(SKBitmap bitmap, int numColors, bool useLAB = false)
    {
        var pixels = ExtractPixels(bitmap, useLAB);

        var representativeColors = PerformMedianCut(pixels, numColors, useLAB);

        return MapPixelsToClosestColors(bitmap, pixels, representativeColors, useLAB);
    }

    public float[][] PerformMedianCut(float[][] pixels, int numColors, bool useLAB)
    {
        var bins = new List<List<float[]>> { pixels.ToList() };

        while (bins.Count < numColors)
        {
            var binToSplit = bins.AsParallel().OrderByDescending(CalculateMaxVariance).First();
            bins.Remove(binToSplit);

            
            var (bin1, bin2) = SplitBin(binToSplit);
            bins.Add(bin1);
            bins.Add(bin2);
        }

        return bins.AsParallel().Select(CalculateAverage).ToArray();
    }

    private unsafe (SKBitmap quantizedBitmap, Dictionary<Color, int> colorCounts) MapPixelsToClosestColors(
        SKBitmap bitmap,
        float[][] pixels,
        float[][] representativeColors,
        bool useLAB)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        var outputBitmap = new SKBitmap(width, height);
        var outputPtr = (byte*)outputBitmap.GetPixels().ToPointer();

        var globalColorCounts = new ConcurrentDictionary<Color, int>();

        Parallel.For(0, pixels.Length, i =>
        {
            var closestColor = FindClosestColor(pixels[i], representativeColors);

            float r, g, b;
            if (useLAB)
            {
                var converted = LabHelper.LabToRgb(closestColor[0], closestColor[1], closestColor[2]);
                r = converted[0];
                g = converted[1];
                b = converted[2];
            }
            else
            {
                b = closestColor[0];
                g = closestColor[1];
                r = closestColor[2];
            }

            int idx = i * 4;
            byte br = (byte)Math.Clamp(b, 0, 255);
            byte bg = (byte)Math.Clamp(g, 0, 255);
            byte bb = (byte)Math.Clamp(r, 0, 255);
            byte alpha = 255;

            // Write pixel data directly
            outputPtr[idx] = br;
            outputPtr[idx + 1] = bg;
            outputPtr[idx + 2] = bb;
            outputPtr[idx + 3] = alpha;

            // Count quantized colors atomically
            var skiaColor = Color.FromArgb(alpha, bb, bg, br);
            globalColorCounts.AddOrUpdate(skiaColor, 1, (key, count) => count + 1);
        });

        return (outputBitmap, new Dictionary<Color, int>(globalColorCounts));
    }

    private unsafe float[][] ExtractPixels(SKBitmap bitmap, bool useLAB)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        // Preallocate memory for all pixels
        var pixels = new float[width * height][];
        var srcPtr = (byte*)bitmap.GetPixels().ToPointer();

        Parallel.For(0, height, row =>
        {
            int rowOffset = row * width * 4;

            for (int col = 0; col < width; col++)
            {
                int idx = rowOffset + (col * 4);
                byte b = srcPtr[idx];
                byte g = srcPtr[idx + 1];
                byte r = srcPtr[idx + 2];

                if (useLAB)
                {
                    var lab = LabHelper.RgbToLab(r, g, b);
                    pixels[row * width + col] = [lab[0], lab[1], lab[2]];
                }
                else
                {
                    pixels[row * width + col] = [b, g, r];
                }
            }
        });

        return pixels;
    }

    private (List<float[]>,List<float[]>) SplitBin(List<float[]> bin)
    {
        // Determine dimension with maximum variance
        int splitDimension = bin[0].Length;
        var variances = Enumerable.Range(0, splitDimension).Select(dim => CalculateVariance(bin, dim)).ToArray();

        int maxVarianceDim = Array.IndexOf(variances, variances.Max());

        // Sort and split pixels by the selected dimension
        bin.Sort((p1, p2) => p1[maxVarianceDim].CompareTo(p2[maxVarianceDim]));
        int mid = bin.Count / 2;

        return (
            bin.Take(mid).ToList(),
            bin.Skip(mid).ToList()
        );
    }

    private float[] CalculateAverage(List<float[]> bin)
    {
        int channels = bin[0].Length;
        var avg = new float[channels];

        foreach (var pixel in bin)
        {
            for (int i = 0; i < channels; i++)
                avg[i] += pixel[i];
        }

        for (int i = 0; i < channels; i++)
            avg[i] /= bin.Count;

        return avg;
    }

    private float CalculateMaxVariance(List<float[]> bin)
    {
        int dimension = bin[0].Length;
        return Enumerable.Range(0, dimension).Max(dim => CalculateVariance(bin, dim));
    }

    private float CalculateVariance(List<float[]> bin, int dim)
    {
        float mean = bin.Average(p => p[dim]);
        return bin.Average(p => (p[dim] - mean) * (p[dim] - mean));
    }

    private float[] FindClosestColor(float[] pixel, float[][] palette)
    {
        float minDistance = float.MaxValue;
        float[] closest = null;

        foreach (var color in palette)
        {
            float dist = EuclideanDistance(pixel, color);
            // AI reccomends I use a custom LabDistance function but I find that rather pointless, although it
            // does mean I can avoid sqrt operations. Though tests so far have pointed to that being pointless.
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = color;
            }
        }

        return closest;
    }

    private float EuclideanDistance(float[] p1, float[] p2)
    {
        // This isn't bottle necked, so don't bother!
        return (float)Math.Sqrt((p1[0] - p2[0]) * (p1[0] - p2[0]) +
                                (p1[1] - p2[1]) * (p1[1] - p2[1]) +
                                (p1[2] - p2[2]) * (p1[2] - p2[2]));
    }
}