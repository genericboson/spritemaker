using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SpriteMaker.Core;

namespace SpriteMaker.App;

/// <summary>PixelImage(0xAARRGGBB) ↔ WPF Bgra32 비트맵. 메모리 배치가 같아 그대로 복사한다.</summary>
internal static class BitmapConvert
{
    public static WriteableBitmap ToBitmap(PixelImage image)
    {
        var bmp = new WriteableBitmap(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null);
        bmp.WritePixels(new Int32Rect(0, 0, image.Width, image.Height), image.Pixels, image.Width * 4, 0);
        return bmp;
    }

    public static void CopyRegion(PixelImage image, WriteableBitmap target, PixelRect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        target.WritePixels(new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height),
            image.Pixels, image.Width * 4, rect.X, rect.Y);
    }

    public static PixelImage Load(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource frame = decoder.Frames[0];
        if (frame.Format != PixelFormats.Bgra32)
            frame = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

        var image = new PixelImage(frame.PixelWidth, frame.PixelHeight);
        frame.CopyPixels(image.Pixels, image.Width * 4, 0);

        // 흰 배경으로 저장된 그림(스캔, 그림판 등)이면 배경을 투명으로 바꾼다
        if (!image.Pixels.Any(c => !ColorUtil.IsOpaque(c)))
            RemoveBackground(image);
        return image;
    }

    /// <summary>네 귀퉁이 색과 거의 같은 픽셀을 투명 처리한다.</summary>
    private static void RemoveBackground(PixelImage image)
    {
        uint bg = image[0, 0];
        for (int i = 0; i < image.Pixels.Length; i++)
            if (ColorUtil.DistanceSq(image.Pixels[i], bg) < 3 * 24 * 24)
                image.Pixels[i] = 0u;
    }
}
