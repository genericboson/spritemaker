using SpriteMaker.Core;

namespace SpriteMaker.Tests;

/// <summary>512×512 캔버스에 사용자가 그렸을 법한 2등신 캐릭터 앞모습을 코드로 그린다.</summary>
internal static class SampleSketch
{
    public const uint Ink = 0xFF202020u;
    public const uint Skin = 0xFFF2C9A0u;
    public const uint Hair = 0xFF6B3E26u;
    public const uint Shirt = 0xFF3A6FD8u;
    public const uint Pants = 0xFF2E3550u;
    public const uint Shoe = 0xFF5A3A22u;

    public static PixelImage Create(bool lineArtOnly = false)
    {
        var img = new PixelImage(512, 512);

        // 다리(가운데 틈) — 몸통보다 먼저
        FillRect(img, 196, 380, 52, 90, Pants);
        FillRect(img, 264, 380, 52, 90, Pants);
        FillRect(img, 190, 455, 60, 22, Shoe);
        FillRect(img, 262, 455, 60, 22, Shoe);

        // 몸통과 팔
        FillRect(img, 176, 250, 160, 140, Shirt);
        FillRect(img, 146, 258, 30, 100, Shirt);
        FillRect(img, 336, 258, 30, 100, Shirt);
        FillRect(img, 146, 350, 30, 22, Skin);
        FillRect(img, 336, 350, 30, 22, Skin);

        // 목
        FillRect(img, 236, 228, 40, 26, Skin);

        // 머리(얼굴 + 머리카락)
        FillEllipse(img, 256, 140, 112, 100, Skin);
        FillEllipse(img, 256, 105, 116, 70, Hair, maxY: 125);
        FillRect(img, 145, 100, 26, 90, Hair);
        FillRect(img, 341, 100, 26, 90, Hair);

        // 눈
        FillRect(img, 205, 150, 18, 30, Ink);
        FillRect(img, 289, 150, 18, 30, Ink);

        if (lineArtOnly)
        {
            // 외곽만 남기고 속을 비운 선화 버전
            var outline = new PixelImage(512, 512);
            for (int y = 0; y < 512; y++)
                for (int x = 0; x < 512; x++)
                    if (Outliner.IsEdge(img, x, y))
                        for (int t = -2; t <= 2; t++)
                            if (outline.InBounds(x + t, y)) outline[x + t, y] = Ink;
            return outline;
        }

        return img;
    }

    private static void FillRect(PixelImage img, int x, int y, int w, int h, uint c)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                if (img.InBounds(xx, yy)) img[xx, yy] = c;
    }

    private static void FillEllipse(PixelImage img, int cx, int cy, int rx, int ry, uint c, int maxY = int.MaxValue)
    {
        for (int y = cy - ry; y <= Math.Min(cy + ry, maxY); y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                double dx = (x - cx) / (double)rx, dy = (y - cy) / (double)ry;
                if (dx * dx + dy * dy <= 1 && img.InBounds(x, y)) img[x, y] = c;
            }
    }
}
