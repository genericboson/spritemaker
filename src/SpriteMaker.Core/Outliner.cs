namespace SpriteMaker.Core;

public static class Outliner
{
    private static readonly (int Dx, int Dy)[] Neighbors4 = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    /// <summary>1px 여백을 두고, 실루엣에 맞닿은 투명 픽셀을 이웃색 기반 외곽선으로 칠한다.</summary>
    public static PixelImage AddOutline(PixelImage image)
    {
        var padded = new PixelImage(image.Width + 2, image.Height + 2);
        padded.Draw(image, 1, 1);
        var result = padded.Clone();
        for (int y = 0; y < padded.Height; y++)
            for (int x = 0; x < padded.Width; x++)
            {
                if (padded.IsOpaqueAt(x, y)) continue;
                foreach (var (dx, dy) in Neighbors4)
                {
                    if (!padded.IsOpaqueAt(x + dx, y + dy)) continue;
                    result[x, y] = ColorUtil.OutlineOf(padded[x + dx, y + dy]);
                    break;
                }
            }
        return result;
    }

    /// <summary>좌우로만 외곽선을 덧댄다(옆모습용으로 잘라낸 조각의 끊긴 옆면 보정).</summary>
    public static PixelImage AddSideOutline(PixelImage image)
    {
        var padded = new PixelImage(image.Width + 2, image.Height);
        padded.Draw(image, 1, 0);
        var result = padded.Clone();
        for (int y = 0; y < padded.Height; y++)
            for (int x = 0; x < padded.Width; x++)
            {
                if (padded.IsOpaqueAt(x, y)) continue;
                if (padded.IsOpaqueAt(x - 1, y)) result[x, y] = ColorUtil.OutlineOf(padded[x - 1, y]);
                else if (padded.IsOpaqueAt(x + 1, y)) result[x, y] = ColorUtil.OutlineOf(padded[x + 1, y]);
            }
        return result;
    }

    /// <summary>실루엣의 가장자리(투명과 4방향으로 맞닿은) 픽셀인지.</summary>
    public static bool IsEdge(PixelImage image, int x, int y)
    {
        if (!image.IsOpaqueAt(x, y)) return false;
        foreach (var (dx, dy) in Neighbors4)
            if (!image.IsOpaqueAt(x + dx, y + dy)) return true;
        return false;
    }
}
