# MandelbrotGpu

GPU-accelerated fractal explorer with a runtime formula editor. Built with C#, ILGPU, and ASP.NET.

## Features

- **GPU rendering** via ILGPU (CUDA → OpenCL → CPU fallback)
- **Smooth coloring** using normalized iteration counts
- **7 built-in fractals**: Mandelbrot, Burning Ship, Tricorn, Cubic, Quartic, Phoenix, Celtic
- **Runtime formula editor**: write custom iteration formulas compiled to GPU kernels on the fly via Roslyn
- **7 color palettes**: Midnight, Ember, Ocean, Amethyst, Verdant, Infrared, Grayscale
- **Animated zoom** to preset interesting coordinates
- **Rectangle zoom**: left-drag to select an area
- **Pan**: shift+drag or right-drag
- **PNG export** at up to 4K resolution

## Requirements

- .NET 10 SDK
- NVIDIA GPU with CUDA support (optional — falls back to OpenCL or CPU)

## Running

```bash
cd MandelbrotGpu
dotnet run
```

Then open http://localhost:5226

Or use the server script:

```bash
./server.sh start    # start in background
./server.sh stop     # stop
./server.sh restart  # restart
```

## Custom Formulas

Click **Formula Editor** in the toolbar. Available variables:

| Variable | Meaning |
|----------|---------|
| `x`, `y` | Current z (real, imaginary) |
| `x0`, `y0` | c point (real, imaginary) |
| `prevX`, `prevY` | Previous z (for Phoenix-type fractals) |

Set `xNew` and `yNew` for the next iteration. Math functions (`Sin`, `Cos`, `Log`, `Sqrt`, `Pow`, etc.) work without a prefix.

Example — Mandelbrot:
```
xNew = x*x - y*y + x0;
yNew = 2.0*x*y + y0;
```

Example — Burning Ship:
```
double ax = x < 0 ? -x : x;
double ay = y < 0 ? -y : y;
xNew = ax*ax - ay*ay + x0;
yNew = 2.0*ax*ay + y0;
```

Press **Ctrl+Enter** to compile and render.

## License

MIT
