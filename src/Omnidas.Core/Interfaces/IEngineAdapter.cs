using Omnidas.Core.Models;

namespace Omnidas.Core.Interfaces;

public record ConversionJob(
    string SourceFilePath,
    string TargetFilePath,
    string TargetExtension,
    ConversionPreset? Preset = null,
    bool Overwrite = false
);

public record ConversionProgress(
    double Percentage,
    TimeSpan? TimeProcessed,
    string CurrentStep
);

public interface IEngineAdapter
{
    string EngineName { get; }
    bool IsAvailable();
    bool CanHandle(string sourceExtension, string targetExtension);
    Task ConvertAsync(
        ConversionJob job, 
        IProgress<ConversionProgress>? progress = null, 
        CancellationToken cancellationToken = default
    );
}
