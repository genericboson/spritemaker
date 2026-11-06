using System.IO.Compression;
using System.Text;

namespace SpriteMaker.Core;

/// <summary>외부 의존 없이 RGBA PNG 를 쓴다(Core 를 WPF 와 분리하기 위함).</summary>
public static class PngEncoder
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static void Save(PixelImage image, string path)
    {
        using var fs = File.Create(path);
        Write(image, fs);
    }

    public static byte[] Encode(PixelImage image)
    {
        using var ms = new MemoryStream();
        Write(image, ms);
        return ms.ToArray();
    }

    public static void Write(PixelImage image, Stream output)
    {
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, (uint)image.Width);
        WriteBigEndian(ihdr, 4, (uint)image.Height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // RGBA
        WriteChunk(output, "IHDR", ihdr);

        using (var raw = new MemoryStream())
        {
            using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
            {
                var row = new byte[1 + image.Width * 4];
                for (int y = 0; y < image.Height; y++)
                {
                    row[0] = 0; // filter: none
                    for (int x = 0; x < image.Width; x++)
                    {
                        uint c = image[x, y];
                        int o = 1 + x * 4;
                        row[o] = ColorUtil.R(c);
                        row[o + 1] = ColorUtil.G(c);
                        row[o + 2] = ColorUtil.B(c);
                        row[o + 3] = ColorUtil.A(c);
                    }
                    z.Write(row);
                }
            }
            WriteChunk(output, "IDAT", raw.ToArray());
        }

        WriteChunk(output, "IEND", []);
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBigEndian(len, 0, (uint)data.Length);
        output.Write(len);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        uint crc = 0xFFFFFFFFu;
        crc = UpdateCrc(crc, typeBytes);
        crc = UpdateCrc(crc, data);
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, crc ^ 0xFFFFFFFFu);
        output.Write(crcBytes);
    }

    private static uint UpdateCrc(uint crc, byte[] data)
    {
        foreach (var b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
