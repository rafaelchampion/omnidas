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
        Console.WriteLine("  omnidas convert <arquivo-ou-pasta> --to <extensao> [--recursive]");
        Console.WriteLine("  omnidas formats [--from <extensao>]");
        Console.WriteLine();
        Console.WriteLine("Exemplos:");
        Console.WriteLine("  omnidas convert audio.opus --to mp3");
        Console.WriteLine("  omnidas convert \"C:\\Musicas\\MinhaPasta\" --to mp3");
        Console.WriteLine("  omnidas formats --from opus");
    }

    private static async Task<int> HandleConvertAsync(string[] args)
    {
        string? targetPath = null;
        string? targetExt = null;
        bool recursive = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--to" or "-t" && i + 1 < args.Length)
            {
                targetExt = args[++i];
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

        // Cenário 1: É um DIRETÓRIO (Conversão em Lote)
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

            Console.WriteLine($"\n[Omnidas] Lote detectado: {convertibleFiles.Count} arquivo(s) na pasta para converter para .{targetExt}.\n");

            int successCount = 0;
            int errorCount = 0;
            var swTotal = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < convertibleFiles.Count; i++)
            {
                var file = convertibleFiles[i];
                var fileName = Path.GetFileName(file);
                Console.Write($"[{i + 1}/{convertibleFiles.Count}] {fileName} ... ");

                try
                {
                    var result = await manager.ConvertAsync(file, targetExt);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Concluído");
                    Console.ResetColor();
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ Falha: {ex.Message}");
                    Console.ResetColor();
                    errorCount++;
                }
            }

            swTotal.Stop();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Lote finalizado em {swTotal.Elapsed.TotalSeconds:F1}s: {successCount} sucesso(s), {errorCount} falha(s).");
            Console.ResetColor();
            return errorCount == 0 ? 0 : 2;
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

        var progress = new Progress<ConversionProgress>(p =>
        {
            if (p.TimeProcessed.HasValue)
            {
                Console.Write($"\rProcessando: {p.TimeProcessed.Value:mm\\:ss\\.ff}");
            }
        });

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await manager.ConvertAsync(fileInfo.FullName, targetExt, progress: progress);
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
