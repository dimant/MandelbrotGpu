using System.Reflection;
using System.Runtime.Loader;
using ILGPU;
using ILGPU.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MandelbrotGpu;

public sealed class FormulaCompiler
{
    private static readonly string KernelTemplate = """
        using ILGPU;
        using ILGPU.Runtime;
        using ILGPU.Algorithms;
        using static ILGPU.Algorithms.XMath;

        public static class CustomKernel
        {
            public static void Compute(
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
                double prevX = 0.0;
                double prevY = 0.0;
                int iteration = 0;

                while (x * x + y * y <= 256.0 && iteration < maxIterations)
                {
                    double xNew, yNew;
                    %%FORMULA%%
                    prevX = x;
                    prevY = y;
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
                    magnitudes[index] = x * x + y * y;
                }
            }
        }
        """;

    public record CompileResult(bool Success, string[] Errors, Action<Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<double, Stride1D.Dense>, int, int, double, double, double, int, int>? Kernel);

    public CompileResult Compile(string formula, Accelerator accelerator)
    {
        var source = KernelTemplate.Replace("%%FORMULA%%", formula);

        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Gather references from the ILGPU types we need
        var references = new List<MetadataReference>();

        // Core runtime assemblies
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Math).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));

        // ILGPU assemblies
        references.Add(MetadataReference.CreateFromFile(typeof(Index1D).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Accelerator).Assembly.Location));

        // Add any additional assemblies that ILGPU types reference
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
            {
                references.Add(MetadataReference.CreateFromFile(asm.Location));
            }
        }

        var compilation = CSharpCompilation.Create(
            $"CustomFractal_{Guid.NewGuid():N}",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToArray();
            return new CompileResult(false, errors, null);
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
        var type = assembly.GetType("CustomKernel")!;
        var method = type.GetMethod("Compute", BindingFlags.Public | BindingFlags.Static)!;

        // Create a delegate from the MethodInfo and load it as an ILGPU kernel
        var del = method.CreateDelegate<Action<Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<double, Stride1D.Dense>, int, int, double, double, double, int, int>>();
        var kernel = accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<double, Stride1D.Dense>,
            int, int, double, double, double, int, int>(del);

        return new CompileResult(true, Array.Empty<string>(), kernel);
    }
}
