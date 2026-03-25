using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace MandelbrotGpu;

public sealed class MandelbrotRenderer : IDisposable
{
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private Action<Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<double, Stride1D.Dense>, int, int, double, double, double, int, int> _kernel;
    private readonly Action<Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<double, Stride1D.Dense>, int, int, double, double, double, int, int> _defaultKernel;
    private readonly object _kernelLock = new();

    private readonly Context _algoContext;
    private readonly Accelerator _algoAccelerator;

    public string AcceleratorName => _accelerator.Name;
    public AcceleratorType AcceleratorType => _accelerator.AcceleratorType;
    public Accelerator CustomKernelAccelerator => _algoAccelerator;

    public MandelbrotRenderer()
    {
        _context = Context.Create(b => b.Default());
        _accelerator = CreateBestAccelerator(_context);
        _defaultKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<double, Stride1D.Dense>,
            int, int, double, double, double, int, int>(
            MandelbrotKernel.ComputeFractal);
        _kernel = _defaultKernel;

        // Second context with algorithms enabled for custom kernels
        _algoContext = Context.Create(b => b.Default().EnableAlgorithms());
        _algoAccelerator = CreateBestAccelerator(_algoContext);

        Console.WriteLine($"Using accelerator: {_accelerator.Name} ({_accelerator.AcceleratorType})");
    }

    private bool _usingCustom;

    public void LoadCustomKernel(Action<Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<double, Stride1D.Dense>, int, int, double, double, double, int, int> compiled)
    {
        lock (_kernelLock)
        {
            _kernel = compiled;
            _usingCustom = true;
        }
    }

    public void ResetToDefaultKernel()
    {
        lock (_kernelLock)
        {
            _kernel = _defaultKernel;
            _usingCustom = false;
        }
    }

    private static Accelerator CreateBestAccelerator(Context context)
    {
        // Try CUDA first, then OpenCL, fall back to CPU
        foreach (var device in context.GetCudaDevices())
        {
            Console.WriteLine($"Found CUDA device: {device.Name}");
            return device.CreateAccelerator(context);
        }

        foreach (var device in context.GetCLDevices())
        {
            Console.WriteLine($"Found OpenCL device: {device.Name}");
            return device.CreateAccelerator(context);
        }

        Console.WriteLine("No GPU found, using CPU accelerator");
        return context.CreateCPUAccelerator(0);
    }

    public double[] Render(int width, int height, double centerX, double centerY, double scale, int maxIterations, int fractalType = 0)
    {
        int totalPixels = width * height;

        Action<Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<double, Stride1D.Dense>, int, int, double, double, double, int, int> kernel;
        bool custom;
        lock (_kernelLock) { kernel = _kernel; custom = _usingCustom; }

        var accel = custom ? _algoAccelerator : _accelerator;
        using var iterBuf = accel.Allocate1D<int>(totalPixels);
        using var magBuf = accel.Allocate1D<double>(totalPixels);

        kernel((int)iterBuf.Length, iterBuf.View, magBuf.View, width, height, centerX, centerY, scale, maxIterations, fractalType);
        accel.Synchronize();

        var iterations = iterBuf.GetAsArray1D();
        var magnitudes = magBuf.GetAsArray1D();

        // Compute smooth iteration values on CPU
        var smooth = new double[totalPixels];
        for (int i = 0; i < totalPixels; i++)
        {
            if (iterations[i] == 0)
            {
                smooth[i] = 0.0;
            }
            else
            {
                // Normalized iteration count: iter + 1 - log2(log2(|z|))
                // = iter + 1 - log2(0.5 * log2(|z|²))
                double logZn = Math.Log(magnitudes[i]) * 0.5; // ln(|z|)
                double nu = Math.Log2(logZn / Math.Log(2.0));  // log2(log2(|z|))
                smooth[i] = iterations[i] + 1.0 - nu;
            }
        }

        return smooth;
    }

    /// <summary>
    /// Renders to a raw RGBA byte array (4 bytes per pixel).
    /// </summary>
    public byte[] RenderToRgba(int width, int height, double centerX, double centerY, double scale, int maxIterations, int fractalType = 0)
    {
        var smoothIters = Render(width, height, centerX, centerY, scale, maxIterations, fractalType);
        var rgba = new byte[width * height * 4];

        for (int i = 0; i < smoothIters.Length; i++)
        {
            int offset = i * 4;

            var (r, g, b) = Palette.Map(smoothIters[i], maxIterations);
            rgba[offset] = r;
            rgba[offset + 1] = g;
            rgba[offset + 2] = b;
            rgba[offset + 3] = 255;
        }

        return rgba;
    }

    /// <summary>
    /// Renders to PNG bytes.
    /// </summary>
    public byte[] RenderToPng(int width, int height, double centerX, double centerY, double scale, int maxIterations, int fractalType = 0)
    {
        var smoothIters = Render(width, height, centerX, centerY, scale, maxIterations, fractalType);
        return PngEncoder.Encode(smoothIters, width, height, maxIterations);
    }

    private static (byte R, byte G, byte B) HsvToRgb(double h, double s, double v)
    {
        h %= 1.0;
        if (h < 0) h += 1.0;

        int hi = (int)(h * 6.0) % 6;
        double f = h * 6.0 - Math.Floor(h * 6.0);
        double p = v * (1.0 - s);
        double q = v * (1.0 - f * s);
        double t = v * (1.0 - (1.0 - f) * s);

        double r, g, b;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    public void Dispose()
    {
        _algoAccelerator.Dispose();
        _algoContext.Dispose();
        _accelerator.Dispose();
        _context.Dispose();
    }
}
