using Omnidas.Core.Engines;
using Omnidas.Core.Interfaces;
using Omnidas.Core.Models;
using Omnidas.Core.Services;

namespace Omnidas.Core.Services;

public class ConversionManager
{
    private readonly FormatRegistry _registry;
    private readonly List<IEngineAdapter> _engines = new();

    public ConversionManager(FormatRegistry? registry = null)
    {
        _registry = registry ?? new FormatRegistry();
        _engines.Add(new FFmpegAdapter());
    }

    public void RegisterEngine(IEngineAdapter engine) => _engines.Add(engine);

    public FormatRegistry Registry => _registry;

    public IEngineAdapter? FindAdapter(string sourceExtension, string targetExtension)
    {
        return _engines.FirstOrDefault(e => e.IsAvailable() && e.CanHandle(sourceExtension, targetExtension));
    }

    public string GenerateSafeOutputPath(string sourcePath, string targetExtension)
    {
        var dir = Path.GetDirectoryName(sourcePath) ?? "";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
        var cleanExt = targetExtension.TrimStart('.');
        
        var targetPath = Path.Combine(dir, $"{nameWithoutExt}.{cleanExt}");
        if (!File.Exists(targetPath)) return targetPath;

        // Se já existe, aplica sufixo seguro sem sobrescrever
        int counter = 1;
        while (File.Exists(Path.Combine(dir, $"{nameWithoutExt} ({counter}).{cleanExt}")))
        {
            counter++;
        }
        return Path.Combine(dir, $"{nameWithoutExt} ({counter}).{cleanExt}");
    }

    public async Task<string> ConvertAsync(
        string sourcePath, 
        string targetExtension, 
        ConversionPreset? preset = null,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var srcExt = Path.GetExtension(sourcePath).TrimStart('.');
        var adapter = FindAdapter(srcExt, targetExtension);

        if (adapter == null)
            throw new NotSupportedException($"Não há motor disponível para converter de .{srcExt} para .{targetExtension}");

        var targetPath = GenerateSafeOutputPath(sourcePath, targetExtension);
        var job = new ConversionJob(sourcePath, targetPath, targetExtension, preset, Overwrite: false);

        await adapter.ConvertAsync(job, progress, cancellationToken);
        return targetPath;
    }
}
