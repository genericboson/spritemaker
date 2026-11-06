namespace SpriteMaker.Core;

public static class ColorUtil
{
    public const uint Transparent = 0u;

    public static byte A(uint c) => (byte)(c >> 24);
    public static byte R(uint c) => (byte)(c >> 16);
    public static byte G(uint c) => (byte)(c >> 8);
    public static byte B(uint c) => (byte)c;

    public static uint Make(int r, int g, int b, int a = 255) =>
        ((uint)Math.Clamp(a, 0, 255) << 24) |
        ((uint)Math.Clamp(r, 0, 255) << 16) |
        ((uint)Math.Clamp(g, 0, 255) << 8) |
        (uint)Math.Clamp(b, 0, 255);

    public static bool IsOpaque(uint c) => A(c) >= 128;

    /// <summary>0~1 상대 휘도(sRGB 가중치 근사).</summary>
    public static double Luminance(uint c) => (0.299 * R(c) + 0.587 * G(c) + 0.114 * B(c)) / 255.0;

    /// <summary>RGB 에 배율을 곱한다. 1 보다 작으면 어두워진다.</summary>
    public static uint Scale(uint c, double factor) =>
        Make((int)Math.Round(R(c) * factor), (int)Math.Round(G(c) * factor), (int)Math.Round(B(c) * factor), A(c));

    /// <summary>외곽선 색: 이웃 색을 어둡고 약간 푸르게(셀아웃 기법).</summary>
    public static uint OutlineOf(uint neighbor)
    {
        uint dark = Scale(neighbor, 0.35);
        return Make(R(dark), G(dark), B(dark) + 18);
    }

    public static int DistanceSq(uint a, uint b)
    {
        int dr = R(a) - R(b), dg = G(a) - G(b), db = B(a) - B(b);
        // 사람 눈의 민감도를 반영한 가중치
        return 3 * dr * dr + 4 * dg * dg + 2 * db * db;
    }

    public static string ToHex(uint c) => $"#{R(c):X2}{G(c):X2}{B(c):X2}";

    public static bool TryParseHex(string text, out uint color)
    {
        color = 0;
        var s = text.Trim().TrimStart('#');
        if (s.Length != 6 || !uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return false;
        color = 0xFF000000u | rgb;
        return true;
    }
}
