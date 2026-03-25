namespace MandelbrotGpu;

public static class Palette
{
    // Each palette is defined by cosine gradient parameters:
    // color(t) = a + b * cos(2π(c*t + d))
    // Format: (a_r, b_r, c_r, d_r, a_g, b_g, c_g, d_g, a_b, b_b, c_b, d_b)
    private static readonly (double ar, double br, double cr, double dr,
                             double ag, double bg, double cg, double dg,
                             double ab, double bb, double cb, double db)[] Palettes =
    {
        // 0: Midnight — deep navy → muted teal → dusty amber → soft ivory
        (0.30, 0.30, 1.0, 0.00,   0.35, 0.25, 1.0, 0.10,   0.45, 0.30, 1.0, 0.20),
        // 1: Ember — black → deep red → orange → pale gold
        (0.20, 0.40, 1.0, 0.00,   0.10, 0.30, 1.0, 0.15,   0.05, 0.15, 1.0, 0.35),
        // 2: Ocean — deep blue → cyan → seafoam → white
        (0.15, 0.25, 1.0, 0.55,   0.30, 0.35, 1.0, 0.20,   0.45, 0.35, 1.0, 0.00),
        // 3: Amethyst — dark purple → magenta → lavender → pale rose
        (0.35, 0.35, 1.0, 0.10,   0.15, 0.20, 1.0, 0.40,   0.45, 0.30, 1.0, 0.00),
        // 4: Verdant — near-black → forest green → gold → cream
        (0.15, 0.30, 1.0, 0.20,   0.30, 0.35, 1.0, 0.00,   0.10, 0.20, 1.0, 0.40),
        // 5: Infrared — black → deep magenta → hot pink → white
        (0.40, 0.40, 1.0, 0.00,   0.05, 0.15, 1.0, 0.30,   0.30, 0.30, 1.0, 0.15),
        // 6: Grayscale
        (0.40, 0.40, 1.0, 0.00,   0.40, 0.40, 1.0, 0.00,   0.40, 0.40, 1.0, 0.00),
    };

    public static int PaletteCount => Palettes.Length;

    public static (byte R, byte G, byte B) Map(double smoothIteration, int maxIterations, int paletteIndex = 0)
    {
        if (smoothIteration <= 0.0)
            return (18, 12, 20);

        double t = Math.Log(1.0 + smoothIteration) / Math.Log(1.0 + maxIterations);

        var p = Palettes[Math.Clamp(paletteIndex, 0, Palettes.Length - 1)];

        double r = CosComponent(t, p.ar, p.br, p.cr, p.dr);
        double g = CosComponent(t, p.ag, p.bg, p.cg, p.dg);
        double b = CosComponent(t, p.ab, p.bb, p.cb, p.db);

        double fade = Math.Sqrt(t);
        r *= fade;
        g *= fade;
        b *= fade;

        return (ToByte(r), ToByte(g), ToByte(b));
    }

    private static double CosComponent(double t, double a, double b, double c, double d)
    {
        return a + b * Math.Cos(2.0 * Math.PI * (c * t + d));
    }

    private static byte ToByte(double v)
    {
        int i = (int)(v * 255.0 + 0.5);
        return (byte)(i < 0 ? 0 : i > 255 ? 255 : i);
    }
}
