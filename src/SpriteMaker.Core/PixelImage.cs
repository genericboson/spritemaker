namespace SpriteMaker.Core;

/// <summary>
/// 32비트 ARGB(0xAARRGGBB) 픽셀 버퍼. WPF Bgra32 와 메모리 배치가 같아 그대로 주고받을 수 있다.
/// </summary>
public sealed class PixelImage
{
    public int Width { get; }
    public int Height { get; }
    public uint[] Pixels { get; }

    public PixelImage(int width, int height)
        : this(width, height, new uint[checked(width * height)])
    {
    }

    public PixelImage(int width, int height, uint[] pixels)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "크기는 1 이상이어야 합니다.");
        if (pixels.Length != width * height)
            throw new ArgumentException("픽셀 수가 크기와 맞지 않습니다.", nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public uint this[int x, int y]
    {
        get => Pixels[y * Width + x];
        set => Pixels[y * Width + x] = value;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>범위 밖은 투명으로 취급해 읽는다.</summary>
    public uint GetOrClear(int x, int y) => InBounds(x, y) ? this[x, y] : 0u;

    public bool IsOpaqueAt(int x, int y) => InBounds(x, y) && ColorUtil.IsOpaque(this[x, y]);

    public PixelImage Clone() => new(Width, Height, (uint[])Pixels.Clone());

    /// <summary>지정 영역을 잘라낸다. 원본 밖의 영역은 투명으로 채운다.</summary>
    public PixelImage Crop(int x, int y, int width, int height)
    {
        var result = new PixelImage(Math.Max(1, width), Math.Max(1, height));
        for (int yy = 0; yy < result.Height; yy++)
            for (int xx = 0; xx < result.Width; xx++)
                result[xx, yy] = GetOrClear(x + xx, y + yy);
        return result;
    }

    /// <summary>불투명 픽셀만 덮어쓴다(도트 아트에는 반투명 합성이 필요 없다).</summary>
    public void Draw(PixelImage source, int dx, int dy)
    {
        for (int y = 0; y < source.Height; y++)
        {
            int ty = dy + y;
            if (ty < 0 || ty >= Height) continue;
            for (int x = 0; x < source.Width; x++)
            {
                int tx = dx + x;
                if (tx < 0 || tx >= Width) continue;
                uint c = source[x, y];
                if (ColorUtil.IsOpaque(c)) this[tx, ty] = c;
            }
        }
    }

    public PixelImage FlipHorizontal()
    {
        var result = new PixelImage(Width, Height);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                result[Width - 1 - x, y] = this[x, y];
        return result;
    }

    public PixelImage Map(Func<uint, uint> map)
    {
        var result = new PixelImage(Width, Height);
        for (int i = 0; i < Pixels.Length; i++)
            result.Pixels[i] = ColorUtil.IsOpaque(Pixels[i]) ? map(Pixels[i]) : 0u;
        return result;
    }

    /// <summary>불투명 픽셀을 감싸는 최소 사각형. 비어 있으면 null.</summary>
    public PixelRect? OpaqueBounds()
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (!ColorUtil.IsOpaque(this[x, y])) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        return maxX < 0 ? null : new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public int CountOpaque() => Pixels.Count(ColorUtil.IsOpaque);
}

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}
