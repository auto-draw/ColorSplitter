using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace ColorSplitter.Algorithms;

public class MedianCut
{
    public SKBitmap quantize(SKBitmap bitmap, int numColors, bool useLAB = false)
    {
        var pixels = extractPixels(bitmap, useLAB);

        var representativeColors = performMedianCut(pixels, numColors);

        var quantizedBitmap = mapPixelsToClosestColor(bitmap, pixels, representativeColors);

        return quantizedBitmap;
    }

    // For improving the KMeans cluster initialization :P
    public float[][] initializeClusters(float[][] pixels, int numClusters)
    {
        return performMedianCut(pixels, numClusters);
    }

    private float[][] performMedianCut(float[][] pixels, int numColors)
    {
        var bins = new List<List<float[]>> { pixels.ToList() };

        while (bins.Count < numColors)
        {
            var binToSplit = bins.OrderByDescending(bin => calculateVariance(bin)).First();
            bins.Remove(binToSplit);

            var (bin1, bin2) = splitBin(binToSplit);

            bins.Add(bin1);
            bins.Add(bin2);
        }

        return bins.Select(bin => calculateAverage(bin)).ToArray();
    }

    private (List<float[]> bin1, List<float[]> bin2) splitBin(List<float[]> bin)
    {
        int dimension = bin[0].Length;
        float[] minValues = new float[dimension];
        float[] maxValues = new float[dimension];

        for (int i = 0; i < dimension; i++)
        {
            minValues[i] = bin.Min(pixel => pixel[i]);
            maxValues[i] = bin.Max(pixel => pixel[i]);
        }

        int splitAxis = Array.IndexOf(maxValues, maxValues.Max());

        var sortedBin = bin.OrderBy(pixel => pixel[splitAxis]).ToList();
        int medianIndex = sortedBin.Count / 2;

        return (sortedBin.Take(medianIndex).ToList(), sortedBin.Skip(medianIndex).ToList());
    }
    
    private float[] calculateAverage(List<float[]> bin)
    {
        int dimension = bin[0].Length;
        float[] average = new float[dimension];

        foreach (var pixel in bin)
        {
            for (int i = 0; i < dimension; i++)
            {
                average[i] += pixel[i];
            }
        }

        for (int i = 0; i < dimension; i++)
        {
            average[i] /= bin.Count;
        }

        return average;
    }

    private float calculateVariance(List<float[]> bin)
    {
        int dimension = bin[0].Length;
        float totalVariance = 0;

        for (int i = 0; i < dimension; i++)
        {
            float mean = bin.Average(pixel => pixel[i]);
            float variance = bin.Sum(pixel => (pixel[i] - mean) * (pixel[i] - mean)) / bin.Count;
            totalVariance += variance;
        }

        return totalVariance;
    }

    private SKBitmap mapPixelsToClosestColor(SKBitmap bitmap, float[][] pixels, float[][] representativeColors)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        var outputBitmap = new SKBitmap(width, height);
        var outputPtr = outputBitmap.GetPixels();

        unsafe
        {
            var ptr = (byte*)outputPtr.ToPointer();

            for (int i = 0; i < pixels.Length; i++)
            {
                var closestColor = findClosestColor(pixels[i], representativeColors);

                ptr[i * 4] = (byte)closestColor[0];     // B
                ptr[i * 4 + 1] = (byte)closestColor[1]; // G
                ptr[i * 4 + 2] = (byte)closestColor[2]; // R
                ptr[i * 4 + 3] = 255;                  // A
            }
        }

        return outputBitmap;
    }

    private float[] findClosestColor(float[] pixel, float[][] representativeColors)
    {
        float minDistance = float.MaxValue;
        float[] closestColor = null;

        foreach (var color in representativeColors)
        {
            float distance = calcDistance(pixel, color);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestColor = color;
            }
        }

        return closestColor;
    }

    private float[][] extractPixels(SKBitmap bitmap, bool useLAB)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        var pixels = new float[width * height][];
        var srcPtr = bitmap.GetPixels();

        unsafe
        {
            var ptr = (byte*)srcPtr.ToPointer();

            for (int i = 0; i < width * height; i++)
            {
                float r = ptr[i * 4 + 2];
                float g = ptr[i * 4 + 1];
                float b = ptr[i * 4];

                if (useLAB)
                {
                    var lab = LabHelper.RgbToLab(r, g, b);
                    pixels[i] = new[] { lab[0], lab[1], lab[2] };
                }
                else
                {
                    pixels[i] = new[] { r, g, b };
                }
            }
        }

        return pixels;
    }

    private float calcDistance(float[] a, float[] b)
    {
        float distance = 0;

        for (int i = 0; i < a.Length; i++)
        {
            distance += (a[i] - b[i]) * (a[i] - b[i]);
        }

        return (float)Math.Sqrt(distance);
    }
}