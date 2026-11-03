using Omnidas.Core.Models;

namespace Omnidas.Core.Services;

public class FormatRegistry
{
    private readonly Dictionary<string, FormatDefinition> _formats = new(StringComparer.OrdinalIgnoreCase);

    public FormatRegistry()
    {
        RegisterDefaults();
    }

    public void Register(FormatDefinition format)
    {
        _formats[format.Extension] = format;
        foreach (var alt in format.AlternativeExtensions)
        {
            _formats[alt] = format;
        }
    }

    public FormatDefinition? GetFormat(string extension)
    {
        var cleanExt = extension.TrimStart('.').ToLowerInvariant();
        return _formats.TryGetValue(cleanExt, out var format) ? format : null;
    }

    public IEnumerable<FormatDefinition> GetSupportedTargets(string sourceExtension)
    {
        var sourceFormat = GetFormat(sourceExtension);
        if (sourceFormat == null) return Enumerable.Empty<FormatDefinition>();

        // Na v1 / M1: formatos que suportam escrita da mesma categoria
        return _formats.Values
            .Distinct()
            .Where(f => f.Category == sourceFormat.Category && f.CanWrite && !f.Extension.Equals(sourceFormat.Extension, StringComparison.OrdinalIgnoreCase));
    }

    private void RegisterDefaults()
    {
        // Áudio
        Register(new FormatDefinition("opus", "Opus Audio", MediaCategory.Audio, "audio/opus", Array.Empty<string>(), true, true, "Codec de áudio versátil e de alta eficiência"));
        Register(new FormatDefinition("mp3", "MP3 Audio", MediaCategory.Audio, "audio/mpeg", Array.Empty<string>(), true, true, "Formato universal com compatibilidade máxima"));
        Register(new FormatDefinition("m4a", "AAC Audio", MediaCategory.Audio, "audio/mp4", new[] { "aac" }, true, true, "Excelente qualidade em bitrates médios e padrão Apple"));
        Register(new FormatDefinition("wav", "Waveform Audio", MediaCategory.Audio, "audio/wav", Array.Empty<string>(), true, true, "Áudio PCM não comprimido, sem perdas"));
        Register(new FormatDefinition("flac", "FLAC Audio", MediaCategory.Audio, "audio/flac", Array.Empty<string>(), true, true, "Compressão sem perda de qualidade (lossless)"));
        Register(new FormatDefinition("ogg", "Ogg Vorbis Audio", MediaCategory.Audio, "audio/ogg", Array.Empty<string>(), true, true, "Formato livre de boa eficiência"));
        Register(new FormatDefinition("wma", "Windows Media Audio", MediaCategory.Audio, "audio/x-ms-wma", Array.Empty<string>(), true, true, "Formato proprietário legada Microsoft"));

        // Vídeo
        Register(new FormatDefinition("mp4", "MPEG-4 Video", MediaCategory.Video, "video/mp4", Array.Empty<string>(), true, true, "Compatibilidade universal em players e navegadores"));
        Register(new FormatDefinition("mkv", "Matroska Video", MediaCategory.Video, "video/x-matroska", Array.Empty<string>(), true, true, "Contêiner flexível para múltiplas faixas e legendas"));
        Register(new FormatDefinition("webm", "WebM Video", MediaCategory.Video, "video/webm", Array.Empty<string>(), true, true, "Formato aberto para web (VP9/AV1 + Opus)"));
        Register(new FormatDefinition("mov", "QuickTime Movie", MediaCategory.Video, "video/quicktime", Array.Empty<string>(), true, true, "Formato padrão da Apple"));
    }
}
