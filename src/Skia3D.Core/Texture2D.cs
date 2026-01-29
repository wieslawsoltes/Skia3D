using System;
using System.Numerics;
using SkiaSharp;

namespace Skia3D.Core;

public enum TextureWrap
{
    Clamp,
    Repeat,
    Mirror
}

public enum TextureFilter
{
    Nearest,
    Bilinear
}

public sealed class TextureSampler
{
    public TextureWrap WrapU { get; set; } = TextureWrap.Repeat;

    public TextureWrap WrapV { get; set; } = TextureWrap.Repeat;

    public TextureFilter Filter { get; set; } = TextureFilter.Nearest;
}

public sealed class Texture2D : IDisposable
{
    private readonly SKBitmap _bitmap;
    private readonly SKColor[] _pixels;

    public Texture2D(SKBitmap bitmap)
    {
        _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        if (_bitmap.Width <= 0 || _bitmap.Height <= 0)
        {
            throw new ArgumentException("Texture bitmap must have valid dimensions.", nameof(bitmap));
        }

        _pixels = _bitmap.Pixels;
        if (_pixels.Length != _bitmap.Width * _bitmap.Height)
        {
            _pixels = new SKColor[_bitmap.Width * _bitmap.Height];
            _bitmap.Pixels = _pixels;
        }
    }

    public int Width => _bitmap.Width;

    public int Height => _bitmap.Height;

    public static Texture2D FromFile(string path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        var bitmap = SKBitmap.Decode(path) ?? throw new InvalidOperationException("Failed to decode texture.");
        return new Texture2D(bitmap);
    }

    public static Texture2D CreateCheckerboard(int width, int height, SKColor a, SKColor b, int cells = 8)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        int cellW = Math.Max(1, width / cells);
        int cellH = Math.Max(1, height / cells);
        for (int y = 0; y < height; y++)
        {
            int cy = (y / cellH) % 2;
            for (int x = 0; x < width; x++)
            {
                int cx = (x / cellW) % 2;
                bitmap.SetPixel(x, y, (cx ^ cy) == 0 ? a : b);
            }
        }

        return new Texture2D(bitmap);
    }

    public static Texture2D FromPixels(int width, int height, ReadOnlySpan<SKColor> pixels)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be positive.");
        }

        if (pixels.Length != width * height)
        {
            throw new ArgumentException("Pixel count must match texture dimensions.", nameof(pixels));
        }

        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var target = bitmap.Pixels;
        for (int i = 0; i < pixels.Length; i++)
        {
            target[i] = pixels[i];
        }
        bitmap.Pixels = target;
        return new Texture2D(bitmap);
    }

    public SKColor Sample(Vector2 uv, TextureSampler? sampler = null)
    {
        if (_pixels.Length == 0)
        {
            return SKColors.White;
        }

        var wrapU = sampler?.WrapU ?? TextureWrap.Repeat;
        var wrapV = sampler?.WrapV ?? TextureWrap.Repeat;
        var filter = sampler?.Filter ?? TextureFilter.Nearest;

        float u = ApplyWrap(uv.X, wrapU);
        float v = ApplyWrap(uv.Y, wrapV);

        v = 1f - v;
        if (filter == TextureFilter.Bilinear)
        {
            return SampleBilinear(u, v);
        }

        int x = (int)MathF.Round(u * (Width - 1));
        int y = (int)MathF.Round(v * (Height - 1));
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);
        return _pixels[y * Width + x];
    }

    public void Dispose()
    {
        _bitmap.Dispose();
    }

    public SKColor[] GetPixels()
    {
        var copy = new SKColor[_pixels.Length];
        Array.Copy(_pixels, copy, _pixels.Length);
        return copy;
    }

    private static float ApplyWrap(float value, TextureWrap wrap)
    {
        switch (wrap)
        {
            case TextureWrap.Repeat:
                value -= MathF.Floor(value);
                if (value < 0f)
                {
                    value += 1f;
                }
                return value;
            case TextureWrap.Mirror:
                value = value % 2f;
                if (value < 0f)
                {
                    value += 2f;
                }
                return value > 1f ? 2f - value : value;
            default:
                return Math.Clamp(value, 0f, 1f);
        }
    }

    private SKColor SampleBilinear(float u, float v)
    {
        float x = u * (Width - 1);
        float y = v * (Height - 1);
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        int x1 = Math.Min(x0 + 1, Width - 1);
        int y1 = Math.Min(y0 + 1, Height - 1);
        float tx = x - x0;
        float ty = y - y0;

        var c00 = _pixels[y0 * Width + x0];
        var c10 = _pixels[y0 * Width + x1];
        var c01 = _pixels[y1 * Width + x0];
        var c11 = _pixels[y1 * Width + x1];

        var cx0 = LerpColor(c00, c10, tx);
        var cx1 = LerpColor(c01, c11, tx);
        return LerpColor(cx0, cx1, ty);
    }

    private static SKColor LerpColor(SKColor a, SKColor b, float t)
    {
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t),
            (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }
}
