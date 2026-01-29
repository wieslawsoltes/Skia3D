using System.Numerics;
using SkiaSharp;

namespace Skia3D.Rendering;

public sealed class PostProcessor
{
    public void Apply(SKBitmap source, SKCanvas target, SKRect dest, PostProcessSettings settings)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (!settings.Enabled)
        {
            DrawBitmap(source, target, dest);
            return;
        }

        int width = source.Width;
        int height = source.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var pixels = source.Pixels;
        int count = pixels.Length;
        var colors = new Vector3[count];
        var alphas = new byte[count];
        float exposure = MathF.Max(0f, settings.Exposure);

        for (int i = 0; i < count; i++)
        {
            var c = pixels[i];
            colors[i] = new Vector3(c.Red / 255f, c.Green / 255f, c.Blue / 255f) * exposure;
            alphas[i] = c.Alpha;
        }

        if (settings.Bloom.Enabled)
        {
            var bloom = BuildBloom(colors, width, height, settings.Bloom);
            float intensity = MathF.Max(0f, settings.Bloom.Intensity);
            if (intensity > 0f)
            {
                for (int i = 0; i < count; i++)
                {
                    colors[i] += bloom[i] * intensity;
                }
            }
        }

        ApplyToneMapping(colors, settings.ToneMapping);

        if (settings.Fxaa.Enabled)
        {
            ApplyFxaa(colors, width, height, settings.Fxaa);
        }

        var output = new SKColor[count];
        for (int i = 0; i < count; i++)
        {
            var color = colors[i];
            color.X = Math.Clamp(color.X, 0f, 1f);
            color.Y = Math.Clamp(color.Y, 0f, 1f);
            color.Z = Math.Clamp(color.Z, 0f, 1f);
            output[i] = new SKColor(
                (byte)(color.X * 255f),
                (byte)(color.Y * 255f),
                (byte)(color.Z * 255f),
                alphas[i]);
        }

        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        bitmap.Pixels = output;
        DrawBitmap(bitmap, target, dest);
    }

    private static void DrawBitmap(SKBitmap bitmap, SKCanvas target, SKRect dest)
    {
        using var paint = new SKPaint { FilterQuality = SKFilterQuality.Medium, IsAntialias = true };
        target.DrawBitmap(bitmap, new SKRect(0, 0, bitmap.Width, bitmap.Height), dest, paint);
    }

    private static Vector3[] BuildBloom(Vector3[] source, int width, int height, BloomSettings settings)
    {
        var bright = new Vector3[source.Length];
        float threshold = Math.Clamp(settings.Threshold, 0f, 1f);
        float invRange = 1f / MathF.Max(1e-4f, 1f - threshold);

        for (int i = 0; i < source.Length; i++)
        {
            var color = source[i];
            float peak = MathF.Max(color.X, MathF.Max(color.Y, color.Z));
            if (peak <= threshold)
            {
                bright[i] = Vector3.Zero;
                continue;
            }

            float scale = (peak - threshold) * invRange;
            bright[i] = color * scale;
        }

        int radius = Math.Max(0, settings.Radius);
        if (radius == 0)
        {
            return bright;
        }

        var blurred = new Vector3[source.Length];
        BoxBlur(bright, blurred, width, height, radius);
        return blurred;
    }

    private static void BoxBlur(Vector3[] source, Vector3[] destination, int width, int height, int radius)
    {
        var temp = new Vector3[source.Length];

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                Vector3 sum = Vector3.Zero;
                int count = 0;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int cx = Math.Clamp(x + dx, 0, width - 1);
                    sum += source[row + cx];
                    count++;
                }

                temp[row + x] = sum / count;
            }
        }

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                Vector3 sum = Vector3.Zero;
                int count = 0;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int cy = Math.Clamp(y + dy, 0, height - 1);
                    sum += temp[cy * width + x];
                    count++;
                }

                destination[row + x] = sum / count;
            }
        }
    }

    private static void ApplyToneMapping(Vector3[] colors, ToneMappingMode mode)
    {
        if (mode == ToneMappingMode.None)
        {
            return;
        }

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = mode switch
            {
                ToneMappingMode.Reinhard => Reinhard(colors[i]),
                ToneMappingMode.Aces => Aces(colors[i]),
                _ => colors[i]
            };
        }
    }

    private static Vector3 Reinhard(Vector3 color)
    {
        return new Vector3(
            color.X / (1f + color.X),
            color.Y / (1f + color.Y),
            color.Z / (1f + color.Z));
    }

    private static Vector3 Aces(Vector3 color)
    {
        const float a = 2.51f;
        const float b = 0.03f;
        const float c = 2.43f;
        const float d = 0.59f;
        const float e = 0.14f;

        return new Vector3(
            (color.X * (a * color.X + b)) / (color.X * (c * color.X + d) + e),
            (color.Y * (a * color.Y + b)) / (color.Y * (c * color.Y + d) + e),
            (color.Z * (a * color.Z + b)) / (color.Z * (c * color.Z + d) + e));
    }

    private static void ApplyFxaa(Vector3[] colors, int width, int height, FxaaSettings settings)
    {
        float edgeThreshold = Math.Clamp(settings.EdgeThreshold, 0f, 1f);
        float edgeThresholdMin = Math.Clamp(settings.EdgeThresholdMin, 0f, 1f);
        float blend = Math.Clamp(settings.SubpixelBlend, 0f, 1f);
        if (blend <= 0f)
        {
            return;
        }

        var copy = new Vector3[colors.Length];
        Array.Copy(colors, copy, colors.Length);

        for (int y = 1; y < height - 1; y++)
        {
            int row = y * width;
            int rowUp = (y - 1) * width;
            int rowDown = (y + 1) * width;
            for (int x = 1; x < width - 1; x++)
            {
                int idx = row + x;
                float lumaM = Luma(copy[idx]);
                float lumaN = Luma(copy[rowUp + x]);
                float lumaS = Luma(copy[rowDown + x]);
                float lumaW = Luma(copy[idx - 1]);
                float lumaE = Luma(copy[idx + 1]);

                float lumaMin = MathF.Min(lumaM, MathF.Min(MathF.Min(lumaN, lumaS), MathF.Min(lumaW, lumaE)));
                float lumaMax = MathF.Max(lumaM, MathF.Max(MathF.Max(lumaN, lumaS), MathF.Max(lumaW, lumaE)));
                float range = lumaMax - lumaMin;

                if (range < MathF.Max(edgeThresholdMin, lumaMax * edgeThreshold))
                {
                    continue;
                }

                var avg = (copy[idx - 1] + copy[idx + 1] + copy[rowUp + x] + copy[rowDown + x]) * 0.25f;
                colors[idx] = Vector3.Lerp(copy[idx], avg, blend);
            }
        }
    }

    private static float Luma(Vector3 color)
    {
        return color.X * 0.299f + color.Y * 0.587f + color.Z * 0.114f;
    }
}
