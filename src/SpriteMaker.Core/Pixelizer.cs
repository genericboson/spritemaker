namespace SpriteMaker.Core;

public sealed record PixelizeOptions
{
    /// <summary>외곽선을 포함한 최종 스프라이트 높이(px).</summary>
    public int TargetHeight { get; init; } = 32;

    /// <summary>팔레트 최대 색 수(외곽선 색 제외).</summary>
    public int MaxColors { get; init; } = 12;

    /// <summary>실루엣 바깥에 1px 셀아웃 외곽선을 두른다.</summary>
    public bool AddOutline { get; init; } = true;

    /// <summary>닫힌 선 안쪽의 빈 영역을 채움색으로 칠한다(선만 그린 스케치용).</summary>
    public bool FillEnclosed { get; init; } = true;

    public uint EnclosedFillColor { get; init; } = 0xFFFFFFFFu;

    /// <summary>어두운 선(잉크) 픽셀의 가중치. 클수록 축소해도 선이 살아남는다.</summary>
    public double InkWeight { get; init; } = 2.5;

    /// <summary>칸 안에서 이 비율 이상 칠해져 있어야 픽셀로 남는다.</summary>
    public double CoverageThreshold { get; init; } = 0.35;
}

/// <summary>큰 스케치를 도트 아트 스프라이트로 축소한다.</summary>
public static class Pixelizer
{
    private const double InkLuminance = 0.25;

    public static PixelImage Pixelize(PixelImage sketch, PixelizeOptions options)
    {
        if (options.TargetHeight < 8)
            throw new ArgumentOutOfRangeException(nameof(options), "스프라이트 높이는 8px 이상이어야 합니다.");

        var source = options.FillEnclosed ? FillEnclosedRegions(sketch, options.EnclosedFillColor) : sketch;
        var bounds = source.OpaqueBounds() ?? throw new InvalidOperationException("스케치가 비어 있습니다.");

        int innerHeight = options.AddOutline ? options.TargetHeight - 2 : options.TargetHeight;
        double scale = Math.Max(1.0, (double)bounds.Height / innerHeight);
        int innerWidth = Math.Max(1, (int)Math.Round(bounds.Width / scale));
        innerHeight = Math.Max(1, (int)Math.Round(bounds.Height / scale));

        var small = new PixelImage(innerWidth, innerHeight);
        for (int oy = 0; oy < innerHeight; oy++)
        {
            int sy0 = bounds.Y + (int)Math.Floor(oy * scale);
            int sy1 = Math.Max(sy0 + 1, bounds.Y + (int)Math.Floor((oy + 1) * scale));
            for (int ox = 0; ox < innerWidth; ox++)
            {
                int sx0 = bounds.X + (int)Math.Floor(ox * scale);
                int sx1 = Math.Max(sx0 + 1, bounds.X + (int)Math.Floor((ox + 1) * scale));
                small[ox, oy] = SampleCell(source, sx0, sy0, sx1, sy1, options);
            }
        }

        var quantized = Palette.Quantize(small, options.MaxColors);
        return options.AddOutline ? Outliner.AddOutline(quantized) : quantized;
    }

    /// <summary>칸 안의 대표색: 비슷한 색끼리 묶어 가장 무거운 묶음의 평균색(잉크는 가중).</summary>
    private static uint SampleCell(PixelImage src, int x0, int y0, int x1, int y1, PixelizeOptions o)
    {
        int total = 0;
        double opaqueWeight = 0;
        var buckets = new Dictionary<int, (double W, double R, double G, double B)>();
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                total++;
                uint c = src.GetOrClear(x, y);
                if (!ColorUtil.IsOpaque(c)) continue;
                double w = ColorUtil.Luminance(c) < InkLuminance ? o.InkWeight : 1.0;
                opaqueWeight += w;
                int key = (ColorUtil.R(c) >> 4 << 8) | (ColorUtil.G(c) >> 4 << 4) | (ColorUtil.B(c) >> 4);
                buckets.TryGetValue(key, out var acc);
                buckets[key] = (acc.W + w, acc.R + ColorUtil.R(c) * w, acc.G + ColorUtil.G(c) * w, acc.B + ColorUtil.B(c) * w);
            }

        if (total == 0 || opaqueWeight / total < o.CoverageThreshold)
            return ColorUtil.Transparent;

        var best = buckets.Values.MaxBy(b => b.W);
        return ColorUtil.Make((int)Math.Round(best.R / best.W), (int)Math.Round(best.G / best.W), (int)Math.Round(best.B / best.W));
    }

    /// <summary>
    /// 그림 바깥(테두리)에서 4방향으로 닿지 않는 투명 영역을 채운다.
    /// 선만 그려도 닫혀 있으면 속이 찬 실루엣이 된다.
    /// </summary>
    public static PixelImage FillEnclosedRegions(PixelImage image, uint fillColor)
    {
        int w = image.Width, h = image.Height;
        var outside = new bool[w * h];
        var queue = new Queue<int>();

        void Seed(int x, int y)
        {
            int i = y * w + x;
            if (outside[i] || ColorUtil.IsOpaque(image.Pixels[i])) return;
            outside[i] = true;
            queue.Enqueue(i);
        }

        for (int x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
        for (int y = 0; y < h; y++) { Seed(0, y); Seed(w - 1, y); }

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % w, y = i / w;
            if (x > 0) Seed(x - 1, y);
            if (x < w - 1) Seed(x + 1, y);
            if (y > 0) Seed(x, y - 1);
            if (y < h - 1) Seed(x, y + 1);
        }

        var result = image.Clone();
        for (int i = 0; i < result.Pixels.Length; i++)
            if (!outside[i] && !ColorUtil.IsOpaque(result.Pixels[i]))
                result.Pixels[i] = fillColor;
        return result;
    }
}
