using System;
using System.IO;
using NUnit.Framework;

namespace ChuchuGames.ProcArt.Tests
{
    public class ShapeTests
    {
        [Test]
        public void Circle_SignedDistance()
        {
            var c = Shapes.Circle(10, 10, 5);
            Assert.AreEqual(-5f, c(10, 10), 1e-4);
            Assert.AreEqual(0f, c(15, 10), 1e-4);
            Assert.AreEqual(5f, c(20, 10), 1e-4);
        }

        [Test]
        public void Box_And_Subtract()
        {
            var box = Shapes.Box(0, 0, 10, 10);
            var hole = Shapes.Circle(0, 0, 4);
            var ring = Shapes.Subtract(box, hole);
            Assert.Greater(ring(0, 0), 0f, "centre is cut out");
            Assert.Less(ring(7, 7), 0f, "corner is still filled");
        }

        [Test]
        public void Polygon_InsideOutside()
        {
            var tri = Shapes.Polygon((0, 0), (10, 0), (0, 10));
            Assert.Less(tri(2, 2), 0f);
            Assert.Greater(tri(9, 9), 0f);
        }

        [Test]
        public void Fill_AntiAliasesEdges()
        {
            var r = new Raster(20, 20);
            r.Fill(Shapes.Circle(10, 10, 6), Rgba.White);
            Assert.AreEqual(1f, r.Get(10, 10).A, 1e-4);
            Assert.AreEqual(0f, r.Get(0, 0).A, 1e-4);
            float edge = r.Get(15, 10).A; // pixel centre 15.5 is ~0.5 px outside → partial
            Assert.That(edge, Is.GreaterThan(0f).And.LessThan(1f));
        }
    }

    public class NoiseTests
    {
        [Test]
        public void Deterministic_InRange_AndSeedMatters()
        {
            float a = Noise.Fbm(12.3f, 45.6f, 20f, 4, 7);
            Assert.AreEqual(a, Noise.Fbm(12.3f, 45.6f, 20f, 4, 7));
            Assert.That(a, Is.InRange(0f, 1f));
            Assert.AreNotEqual(a, Noise.Fbm(12.3f, 45.6f, 20f, 4, 8));
        }

        [Test]
        public void Displace_KeepsDeepInsideInside()
        {
            var wobbly = Shapes.Displace(Shapes.Circle(50, 50, 30), 4f, 10f);
            Assert.Less(wobbly(50, 50), 0f);
            Assert.Greater(wobbly(0, 0), 0f);
        }
    }

    public class EncoderTests
    {
        [Test]
        public void Png_HasSignatureAndChunks()
        {
            var r = new Raster(4, 3);
            r.Clear(Rgba.Hex("#F2A541"));
            var bytes = Png.Encode(r);
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, new ArraySegment<byte>(bytes, 0, 8));
            var text = System.Text.Encoding.ASCII.GetString(bytes);
            StringAssert.Contains("IHDR", text);
            StringAssert.Contains("IDAT", text);
            Assert.AreEqual("IEND", text.Substring(text.Length - 8, 4), "IEND is the last chunk (before its CRC)");
        }

        [Test]
        public void Wav_HeaderAndLength()
        {
            var path = Path.Combine(Path.GetTempPath(), $"procart-{Guid.NewGuid():N}.wav");
            try
            {
                var tone = Synth.Tone(0.1f, 440);
                Wav.Write(path, tone);
                var bytes = File.ReadAllBytes(path);
                Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
                Assert.AreEqual(44 + tone.Length * 2, bytes.Length);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Synth_Normalize_And_At()
        {
            var a = Synth.Normalize(Synth.Tone(0.05f, 220, amp: 0.1f), 0.5f);
            float max = 0;
            foreach (var s in a) max = Math.Max(max, Math.Abs(s));
            Assert.AreEqual(0.5f, max, 1e-3);
            var b = Synth.At(Synth.Silence(0.01f), a, 0.1f);
            Assert.AreEqual((int)(0.1f * Synth.SampleRate) + a.Length, b.Length);
        }
    }
}
