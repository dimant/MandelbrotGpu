using ILGPU;
using ILGPU.Runtime;

namespace MandelbrotGpu;

// fractalType values:
// 0 = Mandelbrot        z = z² + c
// 1 = Burning Ship      z = (|Re|+i|Im|)² + c
// 2 = Tricorn           z = conj(z)² + c
// 3 = Cubic Mandelbrot  z = z³ + c
// 4 = Quartic Mandelbrot z = z⁴ + c
// 5 = Phoenix           z(n+1) = z(n)² + Re(c) + Im(c)*z(n-1)
// 6 = Celtic            like Mandelbrot but |Re(z²)|

public static class MandelbrotKernel
{
    // Escape radius squared — must be > 4; higher values give smoother coloring
    private const double BailoutSq = 256.0;

    public static void ComputeFractal(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output,
        ArrayView1D<double, Stride1D.Dense> magnitudes,
        int width,
        int height,
        double centerX,
        double centerY,
        double scale,
        int maxIterations,
        int fractalType)
    {
        int px = index % width;
        int py = index / width;

        double x0 = centerX + (px - width / 2.0) * scale;
        double y0 = centerY + (py - height / 2.0) * scale;

        double x = 0.0;
        double y = 0.0;
        double prevX = 0.0; // for Phoenix
        double prevY = 0.0;
        int iteration = 0;

        while (x * x + y * y <= BailoutSq && iteration < maxIterations)
        {
            double xNew, yNew;

            if (fractalType == 1)
            {
                // Burning Ship
                double ax = x < 0 ? -x : x;
                double ay = y < 0 ? -y : y;
                xNew = ax * ax - ay * ay + x0;
                yNew = 2.0 * ax * ay + y0;
            }
            else if (fractalType == 2)
            {
                // Tricorn (Mandelbar): conjugate z before squaring
                xNew = x * x - y * y + x0;
                yNew = -2.0 * x * y + y0;
            }
            else if (fractalType == 3)
            {
                // Cubic: z³ + c
                double x2 = x * x;
                double y2 = y * y;
                xNew = x * x2 - 3.0 * x * y2 + x0;
                yNew = 3.0 * x2 * y - y * y2 + y0;
            }
            else if (fractalType == 4)
            {
                // Quartic: z⁴ + c
                double x2 = x * x;
                double y2 = y * y;
                double x2y2 = x2 - y2;
                xNew = x2y2 * x2y2 - 4.0 * x2 * y2 + x0;
                yNew = 4.0 * x * y * x2y2 + y0;
            }
            else if (fractalType == 5)
            {
                // Phoenix: z(n+1) = z(n)² + Re(c) + Im(c)*z(n-1)
                xNew = x * x - y * y + x0 + y0 * prevX;
                yNew = 2.0 * x * y + y0 * prevY;
                prevX = x;
                prevY = y;
            }
            else if (fractalType == 6)
            {
                // Celtic: like Mandelbrot but take |Re(z²)|
                double re2 = x * x - y * y;
                xNew = (re2 < 0 ? -re2 : re2) + x0;
                yNew = 2.0 * x * y + y0;
            }
            else
            {
                // Standard Mandelbrot: z² + c
                xNew = x * x - y * y + x0;
                yNew = 2.0 * x * y + y0;
            }

            x = xNew;
            y = yNew;
            iteration++;
        }

        if (iteration >= maxIterations)
        {
            output[index] = 0;
            magnitudes[index] = 0.0;
        }
        else
        {
            output[index] = iteration;
            magnitudes[index] = x * x + y * y; // |z|²
        }
    }
}
