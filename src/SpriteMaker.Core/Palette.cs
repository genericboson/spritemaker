namespace SpriteMaker.Core;

/// <summary>미디언 컷으로 색 수를 줄인다.</summary>
public static class Palette
{
    public static PixelImage Quantize(PixelImage image, int maxColors)
    {
        var colors = image.Pixels.Where(ColorUtil.IsOpaque).ToList();
        if (colors.Count == 0) return image.Clone();

        var palette = BuildPalette(colors, Math.Max(1, maxColors));
        var cache = new Dictionary<uint, uint>();
        return image.Map(c =>
        {
            uint key = c | 0xFF000000u;
            if (!cache.TryGetValue(key, out var mapped))
            {
                mapped = palette.MinBy(p => ColorUtil.DistanceSq(p, key));
                cache[key] = mapped;
            }
            return mapped;
        });
    }

    public static List<uint> BuildPalette(IReadOnlyList<uint> colors, int maxColors)
    {
        var distinct = colors.Select(c => c | 0xFF000000u).Distinct().ToList();
        if (distinct.Count <= maxColors) return distinct;

        var boxes = new List<List<uint>> { colors.Select(c => c | 0xFF000000u).ToList() };
        while (boxes.Count < maxColors)
        {
            // 가장 넓게 퍼진 상자를 가장 긴 축에서 반으로 자른다
            var (box, channel, range) = boxes
                .Select(b => { var (ch, r) = WidestChannel(b); return (b, ch, r); })
                .MaxBy(t => t.r);
            if (range == 0) break;

            boxes.Remove(box);
            box.Sort((a, b) => Channel(a, channel).CompareTo(Channel(b, channel)));
            int mid = box.Count / 2;
            boxes.Add(box.GetRange(0, mid));
            boxes.Add(box.GetRange(mid, box.Count - mid));
        }

        return boxes.Where(b => b.Count > 0).Select(Average).Distinct().ToList();
    }

    private static (int Channel, int Range) WidestChannel(List<uint> box)
    {
        int best = 0, bestRange = -1;
        for (int ch = 0; ch < 3; ch++)
        {
            int min = 255, max = 0;
            foreach (var c in box)
            {
                int v = Channel(c, ch);
                if (v < min) min = v;
                if (v > max) max = v;
            }
            if (max - min > bestRange) { bestRange = max - min; best = ch; }
        }
        return (best, Math.Max(0, bestRange));
    }

    private static int Channel(uint c, int ch) => ch switch
    {
        0 => ColorUtil.R(c),
        1 => ColorUtil.G(c),
        _ => ColorUtil.B(c),
    };

    private static uint Average(List<uint> box)
    {
        long r = 0, g = 0, b = 0;
        foreach (var c in box) { r += ColorUtil.R(c); g += ColorUtil.G(c); b += ColorUtil.B(c); }
        return ColorUtil.Make((int)(r / box.Count), (int)(g / box.Count), (int)(b / box.Count));
    }
}
