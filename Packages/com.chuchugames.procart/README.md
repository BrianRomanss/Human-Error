# Chuchu Games – Procedural Art

Engine-free placeholder asset generation, so a project has consistent art and sound from day one. Final assets replace the generated files 1:1 by keeping the same file names.

- **`Raster`**: an RGBA image you draw on with signed-distance shapes. Everything is anti-aliased, and it supports `Fill`, `Stroke`, `Glow`, `Mask` and `Draw` (compositing), plus vertical and radial gradients and `Downscale`.
- **`Shapes`**: SDF primitives (`Circle`, `Ellipse`, `Box`/`Rect` with rounded corners, `Capsule`, `Arc`, `Polygon`, `Star`) and operations (`Union`, `SmoothUnion`, `Subtract`, `Intersect`, `Ring`, `Grow`, `Move`, `Scale`, `Rotate`).
- **`Png.Write`**: a PNG encoder that uses only `System.IO.Compression`.
- **`Synth` + `Wav.Write`**: oscillators with pitch glide, a bell, seeded noise, envelopes, a one-pole low-pass, mix/offset/normalize and declick. Enough for UI blips, chimes, whooshes and music-box loops.

Example:
```csharp
var r = new Raster(128, 128);
r.Glow(Shapes.Circle(64, 64, 40), Rgba.Hex("#FFD58A", 0.5f), 20);
r.Fill(Shapes.Star(64, 66, 40, 18), Rgba.Hex("#F2A541"));
Png.Write("star.png", r);
Wav.Write("chime.wav", Synth.Bell(0.6f, 1046.5f));
```
