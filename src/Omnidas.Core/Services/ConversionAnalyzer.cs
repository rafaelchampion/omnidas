using Omnidas.Core.Models;

namespace Omnidas.Core.Services;

public record ConversionInsight(
    string Summary,
    string Explanation,
    bool IsHeavyReencoding,
    int RecommendedConcurrency
);

public class ConversionAnalyzer
{
    public ConversionInsight Analyze(string sourceExtension, string targetExtension, int fileCount)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var dst = targetExtension.TrimStart('.').ToLowerInvariant();

        // Detecta re-encoding de áudio para MP3
        if (dst == "mp3")
        {
            var isLosslessOrOpus = src is "flac" or "wav" or "opus" or "m4a" or "aac";
            var cpuCount = Environment.ProcessorCount;
            // Recomendamos deixar 1 ou 2 núcleos livres para o Windows não engasgar
            var concurrency = Math.Clamp(cpuCount > 4 ? cpuCount - 2 : cpuCount - 1, 2, 8);

            return new ConversionInsight(
                Summary: "Re-encoding acústico de alta fidelidade (LAME MP3 192kbps)",
                Explanation: "A conversão decodifica o fluxo original e recria a compressão psicoacústica MP3 quadro a quadro. " +
                            "O algoritmo do codec é intensivo em CPU. Para acelerar sem perder qualidade, ativamos o processamento paralelo " +
                            $"utilizando {concurrency} núcleos da sua CPU simultaneamente.",
                IsHeavyReencoding: true,
                RecommendedConcurrency: concurrency
            );
        }

        // Outros casos comuns
        var defaultConcurrency = Math.Clamp(Environment.ProcessorCount - 1, 2, 6);
        return new ConversionInsight(
            Summary: $"Conversão padrão de .{src} para .{dst}",
            Explanation: $"Processamento paralelo otimizado para múltiplos arquivos ({defaultConcurrency} tarefas simultâneas).",
            IsHeavyReencoding: false,
            RecommendedConcurrency: defaultConcurrency
        );
    }
}
