using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Media;
using SkiaSharp;

namespace ColorSplitter.Algorithms;

public class KMeans(int clusters, int iterations)
{
    public enum clusterAlgorithm
    {
        kMeansPP,
        MedianCut
    }

    public clusterAlgorithm initializationAlgorithm = clusterAlgorithm.MedianCut;
    
    public (SKBitmap clusteredBitmap, Dictionary<Color, int> colorCounts) applyKMeans(SKBitmap bitmap, bool LAB = false)
    {
        var pixels = extractPixels(bitmap, LAB);

        var clusteredPixels = performKMeans(pixels, clusters, iterations, LAB);

        var colorCounts = new Dictionary<Color, int>();
        
        var clusteredBitmap = clusterBitmap(clusteredPixels, bitmap.Width, bitmap.Height, LAB, colorCounts);

        return (clusteredBitmap, colorCounts);
    }

    
    private float[][] extractPixels(SKBitmap bitmap, bool LAB)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        var srcPtr = bitmap.GetPixels();
        var pixels = new float[width * height][];

        unsafe
        {
            byte* ptr = (byte*)srcPtr.ToPointer();

            Parallel.For(0, width * height, i =>
            {
                float r = ptr[i * 4 + 2];
                float g = ptr[i * 4 + 1];
                float b = ptr[i * 4];

                if (LAB)
                {
                    pixels[i] = LabHelper.RgbToLab(r, g, b);
                }
                else
                {
                    pixels[i] = [r, g, b];
                }
            });
        }

        return pixels;
    }

    
    // k-means++ implementation btw
    private float[][] performKMeans(float[][] pixels, int _clusters, int maxIterations, bool LAB) // k-means is scary, also a bitch to fully understand.
    {
        int pixelCount = pixels.Length;
        int dimension = pixels[0].Length;
        
        var random = new Random();
        
        var centroids = new float[_clusters][];
        if (initializationAlgorithm == clusterAlgorithm.kMeansPP)
        {
            centroids[0] = pixels[random.Next(pixelCount)];
            for (int i = 1; i < _clusters; i++)
            {
                var distances = new float[pixelCount];
                
                for (int j = 0; j < pixelCount; j++)
                {
                    float minDistance = float.MaxValue;
                    for (int k = 0; k < i; k++)
                    {
                        float distance = calcDistance(pixels[j], centroids[k]);
                        if (distance < minDistance)
                            minDistance = distance;
                    }
                    distances[j] = minDistance;
                }

                float totalDistance = 0;
                for (int j = 0; j < pixelCount; j++)
                    totalDistance += distances[j] * distances[j];

                float randomValue = (float)(random.NextDouble() * totalDistance);
                float sum = 0;
                for (int j = 0; j < pixelCount; j++)
                {
                    sum += distances[j] * distances[j];
                    if (sum >= randomValue)
                    {
                        centroids[i] = pixels[j];
                        break;
                    }
                }
            }
        }else if (initializationAlgorithm == clusterAlgorithm.MedianCut)
        {
            var medianCut = new MedianCut();
            centroids = medianCut.PerformMedianCut(pixels, _clusters, LAB);
        }


        var assignments = new int[pixelCount];

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool hasChanged = false;

            for (int i = 0; i < pixelCount; i++)
            {
                int closestCluster = findClosestCluster(pixels[i], centroids);
                if (assignments[i] != closestCluster)
                {
                    assignments[i] = closestCluster;
                    hasChanged = true;
                }
            }

            if (!hasChanged)
                break;

            var newCentroids = new float[_clusters][];
            var counts = new int[_clusters];

            for (int i = 0; i < _clusters; i++)
                newCentroids[i] = new float[dimension];

            for (int i = 0; i < pixelCount; i++)
            {
                int cluster = assignments[i];
                for (int j = 0; j < dimension; j++)
                    newCentroids[cluster][j] += pixels[i][j];

                counts[cluster]++;
            }

            for (int i = 0; i < _clusters; i++)
                if (counts[i] > 0)
                    for (int j = 0; j < dimension; j++)
                        newCentroids[i][j] /= counts[i];
                else
                    newCentroids[i] = pixels[random.Next(pixelCount)];

            centroids = newCentroids;
        }

        for (int i = 0; i < pixelCount; i++)
            pixels[i] = centroids[assignments[i]];

        return pixels;
    }
    
    private int findClosestCluster(float[] pixel, float[][] centroids)
    {
        int closestIndex = 0;
        float minDistance = float.MaxValue;

        for (int i = 0; i < centroids.Length; i++)
        {
            float distance = calcDistance(pixel, centroids[i]);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private float calcDistance(float[] a, float[] b) // calc stands for calculator btw
    {
        float distance = 0;

        for (int i = 0; i < a.Length; i++)
        {
            float diff = a[i] - b[i];
            distance += diff * diff;
        }

        return distance;
    }

    private SKBitmap clusterBitmap(float[][] clusteredPixels, int width, int height, bool LAB, Dictionary<Color, int> colorCounts)
    {
        var processedBitmap = new SKBitmap(width, height);
        var dstPtr = processedBitmap.GetPixels();

        unsafe
        {
            var ptr = (byte*)dstPtr.ToPointer();

            for (int i = 0; i < clusteredPixels.Length; i++)
            {
                float[] pixel = clusteredPixels[i];
                Color color;

                if (LAB)
                {
                    var rgb = LabHelper.LabToRgb(pixel[0], pixel[1], pixel[2]);
                    color = Color.FromArgb(255, (byte)rgb[0], (byte) rgb[1], (byte) rgb[2]);
                }
                else
                {
                    color = Color.FromArgb(255, (byte)pixel[0], (byte)pixel[1], (byte)pixel[2]);
                }

                ptr[i * 4] = color.B;     // B
                ptr[i * 4 + 1] = color.G; // G
                ptr[i * 4 + 2] = color.R; // R
                ptr[i * 4 + 3] = 255;     // A

                if (!colorCounts.TryAdd(color, 1))
                    colorCounts[color]++;
            }
        }

        return processedBitmap;
    }
}