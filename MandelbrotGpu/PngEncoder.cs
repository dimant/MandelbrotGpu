using System.IO.Compression;

namespace MandelbrotGpu;

/// <summary>
/// Minimal PNG encoder — no external image library needed.
/// </summary>
public static class PngEncoder
{
    public static byte[] Encode(double[] iterations, int width, int height, int maxIterations)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // PNG signature
        bw.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        // IHDR
        WriteChunk(bw, "IHDR", writer =>
        {
            writer.WriteBE(width);
            writer.WriteBE(height);
            writer.Write((byte)8); // bit depth
            writer.Write((byte)2); // color type: RGB
            writer.Write((byte)0); // compression
            writer.Write((byte)0); // filter
            writer.Write((byte)0); // interlace
        });

        // IDAT
        WriteChunk(bw, "IDAT", writer =>
        {
            using var compressed = new MemoryStream();
            using (var deflate = new DeflateStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                for (int y = 0; y < height; y++)
                {
                    deflate.WriteByte(0); // filter: none
                    for (int x = 0; x < width; x++)
                    {
                        double iter = iterations[y * width + x];
                        var (r, g, b) = Palette.Map(iter, maxIterations);
                        deflate.WriteByte(r);
                        deflate.WriteByte(g);
                        deflate.WriteByte(b);
                    }
                }
            }

            // zlib wrapper: header + deflate data + adler32
            var deflateData = compressed.ToArray();
            writer.Write((byte)0x78); // CMF
            writer.Write((byte)0x01); // FLG
            writer.Write(deflateData);

            // Compute Adler-32 over uncompressed data
            uint adler = ComputeAdler32(iterations, width, height, maxIterations);
            writer.WriteBE((int)adler);
        });

        // IEND
        WriteChunk(bw, "IEND", _ => { });

        return ms.ToArray();
    }

    private static uint ComputeAdler32(double[] iterations, int width, int height, int maxIterations)
    {
        uint a = 1, b = 0;
        for (int y = 0; y < height; y++)
        {
            // filter byte
            b = (b + a) % 65521;

            for (int x = 0; x < width; x++)
            {
                double iter = iterations[y * width + x];
                var (rv, gv, bv) = Palette.Map(iter, maxIterations);
                a = (a + rv) % 65521; b = (b + a) % 65521;
                a = (a + gv) % 65521; b = (b + a) % 65521;
                a = (a + bv) % 65521; b = (b + a) % 65521;
            }
        }
        return (b << 16) | a;
    }

    private static void WriteChunk(BinaryWriter bw, string type, Action<BinaryWriter> writeData)
    {
        using var dataMs = new MemoryStream();
        using var dataWriter = new BinaryWriter(dataMs);
        writeData(dataWriter);
        dataWriter.Flush();

        var data = dataMs.ToArray();
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);

        bw.WriteBE(data.Length);
        bw.Write(typeBytes);
        bw.Write(data);

        // CRC32 over type + data
        uint crc = Crc32(typeBytes, data);
        bw.WriteBE((int)crc);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in type) crc = CrcUpdate(crc, b);
        foreach (byte b in data) crc = CrcUpdate(crc, b);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint CrcUpdate(uint crc, byte b)
    {
        crc ^= b;
        for (int i = 0; i < 8; i++)
        {
            if ((crc & 1) != 0)
                crc = (crc >> 1) ^ 0xEDB88320;
            else
                crc >>= 1;
        }
        return crc;
    }

    public static void WriteBE(this BinaryWriter bw, int value)
    {
        bw.Write((byte)((value >> 24) & 0xFF));
        bw.Write((byte)((value >> 16) & 0xFF));
        bw.Write((byte)((value >> 8) & 0xFF));
        bw.Write((byte)(value & 0xFF));
    }
}
