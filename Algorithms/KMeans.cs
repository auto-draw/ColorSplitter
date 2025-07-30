using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using SkiaSharp;

namespace ColorSplitter.Algorithms;

public class KMeans(int clusters, int iterations)
{
    public enum ClusterAlgorithm
    {
        KMeansPP,
        MedianCut
    }

    public ClusterAlgorithm InitializationAlgorithm { get; set; } = ClusterAlgorithm.MedianCut;

    public (SKBitmap ClusteredBitmap, Dictionary<Color, int> ColorCounts) ApplyKMeans(SKBitmap bitmap, bool LAB = false)
    {
        var pixels = ExtractPixels(bitmap, LAB);
        var clusteredPixels = PerformKMeans(pixels, clusters, iterations, LAB);
        var colorCounts = new Dictionary<Color, int>();
        var clusteredBitmap = ClusterBitmap(clusteredPixels, bitmap.Width, bitmap.Height, LAB, colorCounts);
        return (clusteredBitmap, colorCounts);
    }

    /// <summary>
    /// A smoother variant that first groups pixels into super-pixels using a simplified SLIC implementation in OKLab space.
    /// Each super-pixel is then represented by its mean colour and clustered with k-means. Finally every pixel inherits the
    /// colour of the k-means centroid assigned to its parent super-pixel – this guarantees local colour consistency and
    /// produces significantly cleaner borders.
    /// </summary>
    /// <param name="bitmap">Input image (not modified).</param>
    /// <param name="superPixelCount">Target number of super-pixels – values between 400-2000 work well for most images.</param>
    /// <param name="compactness">The SLIC compactness / M parameter: larger values favour compact shapes.</param>
    /// <param name="LAB">Operate internally in OKLab (recommended) – set to <c>false</c> to use RGB.</param>
    public (SKBitmap ClusteredBitmap, Dictionary<Color, int> ColorCounts) ApplyKMeansWithSuperpixels(
        SKBitmap bitmap,
        int superPixelCount = 200,
        float compactness = 10f,
        bool LAB = true)
    {
        // 1. Run SLIC to obtain label map.
        var seeds = new SeedsSuperpixels(superPixelCount, iterationsPerLevel: 60);
        int[] labels;
        int actualSuperPixelCount = seeds.Generate(bitmap, out labels);

        // 2. Convert to working colour space.
        var pixels = ExtractPixels(bitmap, LAB);

        // 3. Compute mean colour for every super-pixel.
        var channelCount = pixels[0].Length;
        float[][] superPixelMeans = new float[actualSuperPixelCount][];
        int[] counts = new int[actualSuperPixelCount];
        for (int i = 0; i < actualSuperPixelCount; i++)
            superPixelMeans[i] = new float[channelCount];

        for (int p = 0; p < pixels.Length; p++)
        {
            int lbl = labels[p];
            counts[lbl]++;
            for (int c = 0; c < channelCount; c++)
                superPixelMeans[lbl][c] += pixels[p][c];
        }
        for (int s = 0; s < actualSuperPixelCount; s++)
            if (counts[s] > 0)
                for (int c = 0; c < channelCount; c++)
                    superPixelMeans[s][c] /= counts[s];

        // 4. Cluster the super-pixel colours (weights ignored for speed – we assume similarly-sized regions).
        var clusteredSuperPixelColours = PerformKMeans(superPixelMeans, clusters, iterations, LAB);

        // 5. Map back to full-resolution pixel list.
        float[][] clusteredPixels = new float[pixels.Length][];
        for (int i = 0; i < pixels.Length; i++)
        {
            clusteredPixels[i] = clusteredSuperPixelColours[labels[i]];
        }

        // 6. Build bitmap and colour histogram.
        var colorCounts = new Dictionary<Color, int>();
        var clusteredBitmap = ClusterBitmap(clusteredPixels, bitmap.Width, bitmap.Height, LAB, colorCounts);
        return (clusteredBitmap, colorCounts);
    }

    private unsafe float[][] ExtractPixels(SKBitmap bitmap, bool LAB)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        int totalPixels = width * height;

        var srcPtr = (byte*)bitmap.GetPixels().ToPointer();
        var pixels = new float[totalPixels][];

        Parallel.For(0, totalPixels, i =>
        {
            int idx = i * 4;
            float r = srcPtr[idx + 2];
            float g = srcPtr[idx + 1];
            float b = srcPtr[idx];

            pixels[i] = LAB 
                ? LabHelper.RgbToLab(r, g, b) 
                : new[] { r, g, b };
        });

        return pixels;
    }

    private float[][] PerformKMeans(float[][] pixels, int clusterCount, int maxIterations, bool LAB)
    {
        int pixelCount = pixels.Length;
        var random = new Random();

        var centroids = InitializationAlgorithm == ClusterAlgorithm.KMeansPP
            ? InitializeKMeansPP(pixels, clusterCount, random)
            : new MedianCut().PerformMedianCut(pixels, clusterCount, LAB);

        var assignments = new int[pixelCount];

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool hasChanged = false;

            Parallel.For(0, pixelCount, i =>
            {
                int closestCluster = FindClosestCluster(pixels[i], centroids);
                if (assignments[i] != closestCluster)
                {
                    assignments[i] = closestCluster;
                    hasChanged = true;
                }
            });

            if (!hasChanged) break;

            var clusterSums = new float[clusterCount][];
            var clusterCounts = new int[clusterCount];

            for (int i = 0; i < clusterCount; i++)
            {
                clusterSums[i] = new float[pixels[0].Length];
            }

            Parallel.For(0, pixelCount, i =>
            {
                int clusterId = assignments[i];
                lock (clusterSums[clusterId])
                {
                    for (int d = 0; d < pixels[i].Length; d++)
                        clusterSums[clusterId][d] += pixels[i][d];

                    clusterCounts[clusterId]++;
                }
            });

            Parallel.For(0, clusterCount, k =>
            {
                if (clusterCounts[k] > 0)
                {
                    for (int d = 0; d < clusterSums[k].Length; d++)
                        clusterSums[k][d] /= clusterCounts[k];

                    centroids[k] = clusterSums[k];
                }
                else
                {
                    centroids[k] = pixels[random.Next(pixelCount)];
                }
            });
        }

        // --- Ensure that no cluster is empty ------------------------------------
    // Collect which clusters received at least one pixel.
    var used = new bool[clusterCount];
    foreach (int c in assignments) used[c] = true;
    for (int k = 0; k < clusterCount; k++)
    {
        if (used[k]) continue;
        // Pick a random pixel and assign it exclusively to this empty cluster.
        int idx = random.Next(pixelCount);
        assignments[idx] = k;
        centroids[k] = pixels[idx];
    }

    return assignments.Select(c => centroids[c]).ToArray();
    }

    private float[][] InitializeKMeansPP(float[][] pixels, int clusterCount, Random random)
    {
        int pixelCount = pixels.Length;
        var centroids = new float[clusterCount][];

        centroids[0] = pixels[random.Next(pixelCount)];
        for (int i = 1; i < clusterCount; i++)
        {
            var distances = new float[pixelCount];
            Parallel.For(0, pixelCount, j =>
            {
                float minDistance = float.MaxValue;
                for (int k = 0; k < i; k++)
                {
                    float distance = CalcDistance(pixels[j], centroids[k]);
                    if (distance < minDistance)
                        minDistance = distance;
                }
                distances[j] = minDistance;
            });

            float totalDistance = distances.Sum();
            float randomValue = (float)(random.NextDouble() * totalDistance);
            float cumulative = 0;

            for (int j = 0; j < pixelCount; j++)
            {
                cumulative += distances[j];
                if (cumulative >= randomValue)
                {
                    centroids[i] = pixels[j];
                    break;
                }
            }
        }

        return centroids;
    }

    private int FindClosestCluster(float[] pixel, float[][] centroids)
    {
        float minDistance = float.MaxValue;
        int closest = 0;

        for (int i = 0; i < centroids.Length; i++)
        {
            float distance = CalcDistance(pixel, centroids[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = i;
            }
        }

        return closest;
    }

    private unsafe SKBitmap ClusterBitmap(float[][] clusteredPixels, int width, int height, bool LAB, Dictionary<Color, int> colorCounts)
    {
        var outputBitmap = new SKBitmap(width, height);
        var outputPtr = (byte*)outputBitmap.GetPixels().ToPointer();

        Parallel.For(0, clusteredPixels.Length, i =>
        {
            float r, g, b;

            if (LAB)
            {
                var rgb = LabHelper.LabToRgb(clusteredPixels[i][0], clusteredPixels[i][1], clusteredPixels[i][2]);
                r = rgb[0];
                g = rgb[1];
                b = rgb[2];
            }
            else
            {
                // Don't bother asking, I don't know where it got flipped around twice.
                r = clusteredPixels[i][0];
                g = clusteredPixels[i][1];
                b = clusteredPixels[i][2];
            }

            int idx = i * 4;
            outputPtr[idx] = (byte)Math.Clamp(b, 0, 255);
            outputPtr[idx + 1] = (byte)Math.Clamp(g, 0, 255);
            outputPtr[idx + 2] = (byte)Math.Clamp(r, 0, 255);
            outputPtr[idx + 3] = 255;

            var color = Color.FromArgb(255, (byte)Math.Clamp(r, 0, 255), 
                                       (byte)Math.Clamp(g, 0, 255), 
                                       (byte)Math.Clamp(b, 0, 255));
            lock (colorCounts)
            {
                if (!colorCounts.TryAdd(color, 1))
                    colorCounts[color]++;
            }
        });

        return outputBitmap;
    }

    private float CalcDistance(float[] p1, float[] p2)
    {
        float sum = 0;
        for (int i = 0; i < p1.Length; i++)
            sum += (p1[i] - p2[i]) * (p1[i] - p2[i]);
        return sum;
    }
}
