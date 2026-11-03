using Omnidas.Core.Models;

namespace Omnidas.Core.Models;

public record ConversionPreset(
    string Id,
    string Name,
    string Description,
    MediaCategory Category,
    string TargetExtension,
    QualityProfile Profile,
    IReadOnlyDictionary<string, string> EngineParameters
);

public enum QualityProfile
{
    BestQuality,
    Balanced,
    SmallestSize,
    WhatsAppWeb,
    Lossless
}
