namespace SpriteMaker.Core;

/// <summary>뷰 좌상단 기준 위치에 놓인 부위 조각.</summary>
public sealed record SpritePart(PixelImage Image, int X, int Y);

/// <summary>
/// 한 방향을 바라보는 캐릭터. 애니메이션은 부위를 조금씩 움직여 만든다.
/// 그리는 순서: LegFar → LegNear → Torso → Head.
/// </summary>
public sealed record PosedView(
    SpritePart Head,
    SpritePart Torso,
    SpritePart LegFar,
    SpritePart LegNear,
    int Width,
    int Height,
    int CenterX);

public sealed record CharacterViews(PosedView Front, PosedView Back, PosedView Side);

/// <summary>앞모습 한 장에서 뒷모습·옆모습(오른쪽)을 추정한다.</summary>
public static class ViewBuilder
{
    public static CharacterViews Build(PixelImage front, BodyLayout layout)
    {
        layout = layout.ClampTo(front);
        var backImage = MakeBackImage(front, layout);
        var frontView = Split(front, layout);
        var backView = Split(backImage, layout with { CenterX = front.Width - layout.CenterX });
        var sideView = MakeSideView(front, backImage.FlipHorizontal(), layout, frontView);
        return new CharacterViews(frontView, backView, sideView);
    }

    private static PosedView Split(PixelImage img, BodyLayout l)
    {
        int legH = img.Height - l.HipY;
        var head = new SpritePart(img.Crop(0, 0, img.Width, l.HeadBottom), 0, 0);
        var torso = new SpritePart(img.Crop(0, l.HeadBottom, img.Width, l.HipY - l.HeadBottom), 0, l.HeadBottom);
        var legLeft = new SpritePart(img.Crop(0, l.HipY, l.CenterX, legH), 0, l.HipY);
        var legRight = new SpritePart(img.Crop(l.CenterX, l.HipY, img.Width - l.CenterX, legH), l.CenterX, l.HipY);
        return new PosedView(head, torso, legLeft, legRight, img.Width, img.Height, l.CenterX);
    }

    /// <summary>머리 안쪽을 머리카락 색으로 덮고(얼굴을 가림) 좌우를 뒤집는다.</summary>
    private static PixelImage MakeBackImage(PixelImage front, BodyLayout l)
    {
        uint hair = DominantHairColor(front, l);
        var back = front.Clone();
        for (int y = 0; y < l.HeadBottom; y++)
            for (int x = 0; x < front.Width; x++)
                if (front.IsOpaqueAt(x, y) && !Outliner.IsEdge(front, x, y))
                    back[x, y] = hair;
        return back.FlipHorizontal();
    }

    /// <summary>머리 윗부분에서 가장 많이 쓰인(가장자리 제외) 색을 머리카락 색으로 본다.</summary>
    public static uint DominantHairColor(PixelImage front, BodyLayout l)
    {
        var bounds = front.OpaqueBounds() ?? new PixelRect(0, 0, front.Width, front.Height);
        int headTop = bounds.Y;
        int sampleBottom = headTop + Math.Max(1, (l.HeadBottom - headTop) * 2 / 5);

        uint? Most(int y0, int y1)
        {
            var counts = new Dictionary<uint, int>();
            for (int y = y0; y < y1; y++)
                for (int x = 0; x < front.Width; x++)
                    if (front.IsOpaqueAt(x, y) && !Outliner.IsEdge(front, x, y))
                        counts[front[x, y]] = counts.GetValueOrDefault(front[x, y]) + 1;
            return counts.Count == 0 ? null : counts.MaxBy(kv => kv.Value).Key;
        }

        return Most(headTop, sampleBottom) ?? Most(headTop, l.HeadBottom) ?? 0xFF3A2A20u;
    }

    /// <summary>
    /// 앞모습 얼굴의 오른쪽 절반에는 옆머리가 붙어 있어 옆모습에서는 얼굴 앞선(코 쪽)이 된다.
    /// 피부가 있는 행에서 피부보다 바깥쪽 머리카락을 피부색으로 바꿔 얼굴이 앞을 향하게 한다.
    /// </summary>
    private static PixelImage ExposeFaceFront(PixelImage face, uint hair)
    {
        var counts = new Dictionary<uint, int>();
        for (int y = 0; y < face.Height; y++)
            for (int x = 0; x < face.Width; x++)
            {
                uint c = face[x, y];
                if (!face.IsOpaqueAt(x, y) || Outliner.IsEdge(face, x, y) || c == hair || ColorUtil.Luminance(c) < 0.25) continue;
                counts[c] = counts.GetValueOrDefault(c) + 1;
            }
        if (counts.Count == 0) return face;
        uint skin = counts.MaxBy(kv => kv.Value).Key;

        var result = face.Clone();
        for (int y = 0; y < face.Height; y++)
        {
            int lastSkin = -1;
            for (int x = 0; x < face.Width; x++)
                if (face[x, y] == skin) lastSkin = x;
            if (lastSkin < 0) continue;
            for (int x = lastSkin + 1; x < face.Width; x++)
                if (face[x, y] == hair && !IsRightEdge(face, x, y)) result[x, y] = skin;
        }
        return result;
    }

    private static bool IsRightEdge(PixelImage img, int x, int y) => !img.IsOpaqueAt(x + 1, y);

    /// <summary>
    /// 오른쪽을 보는 옆모습: 머리는 [뒤통수 쪽 머리카락 | 앞모습 얼굴의 오른쪽 절반]을 이어 붙이고,
    /// 몸통은 가운데 조각, 다리는 한쪽 다리를 앞/뒤 두 벌로 쓴다.
    /// </summary>
    private static PosedView MakeSideView(PixelImage front, PixelImage backUnmirrored, BodyLayout l, PosedView frontView)
    {
        // --- 머리 ---
        var headRows = front.Crop(0, 0, front.Width, l.HeadBottom);
        var headBounds = headRows.OpaqueBounds() ?? new PixelRect(0, 0, front.Width, l.HeadBottom);
        int headW = headBounds.Width;
        int frontCols = (headW + 1) / 2;
        int sideHeadW = Math.Max(frontCols + 1, (int)Math.Round(headW * 0.85));
        int backCols = sideHeadW - frontCols;
        var head = new PixelImage(sideHeadW, l.HeadBottom);
        head.Draw(backUnmirrored.Crop(headBounds.X, 0, backCols, l.HeadBottom), 0, 0);
        var face = front.Crop(headBounds.Right - frontCols, 0, frontCols, l.HeadBottom);
        head.Draw(ExposeFaceFront(face, DominantHairColor(front, l)), backCols, 0);

        // --- 몸통 ---
        int torsoH = l.HipY - l.HeadBottom;
        var torsoRows = front.Crop(0, l.HeadBottom, front.Width, torsoH);
        var torsoBounds = torsoRows.OpaqueBounds() ?? new PixelRect(0, 0, front.Width, torsoH);
        int innerW = Math.Max(1, (int)Math.Round(torsoBounds.Width * 0.6) - 2);
        int torsoCenter = torsoBounds.X + torsoBounds.Width / 2;
        var torso = Outliner.AddSideOutline(torsoRows.Crop(torsoCenter - innerW / 2, 0, innerW, torsoH));

        // --- 다리 ---
        var nearSrc = frontView.LegNear.Image;
        var legBounds = nearSrc.OpaqueBounds() ?? new PixelRect(0, 0, nearSrc.Width, nearSrc.Height);
        var leg = nearSrc.Crop(legBounds.X, 0, legBounds.Width, nearSrc.Height);
        var legFar = leg.Map(c => ColorUtil.Scale(c, 0.72));

        int width = new[] { head.Width, torso.Width, leg.Width + 4 }.Max() + 2;
        int cx = width / 2;
        // 얼굴이 진행 방향으로 살짝 나와 보이도록 머리를 1px 앞으로
        int headX = cx - head.Width / 2 + (head.Width > 6 ? 1 : 0);
        return new PosedView(
            new SpritePart(head, headX, 0),
            new SpritePart(torso, cx - torso.Width / 2, l.HeadBottom),
            new SpritePart(legFar, cx - leg.Width / 2, l.HipY),
            new SpritePart(leg, cx - leg.Width / 2, l.HipY),
            width,
            front.Height,
            cx);
    }
}
