using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ChuchuGames.ProcArt
{
    /// <summary>Minimal PNG writer (8-bit RGBA, no filtering) using only System.IO.Compression.</summary>
    public static class Png
    {
        public static void Write(string path, Raster raster)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, Encode(raster));
        }

        public static byte[] Encode(Raster raster)
        {
            int w = raster.Width, h = raster.Height;
            var rgba = raster.ToBytes();
            var raw = new byte[(w * 4 + 1) * h];
            for (int y = 0; y < h; y++)
            {
                raw[y * (w * 4 + 1)] = 0; // filter: none
                Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
            }

            using (var ms = new MemoryStream())
            {
                ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                var ihdr = new byte[13];
                WriteBE(ihdr, 0, (uint)w);
                WriteBE(ihdr, 4, (uint)h);
                ihdr[8] = 8;  // bit depth
                ihdr[9] = 6;  // colour type RGBA
                Chunk(ms, "IHDR", ihdr);
                Chunk(ms, "IDAT", Zlib(raw));
                Chunk(ms, "IEND", new byte[0]);
                return ms.ToArray();
            }
        }

        static byte[] Zlib(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78); ms.WriteByte(0x9C); // zlib header, default compression
                using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    deflate.Write(data, 0, data.Length);
                uint a = 1, b = 0; // Adler-32
                foreach (var d in data)
                {
                    a = (a + d) % 65521;
                    b = (b + a) % 65521;
                }
                var adler = new byte[4];
                WriteBE(adler, 0, (b << 16) | a);
                ms.Write(adler, 0, 4);
                return ms.ToArray();
            }
        }

        static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteBE(len, 0, (uint)data.Length);
            s.Write(len, 0, 4);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = Crc(typeBytes, 0xFFFFFFFFu);
            crc = Crc(data, crc) ^ 0xFFFFFFFFu;
            var c = new byte[4];
            WriteBE(c, 0, crc);
            s.Write(c, 0, 4);
        }

        static uint[] _crcTable;

        static uint Crc(byte[] data, uint crc)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    _crcTable[n] = c;
                }
            }
            foreach (var d in data) crc = _crcTable[(crc ^ d) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        static void WriteBE(byte[] b, int o, uint v)
        {
            b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
        }
    }

    /// <summary>16-bit mono PCM WAV writer.</summary>
    public static class Wav
    {
        public static void Write(string path, float[] samples, int sampleRate = Synth.SampleRate)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (var fs = File.Create(path))
            using (var w = new BinaryWriter(fs))
            {
                int dataBytes = samples.Length * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(sampleRate); w.Write(sampleRate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(dataBytes);
                foreach (var s in samples)
                {
                    float c = s < -1 ? -1 : s > 1 ? 1 : s;
                    w.Write((short)(c * 32767f));
                }
            }
        }
    }
}
