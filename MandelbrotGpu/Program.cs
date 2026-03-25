using MandelbrotGpu;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<MandelbrotRenderer>();
builder.Services.AddSingleton<FormulaCompiler>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/render", (
    MandelbrotRenderer renderer,
    double cx = -0.5,
    double cy = 0.0,
    double scale = 0.004,
    int width = 960,
    int height = 640,
    int maxIter = 256,
    int fractal = 0) =>
{
    // Clamp dimensions to prevent abuse
    width = Math.Clamp(width, 64, 3840);
    height = Math.Clamp(height, 64, 2160);
    maxIter = Math.Clamp(maxIter, 32, 4096);
    fractal = Math.Clamp(fractal, 0, 6);

    var png = renderer.RenderToPng(width, height, cx, cy, scale, maxIter, fractal);
    return Results.File(png, "image/png");
});

app.MapGet("/api/info", (MandelbrotRenderer renderer) => Results.Ok(new
{
    accelerator = renderer.AcceleratorName,
    type = renderer.AcceleratorType.ToString()
}));

app.MapPost("/api/compile", async (HttpRequest request, MandelbrotRenderer renderer, FormulaCompiler compiler) =>
{
    var body = await new StreamReader(request.Body).ReadToEndAsync();
    var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var formula = System.Text.Json.JsonSerializer.Deserialize<FormulaRequest>(body, jsonOpts);
    if (formula?.Code is null)
        return Results.BadRequest(new { success = false, errors = new[] { "No formula provided" } });

    try
    {
        var result = compiler.Compile(formula.Code, renderer.CustomKernelAccelerator);
        if (!result.Success)
            return Results.Ok(new { success = false, errors = result.Errors });

        renderer.LoadCustomKernel(result.Kernel!);
        return Results.Ok(new { success = true, errors = Array.Empty<string>() });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { success = false, errors = new[] { $"Kernel load failed: {ex.Message}" } });
    }
});

app.MapPost("/api/reset-kernel", (MandelbrotRenderer renderer) =>
{
    renderer.ResetToDefaultKernel();
    return Results.Ok(new { success = true });
});

app.Run();

record FormulaRequest(string? Code);
