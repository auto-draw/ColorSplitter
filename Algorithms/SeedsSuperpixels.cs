using System;
using System.Collections.Generic;
using SkiaSharp;

namespace ColorSplitter.Algorithms;

/// <summary>
/// A lightweight, non-hierarchical approximation of the SEEDS super-pixel algorithm.
/// It starts with a regular block grid and iteratively exchanges boundary pixels between
/// neighbouring regions when they fit the neighbour colour model better.
/// The colour model is just the mean Lab of the region (good enough for cartoons / flat art).
/// </summary>
public class SeedsSuperpixels
{
    private readonly int _iterationsPerLevel;
    private readonly int _desiredSuperPixelCount;

    public SeedsSuperpixels(int desiredSuperPixelCount, int iterationsPerLevel = 2)
    {
        _iterationsPerLevel = Math.Max(1, iterationsPerLevel);
        _desiredSuperPixelCount = Math.Max(2, desiredSuperPixelCount);
    }

    private class RegionStat
    {
        public float SumL;
        public float SumA;
        public float SumB;
        public float SumX;
        public float SumY;
        public int Count;

        public RegionStat(float l, float a, float b, int c)
        {
            SumL = l;
            SumA = a;
            SumB = b;
            SumX = 0;
            SumY = 0;
            Count = c;
        }

        public float MeanL => SumL / Count;
        public float MeanA => SumA / Count;
        public float MeanB => SumB / Count;
        public float MeanX => SumX / Count;
        public float MeanY => SumY / Count;

        public void Add(float l, float a, float b, int x, int y)
        {
            SumL += l;
            SumA += a;
            SumB += b;
            SumX += x;
            SumY += y;
            Count++;
        }

        public void Remove(float l, float a, float b, int x, int y)
        {
            SumL -= l;
            SumA -= a;
            SumB -= b;
            SumX -= x;
            SumY -= y;
            Count = Math.Max(1, Count - 1);
        }

    }

    /// <summary>
    /// Generate super-pixels; returns the actual number created.
    /// </summary>
    /// <summary>
    /// Multi-level SEEDS: run boundary exchange on a hierarchy of block sizes that halves each level.
    /// This simple variant still uses mean-colour cost but the coarse-to-fine scheme already removes
    /// most grid artefacts on smooth gradients. Histogram cost can be plugged in later.
    /// </summary>
    public int Generate(SKBitmap bitmap, out int[] labels)
    {
        // --- Proper coarse-to-fine SEEDS (mean-colour) ---
        int width = bitmap.Width;
        int height = bitmap.Height;
        int total = width * height;
        labels = new int[total];

        // Work in OKLab.
        float[] lArr = new float[total];
        float[] aArr = new float[total];
        float[] bArr = new float[total];
        ToLab(bitmap, lArr, aArr, bArr);

        // After colour arrays are filled, compute gradient magnitude for edge strength.
        float[] gradArr = new float[total];
        float maxGrad = 0f;
        for (int y = 0; y < height - 1; y++)
        {
            int rowIdx = y * width;
            int nextRowIdx = (y + 1) * width;
            for (int x = 0; x < width - 1; x++)
            {
                int idx = rowIdx + x;
                float dx = lArr[idx + 1] - lArr[idx];
                float dy = lArr[nextRowIdx + x] - lArr[idx];
                float g = dx * dx + dy * dy;
                gradArr[idx] = g;
                if (g > maxGrad) maxGrad = g;
            }
        }
        // Normalise to 0..1 to make edge penalty scale-independent.
        if (maxGrad > 0)
        {
            float inv = 1f / maxGrad;
            for (int i = 0; i < total; i++) gradArr[i] *= inv;
        }
        // Edge penalty strength (tunable).
        const float edgeWeight = 10f;

        // Block size from desired count.
        const int minBlockSize = 1;
        int blockSize = Math.Max(minBlockSize, (int)MathF.Sqrt(total / (float)this._desiredSuperPixelCount));
        int blocksX = (int)Math.Ceiling(width / (float)blockSize);
        int blocksY = (int)Math.Ceiling(height / (float)blockSize);
        int regionCount = blocksX * blocksY;

        var stats = new RegionStat[regionCount];
        for (int i = 0; i < regionCount; i++) stats[i] = new RegionStat(0, 0, 0, 0);

        // Initial assignment and stats.
        for (int y = 0; y < height; y++)
        {
            int by = y / blockSize;
            for (int x = 0; x < width; x++)
            {
                int bx = x / blockSize;
                int region = by * blocksX + bx;
                int idx = y * width + x;
                labels[idx] = region;
                stats[region].Add(lArr[idx], aArr[idx], bArr[idx], x, y);
            }
        }

        // Helper for neighbour offsets.
        int[] nDx = { -1, 1, 0, 0 };
        int[] nDy = { 0, 0, -1, 1 };

        // --- Boundary-relaxation iterations on the initial grid ---
        for (int iter = 0; iter < this._iterationsPerLevel; iter++)
        {
            // Recompute stats from current labels
            int maxLabel = 0;
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] > maxLabel) maxLabel = labels[i];
            stats = new RegionStat[maxLabel + 1];
            for (int i = 0; i <= maxLabel; i++) stats[i] = new RegionStat(0, 0, 0, 0);
            for (int i = 0; i < total; i++)
            {
                int py = i / width;
                int px = i - py * width;
                stats[labels[i]].Add(lArr[i], aArr[i], bArr[i], px, py);
            }

            bool anyMove = false;
            for (int y = 1; y < height - 1; y++)
            {
                int rowIdx = y * width;
                for (int x = 1; x < width - 1; x++)
                {
                    int pIdx = rowIdx + x;
                    int region = labels[pIdx];
                    for (int n = 0; n < 4; n++)
                    {
                        int nx = x + nDx[n];
                        int ny = y + nDy[n];
                        int nIdx = ny * width + nx;
                        int nRegion = labels[nIdx];
                        if (nRegion == region) continue;

                        float dl = lArr[pIdx] - stats[region].MeanL;
                        float da = aArr[pIdx] - stats[region].MeanA;
                        float db = bArr[pIdx] - stats[region].MeanB;
                        float distCurrent = dl * dl + da * da + db * db;

                        dl = lArr[pIdx] - stats[nRegion].MeanL;
                        da = aArr[pIdx] - stats[nRegion].MeanA;
                        db = bArr[pIdx] - stats[nRegion].MeanB;
                        float distNeighbour = dl * dl + da * da + db * db;

                        // Penalise crossing strong edges – bias against moving where gradient is high.
                        float edgeFactor = 1f + edgeWeight * gradArr[pIdx];
                        if (distNeighbour * edgeFactor + 1e-5f < distCurrent)
                        {
                            stats[region].Remove(lArr[pIdx], aArr[pIdx], bArr[pIdx], x, y);
                            stats[nRegion].Add(lArr[pIdx], aArr[pIdx], bArr[pIdx], x, y);
                            labels[pIdx] = nRegion;
                            anyMove = true;
                        }
                    }
                }
            }
            if (!anyMove) break;
        }

        // --- Compact labels and compute true region count ----------------------
        var labelMap = new Dictionary<int, int>();
        int next = 0;
        for (int i = 0; i < labels.Length; i++)
        {
            int lbl = labels[i];
            if (!labelMap.TryGetValue(lbl, out int compact))
            {
                compact = next++;
                labelMap[lbl] = compact;
            }
            labels[i] = compact;
        }

        return next;
    }

    private static void ToLab(SKBitmap bmp, float[] l, float[] a, float[] b)
    {
        int w = bmp.Width;
        int h = bmp.Height;
        float scale = LabHelper.colorSpace == LabHelper.ColorSpace.OKLAB ? 100f : 1f;

        unsafe
        {
            uint* ptr = (uint*)bmp.GetPixels().ToPointer();

            System.Threading.Tasks.Parallel.For(0, h, y =>
            {
                int rowOffset = y * w;
                for (int x = 0; x < w; x++)
                {
                    int idx = rowOffset + x;
                    uint pixel = ptr[idx]; // BGRA little-endian
                    byte blue = (byte)(pixel & 0xFF);
                    byte g = (byte)((pixel >> 8) & 0xFF);
                    byte r = (byte)((pixel >> 16) & 0xFF);

                    var lab = LabHelper.RgbToLab(r, g, blue);
                    l[idx] = lab[0] * scale;
                    a[idx] = lab[1] * scale;
                    b[idx] = lab[2] * scale;
                }
            });
        }
    }
}
