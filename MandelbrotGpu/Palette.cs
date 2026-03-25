namespace MandelbrotGpu;

/// <summary>
/// Subdued cosine-gradient palette inspired by Inigo Quilez's technique.
/// Produces smooth dark-to-warm tones: deep navy → muted teal → dusty amber → soft ivory.
/// </summary>
public static class Palette
{
    public static (byte R, byte G, byte B) Map(double smoothIteration, int maxIterations)
    {
        if (smoothIteration <= 0.0)
            return (18, 12, 20); // near-black with a slight purple tint for the set interior

        // Use log-based remapping so colors spread nicely at all zoom levels
        double t = Math.Log(1.0 + smoothIteration) / Math.Log(1.0 + maxIterations);

        // Cosine palette: color(t) = a + b * cos(2π(c*t + d))
        // Tuned for subdued, earthy-cool tones
        double r = CosComponent(t, 0.30, 0.30, 1.0, 0.00);  // warm rise
        double g = CosComponent(t, 0.35, 0.25, 1.0, 0.10);  // teal mid-range
        double b = CosComponent(t, 0.45, 0.30, 1.0, 0.20);  // blue undertone

        // Darken the low end for depth
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
