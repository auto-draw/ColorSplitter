using System;
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

        var (quantizedBitmap, colorCounts) = MapPixelsToClosestColors(bitmap, pixels, representativeColors, useLAB);

        return (quantizedBitmap, colorCounts);
    }

    public float[][] PerformMedianCut(float[][] pixels, int numColors, bool useLAB)
    {
        var bins = new List<List<float[]>> { pixels.ToList() };

        while (bins.Count < numColors)
        {
            var binToSplit = bins.OrderByDescending(bin => CalculateMaxVariance(bin)).First();
            bins.Remove(binToSplit);

            var (bin1, bin2) = SplitBin(binToSplit);
            bins.Add(bin1);
            bins.Add(bin2);
        }

        return bins.Select(CalculateAverage).ToArray();
    }

    private (SKBitmap quantizedBitmap, Dictionary<Color, int> colorCounts) MapPixelsToClosestColors(
        SKBitmap bitmap, 
        float[][] pixels, 
        float[][] representativeColors,
        bool useLAB)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        var outputBitmap = new SKBitmap(width, height);
        var colorCounts = new Dictionary<Color, int>();
        var outputPtr = outputBitmap.GetPixels();

        unsafe
        {
            var ptr = (byte*)outputPtr.ToPointer();

            Parallel.For(0, pixels.Length, i =>
            {
                var closestColor = FindClosestColor(pixels[i], representativeColors, useLAB);

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

                int idx = i * 4; // Pointer position
                ptr[idx] = (byte)Math.Clamp(b, 0, 255);
                ptr[idx + 1] = (byte)Math.Clamp(g, 0, 255);
                ptr[idx + 2] = (byte)Math.Clamp(r, 0, 255);
                ptr[idx + 3] = 255;

                var skiaColor = Color.FromArgb(
                    255,
                    (byte)Math.Clamp(r, 0, 255),
                    (byte)Math.Clamp(g, 0, 255),
                    (byte)Math.Clamp(b, 0, 255)
                );

                lock (colorCounts)
                {
                    if (!colorCounts.TryAdd(skiaColor, 1))
                    {
                        colorCounts[skiaColor]++;
                    }
                }
            });
        }

        return (outputBitmap, colorCounts);
    }


    private float[][] ExtractPixels(SKBitmap bitmap, bool useLAB)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        // Prealloc arrays
        var pixels = new float[width * height][];
        var srcPtr = bitmap.GetPixels();

        unsafe
        {
            var ptr = (byte*)srcPtr.ToPointer();

            Parallel.For(0, width * height, i =>
            {
                float b = ptr[i * 4];
                float g = ptr[i * 4 + 1];
                float r = ptr[i * 4 + 2];

                if (useLAB)
                {
                    var lab = LabHelper.RgbToLab(r, g, b);
                    pixels[i] = new[] { lab[0], lab[1], lab[2] };
                }
                else
                {
                    pixels[i] = new[] { b, g, r };
                }
            });
        }

        return pixels;
    }

    private float[] FindClosestColor(float[] pixel, float[][] representativeColors, bool useLAB)
    {
        float minDistance = float.MaxValue;
        float[] closestColor = null;

        foreach (var color in representativeColors)
        {
            float distance;

            // avoid Math.Sqrt when possible
            if (useLAB)
            {
                distance = (pixel[0] - color[0]) * (pixel[0] - color[0])
                           + (pixel[1] - color[1]) * (pixel[1] - color[1])
                           + (pixel[2] - color[2]) * (pixel[2] - color[2]);
            }
            else
            {
                distance = (pixel[0] - color[0]) * (pixel[0] - color[0]) +
                           (pixel[1] - color[1]) * (pixel[1] - color[1]) +
                           (pixel[2] - color[2]) * (pixel[2] - color[2]);
            }

            if (distance < minDistance)
            {
                minDistance = distance;
                closestColor = color;
            }
        }

        return closestColor;
    }

    private float CalculateDistance(float[] pixel1, float[] pixel2, bool useLAB)
    {
        float distance = 0;
        for (int i = 0; i < pixel1.Length; i++)
        {
            distance += (pixel1[i] - pixel2[i]) * (pixel1[i] - pixel2[i]);
        }

        return MathF.Sqrt(distance);
    }

    private float CalculateMaxVariance(List<float[]> bin)
    {
        int dimensions = bin[0].Length;
        float maxVariance = 0;

        for (int i = 0; i < dimensions; i++)
        {
            float mean = bin.Average(pixel => pixel[i]);
            float variance = bin.Sum(pixel => (pixel[i] - mean) * (pixel[i] - mean)) / bin.Count;
            maxVariance = Math.Max(maxVariance, variance);
        }

        return maxVariance;
    }

    private (List<float[]> Bin1, List<float[]> Bin2) SplitBin(List<float[]> bin)
    {
        int dimensions = bin[0].Length;

        float[] ranges = new float[dimensions];

        for (int i = 0; i < dimensions; i++)
        {
            ranges[i] = bin.Max(pixel => pixel[i]) - bin.Min(pixel => pixel[i]);
        }

        int splitAxis = Array.IndexOf(ranges, ranges.Max());

        var sortedBin = bin.OrderBy(pixel => pixel[splitAxis]).ToList();
        int medianIndex = sortedBin.Count / 2;

        return (sortedBin.Take(medianIndex).ToList(), sortedBin.Skip(medianIndex).ToList());
    }

    private float[] CalculateAverage(List<float[]> bin)
    {
        int dimensions = bin[0].Length;
        float[] average = new float[dimensions];

        foreach (var pixel in bin)
        {
            for (int i = 0; i < dimensions; i++)
            {
                average[i] += pixel[i];
            }
        }

        for (int i = 0; i < dimensions; i++)
        {
            average[i] /= bin.Count;
        }

        return average;
    }
}