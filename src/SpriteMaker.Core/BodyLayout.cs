namespace SpriteMaker.Core;

/// <summary>
/// 앞모습 스프라이트를 머리·몸통·다리로 나누는 기준.
/// HeadBottom: 몸통이 시작하는 행, HipY: 다리가 시작하는 행, CenterX: 왼다리/오른다리를 가르는 열.
/// </summary>
public sealed record BodyLayout(int HeadBottom, int HipY, int CenterX)
{
    public BodyLayout ClampTo(PixelImage sprite)
    {
        int head = Math.Clamp(HeadBottom, 1, sprite.Height - 2);
        int hip = Math.Clamp(HipY, head + 1, sprite.Height - 1);
        int center = Math.Clamp(CenterX, 1, sprite.Width - 1);
        return new BodyLayout(head, hip, center);
    }
}

public static class BodyAnalyzer
{
    /// <summary>목(가장 좁은 행)과 가랑이(다리 사이 빈틈)를 찾아 부위 경계를 추정한다.</summary>
    public static BodyLayout Detect(PixelImage sprite)
    {
        var bounds = sprite.OpaqueBounds() ?? new PixelRect(0, 0, sprite.Width, sprite.Height);
        int top = bounds.Y, height = bounds.Height;

        int headBottom = DetectNeck(sprite, bounds) ?? top + (int)Math.Round(height * 0.45);
        var (hip, center) = DetectCrotch(sprite, bounds);
        int hipY = hip ?? top + (int)Math.Round(height * 0.78);
        int centerX = center ?? bounds.X + bounds.Width / 2;

        if (hipY <= headBottom + 1) hipY = Math.Min(bounds.Bottom - 1, headBottom + Math.Max(2, height / 4));
        return new BodyLayout(headBottom, hipY, centerX).ClampTo(sprite);
    }

    private static (int Left, int Right, int Count) RowExtent(PixelImage s, int y)
    {
        int left = -1, right = -1, count = 0;
        for (int x = 0; x < s.Width; x++)
        {
            if (!s.IsOpaqueAt(x, y)) continue;
            if (left < 0) left = x;
            right = x;
            count++;
        }
        return (left, right, count);
    }

    private static int? DetectNeck(PixelImage s, PixelRect b)
    {
        int from = b.Y + (int)(b.Height * 0.25);
        int to = b.Y + (int)(b.Height * 0.62);
        int maxHeadWidth = 0;
        for (int y = b.Y; y < from; y++)
        {
            var (l, r, _) = RowExtent(s, y);
            if (l >= 0) maxHeadWidth = Math.Max(maxHeadWidth, r - l + 1);
        }

        int bestY = -1, bestWidth = int.MaxValue;
        for (int y = from; y <= to; y++)
        {
            var (l, r, _) = RowExtent(s, y);
            if (l < 0) continue;
            int width = r - l + 1;
            if (width <= bestWidth) { bestWidth = width; bestY = y; }
        }

        // 뚜렷하게 잘록하지 않으면 추정 실패로 본다
        if (bestY < 0 || bestWidth > maxHeadWidth * 0.85) return null;

        // 잘록한 구간의 마지막 행 다음부터 몸통
        int y2 = bestY;
        while (y2 + 1 <= to)
        {
            var (l, r, _) = RowExtent(s, y2 + 1);
            if (l < 0 || r - l + 1 > bestWidth) break;
            y2++;
        }
        return y2 + 1;
    }

    /// <summary>아래에서 위로 올라가며 가운데에 빈틈이 있는 행(다리 사이)을 찾는다.</summary>
    private static (int? Hip, int? Center) DetectCrotch(PixelImage s, PixelRect b)
    {
        int? hip = null;
        int? center = null;
        int minY = b.Y + b.Height / 2;
        for (int y = b.Bottom - 1; y >= minY; y--)
        {
            var gap = FindCentralGap(s, y, b);
            if (gap is null)
            {
                if (hip is not null) break;
                continue; // 발끝이 붙어 있을 수도 있으니 맨 아래 몇 줄은 건너뛴다
            }
            hip = y;
            center ??= gap.Value;
        }
        if (hip is null) return (null, null);
        // 빈틈이 끝난 바로 윗줄(가랑이)부터 다리로 친다
        return (Math.Max(b.Y + 1, hip.Value - 1), center);
    }

    private static int? FindCentralGap(PixelImage s, int y, PixelRect b)
    {
        var (left, right, _) = RowExtent(s, y);
        if (left < 0) return null;
        int mid = b.X + b.Width / 2;
        int window = Math.Max(1, b.Width / 4);
        for (int d = 0; d <= window; d++)
        {
            foreach (int x in new[] { mid - d, mid + d })
            {
                if (x <= left || x >= right) continue;
                if (!s.IsOpaqueAt(x, y)) return x;
            }
        }
        return null;
    }
}
