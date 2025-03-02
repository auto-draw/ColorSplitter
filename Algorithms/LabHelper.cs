using System;

namespace ColorSplitter.Algorithms;

// LAB Helper for RGB to LAB and vice versa.
// "Which do I use?!"
// CIELAB is fairly good for general use cases, however falters in lighter regions
// OKLAB is better at small differences in lighter regions, but can prefer darker regions more.
public static class LabHelper
{
    public enum ColorSpace
    {
        OKLAB,
        CIELAB,
    }
    
    public static ColorSpace colorSpace = ColorSpace.OKLAB;

    public static float[] RgbToLab(float r, float g, float b)
    {
        switch (colorSpace)
        {
            case ColorSpace.OKLAB:
                return RgbToOkLab(r, g, b);
                break;
            case ColorSpace.CIELAB:
                return RgbToCielab(r, g, b);
                break;
        }
        throw new Exception("Color space not found");
    }

    public static float[] LabToRgb(float l, float a, float b)
    {
        switch (colorSpace)
        {
            case ColorSpace.OKLAB:
                return OkLabToRgb(l, a, b);
                break;
            case ColorSpace.CIELAB:
                return CielabToRgb(l, a, b);
                break;
        }
        throw new Exception("Color space not found");
    }
    
    //OKLAB
    private static float[] RgbToOkLab(float r, float g, float b)
    {
        r /= 255f;
        g /= 255f;
        b /= 255f;

        r = r > 0.04045f ? MathF.Pow((r + 0.055f) / 1.055f, 2.4f) : r / 12.92f;
        g = g > 0.04045f ? MathF.Pow((g + 0.055f) / 1.055f, 2.4f) : g / 12.92f;
        b = b > 0.04045f ? MathF.Pow((b + 0.055f) / 1.055f, 2.4f) : b / 12.92f;

        float l = 0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b;
        float m = 0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b;
        float s = 0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b;

        l = MathF.Cbrt(l);
        m = MathF.Cbrt(m);
        s = MathF.Cbrt(s);

        float _l = 0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s;
        float a = 1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s;
        float _b = 0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s;

        return [_l, a, _b];
    }

    private static float[] OkLabToRgb(float l, float a, float b)
    {
        float _l = l + 0.3963377774f * a + 0.2158037573f * b;
        float m = l - 0.1055613458f * a - 0.0638541728f * b;
        float s = l - 0.0894841775f * a - 1.2914855480f * b;

        _l = _l * _l * _l;
        m = m * m * m;
        s = s * s * s;

        float r = 4.0767416621f * _l - 3.3077115913f * m + 0.2309699292f * s;
        float g = -1.2684380046f * _l + 2.6097574011f * m - 0.3413193965f * s;
        float _b = -0.0041960863f * _l - 0.7034186147f * m + 1.7076147010f * s;

        r = r > 0.0031308f ? 1.055f * MathF.Pow(r, 1 / 2.4f) - 0.055f : 12.92f * r;
        g = g > 0.0031308f ? 1.055f * MathF.Pow(g, 1 / 2.4f) - 0.055f : 12.92f * g;
        _b = _b > 0.0031308f ? 1.055f * MathF.Pow(_b, 1 / 2.4f) - 0.055f : 12.92f * _b;

        r = Math.Clamp(r, 0f, 1f);
        g = Math.Clamp(g, 0f, 1f);
        _b = Math.Clamp(_b, 0f, 1f);

        return
        [
            r * 255f,
            g * 255f,
            _b * 255f
        ];
    }
    
    // CIELAB
    private static float[] RgbToCielab(float r, float g, float b)
    {
        r /= 255f;
        g /= 255f;
        b /= 255f;

        r = r > 0.04045f ? MathF.Pow((r + 0.055f) / 1.055f, 2.4f) : r / 12.92f;
        g = g > 0.04045f ? MathF.Pow((g + 0.055f) / 1.055f, 2.4f) : g / 12.92f;
        b = b > 0.04045f ? MathF.Pow((b + 0.055f) / 1.055f, 2.4f) : b / 12.92f;

        float x = r * 0.4124564f + g * 0.3575761f + b * 0.1804375f;
        float y = r * 0.2126729f + g * 0.7151522f + b * 0.0721750f;
        float z = r * 0.0193339f + g * 0.1191920f + b * 0.9503041f;

        x /= 0.95047f;
        y /= 1.00000f;
        z /= 1.08883f;

        x = x > 0.008856f ? MathF.Pow(x, 1f / 3f) : (7.787f * x) + (16f / 116f);
        y = y > 0.008856f ? MathF.Pow(y, 1f / 3f) : (7.787f * y) + (16f / 116f);
        z = z > 0.008856f ? MathF.Pow(z, 1f / 3f) : (7.787f * z) + (16f / 116f);

        float l = (116f * y) - 16f;
        float a = 500f * (x - y);
        float _b = 200f * (y - z);

        return new[] { l, a, _b };
    }

    private static float[] CielabToRgb(float l, float a, float b)
    {
        // Convert LAB to XYZ
        float y = (l + 16f) / 116f;
        float x = (a / 500f) + y;
        float z = y - (b / 200f);

        x = MathF.Pow(x, 3f) > 0.008856f ? MathF.Pow(x, 3f) : (x - 16f / 116f) / 7.787f;
        y = MathF.Pow(y, 3f) > 0.008856f ? MathF.Pow(y, 3f) : (y - 16f / 116f) / 7.787f;
        z = MathF.Pow(z, 3f) > 0.008856f ? MathF.Pow(z, 3f) : (z - 16f / 116f) / 7.787f;

        x *= 0.95047f;
        y *= 1.00000f;
        z *= 1.08883f;

        float r = x * 3.2404542f + y * -1.5371385f + z * -0.4985314f;
        float g = x * -0.9692660f + y * 1.8760108f + z * 0.0415560f;
        float _b = x * 0.0556434f + y * -0.2040259f + z * 1.0572252f;

        r = r > 0.0031308f ? 1.055f * MathF.Pow(r, 1 / 2.4f) - 0.055f : 12.92f * r;
        g = g > 0.0031308f ? 1.055f * MathF.Pow(g, 1 / 2.4f) - 0.055f : 12.92f * g;
        _b = _b > 0.0031308f ? 1.055f * MathF.Pow(_b, 1 / 2.4f) - 0.055f : 12.92f * _b;

        r = Math.Clamp(r, 0f, 1f);
        g = Math.Clamp(g, 0f, 1f);
        _b = Math.Clamp(_b, 0f, 1f);

        return new[]
        {
            r * 255f,
            g * 255f,
            _b * 255f
        };
    }
}