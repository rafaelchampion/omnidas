using Omnidas.Core;
using Omnidas.Core.Interfaces;
using Omnidas.Core.Services;

namespace Omnidas.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "convert":
                return await HandleConvertAsync(args.Skip(1).ToArray());

            case "formats":
                return HandleFormats(args.Skip(1).ToArray());

            case "version" or "-v" or "--version":
                Console.WriteLine($"{OmnidasInfo.Name} v0.1.0 (M1 - Preview)");
                return 0;

            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Comando desconhecido: '{command}'. Use 'omnidas --help' para ver os comandos.");
                Console.ResetColor();
                return 1;
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine($"{OmnidasInfo.Name} - Conversor Universal de Formatos para Windows");
        Console.WriteLine();
        Console.WriteLine("Uso:");
        Console.WriteLine("  omnidas convert <arquivo-ou-pasta> --to <extensao> [--threads <num>] [--recursive]");
        Console.WriteLine("  omnidas formats [--from <extensao>]");
        Console.WriteLine();
        Console.WriteLine("Exemplos:");
        Console.WriteLine("  omnidas convert audio.opus --to mp3");
        Console.WriteLine("  omnidas convert \"G:\\MeusAudios\" --to mp3 --threads 8");
        Console.WriteLine("  omnidas formats --from opus");
    }

    private static async Task<int> HandleConvertAsync(string[] args)
    {
        string? targetPath = null;
        string? targetExt = null;
        bool recursive = false;
        int? threads = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--to" or "-t" && i + 1 < args.Length)
            {
                targetExt = args[++i];
            }
            else if (args[i] is "--threads" && i + 1 < args.Length && int.TryParse(args[++i], out var t))
            {
                threads = t;
            }
            else if (args[i] is "--recursive" or "-r")
            {
                recursive = true;
            }
            else if (targetPath == null && !args[i].StartsWith('-'))
            {
                targetPath = args[i];
            }
        }

        if (string.IsNullOrWhiteSpace(targetPath) || string.IsNullOrWhiteSpace(targetExt))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Uso obrigatório: omnidas convert <arquivo-ou-pasta> --to <formato>");
            Console.ResetColor();
            return 1;
        }

        targetExt = targetExt.TrimStart('.');
        var manager = new ConversionManager();

        // Cenário 1: É um DIRETÓRIO (Conversão em Lote Paralela)
        if (Directory.Exists(targetPath))
        {
            var searchOpt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = Directory.GetFiles(targetPath, "*.*", searchOpt);

            var convertibleFiles = allFiles.Where(f =>
            {
                var ext = Path.GetExtension(f).TrimStart('.');
                if (ext.Equals(targetExt, StringComparison.OrdinalIgnoreCase)) return false;
                return manager.FindAdapter(ext, targetExt) != null;
            }).ToList();

            if (!convertibleFiles.Any())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Nenhum arquivo compatível encontrado na pasta para converter para .{targetExt}.");
                Console.ResetColor();
                return 0;
            }

            var batchManager = new BatchConversionManager(manager);
            var sampleExt = Path.GetExtension(convertibleFiles[0]).TrimStart('.');
            var insight = batchManager.Analyzer.Analyze(sampleExt, targetExt, convertibleFiles.Count);
            int activeThreads = threads ?? insight.RecommendedConcurrency;

            Console.WriteLine("\n========================================================");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Omnidas] Lote: {convertibleFiles.Count} arquivo(s) -> .{targetExt}");
            Console.ResetColor();
            Console.WriteLine($"Perfil: {insight.Summary}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Diagnóstico: {insight.Explanation}");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Paralelismo: Executando {activeThreads} conversões simultâneas");
            Console.ResetColor();
            Console.WriteLine("========================================================\n");

            var progress = new Progress<BatchProgressItem>(p =>
            {
                var totalDone = p.CompletedFiles + p.FailedFiles;
                var percent = (double)totalDone / p.TotalFiles * 100.0;
                var etaStr = p.EstimatedRemainingTime.HasValue 
                    ? $" | Restante: ~{p.EstimatedRemainingTime.Value:hh\\:mm\\:ss}" 
                    : "";

                Console.Write($"\rProgresso: [{totalDone}/{p.TotalFiles}] ({percent:F1}%) | Tempo decorrido: {p.ElapsedTime:mm\\:ss}{etaStr}   ");
            });

            var result = await batchManager.ConvertBatchAsync(convertibleFiles, targetExt, activeThreads, progress);

            Console.WriteLine("\n");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Lote concluído em {result.TotalTime.TotalMinutes:F1} min ({result.TotalTime.TotalSeconds:F1}s)");
            Console.WriteLine($"  - Sucessos: {result.SuccessCount}");
            if (result.FailureCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  - Falhas: {result.FailureCount}");
                foreach (var err in result.Errors.Take(5))
                {
                    Console.WriteLine($"    * {Path.GetFileName(err.FilePath)}: {err.ErrorMessage}");
                }
            }
            Console.ResetColor();

            return result.FailureCount == 0 ? 0 : 2;
        }

        // Cenário 2: É um ARQUIVO individual
        if (!File.Exists(targetPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Erro: O caminho especificado '{targetPath}' não é um arquivo nem uma pasta existente.");
            Console.ResetColor();
            return 1;
        }

        var fileInfo = new FileInfo(targetPath);

        Console.WriteLine($"\n[Omnidas] Convertendo '{fileInfo.Name}' para .{targetExt}...");

        var fileProgress = new Progress<ConversionProgress>(p =>
        {
            if (p.TimeProcessed.HasValue)
            {
                Console.Write($"\rProcessando: {p.TimeProcessed.Value:mm\\:ss\\.ff}");
            }
        });

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await manager.ConvertAsync(fileInfo.FullName, targetExt, progress: fileProgress);
            sw.Stop();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Sucesso! Gerado: {result} ({sw.Elapsed.TotalSeconds:F2}s)");
            Console.ResetColor();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"\nFalha na conversão: {ex.Message}");
            Console.ResetColor();
            return 2;
        }
    }

    private static int HandleFormats(string[] args)
    {
        string? fromExt = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--from" or "-f" && i + 1 < args.Length)
            {
                fromExt = args[++i];
            }
        }

        var registry = new FormatRegistry();

        if (string.IsNullOrWhiteSpace(fromExt))
        {
            Console.WriteLine("\nFormatos suportados no Omnidas (M1):");
            Console.WriteLine("  Áudio : opus, mp3, m4a, wav, flac, ogg, wma");
            Console.WriteLine("  Vídeo : mp4, mkv, webm, mov");
            Console.WriteLine("\nPara ver os destinos de um formato específico: omnidas formats --from <ext>");
        }
        else
        {
            var targets = registry.GetSupportedTargets(fromExt).ToList();
            if (!targets.Any())
            {
                Console.WriteLine($"\nNenhum formato de destino configurado para .{fromExt.TrimStart('.')}");
            }
            else
            {
                Console.WriteLine($"\nFormatos de destino possíveis para .{fromExt.TrimStart('.')}:");
                foreach (var t in targets)
                {
                    Console.WriteLine($"  - .{t.Extension,-6} | {t.Name,-20} | {t.Description}");
                }
            }
        }

        return 0;
    }
}
