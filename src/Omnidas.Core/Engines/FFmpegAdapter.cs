using System.Diagnostics;
using System.Text.RegularExpressions;
using Omnidas.Core.Interfaces;
using Omnidas.Core.Models;

namespace Omnidas.Core.Engines;

public class FFmpegAdapter : IEngineAdapter
{
    public string EngineName => "FFmpeg";
    private readonly string _ffmpegPath;

    public FFmpegAdapter(string? customFfmpegPath = null)
    {
        _ffmpegPath = customFfmpegPath ?? LocateFFmpeg();
    }

    private static string LocateFFmpeg()
    {
        // 1. Diretório do app / binários embutidos
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var localExe = Path.Combine(appDir, "engines", "ffmpeg", "ffmpeg.exe");
        if (File.Exists(localExe)) return localExe;

        // 2. PATH ou locais conhecidos no sistema
        var localApp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ffmpegio", "ffmpeg-downloader", "ffmpeg", "bin", "ffmpeg.exe"
        );
        if (File.Exists(localApp)) return localApp;

        return "ffmpeg"; // Assume que está no PATH global
    }

    public bool IsAvailable()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public bool CanHandle(string sourceExtension, string targetExtension)
    {
        var audioExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "opus", "mp3", "m4a", "aac", "wav", "flac", "ogg", "wma", "ac3" };
        var videoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "mp4", "mkv", "webm", "mov", "avi" };

        var src = sourceExtension.TrimStart('.');
        var dst = targetExtension.TrimStart('.');

        bool isAudio = audioExts.Contains(src) && audioExts.Contains(dst);
        bool isVideo = videoExts.Contains(src) && (videoExts.Contains(dst) || audioExts.Contains(dst));

        return isAudio || isVideo;
    }

    public async Task ConvertAsync(
        ConversionJob job, 
        IProgress<ConversionProgress>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(job.SourceFilePath))
            throw new FileNotFoundException("Arquivo de origem não encontrado", job.SourceFilePath);

        var outputDir = Path.GetDirectoryName(job.TargetFilePath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var args = BuildArguments(job);

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = args,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        var errorOutput = new List<string>();
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data == null) return;
            errorOutput.Add(e.Data);
            
            // Tenta extrair tempo de progresso (ex: time=00:01:23.45)
            var match = Regex.Match(e.Data, @"time=(\d{2}):(\d{2}):(\d{2}\.\d+)");
            if (match.Success)
            {
                var hours = int.Parse(match.Groups[1].Value);
                var minutes = int.Parse(match.Groups[2].Value);
                var seconds = double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                var timeSpan = TimeSpan.FromSeconds(hours * 3600 + minutes * 60 + seconds);
                
                progress?.Report(new ConversionProgress(0, timeSpan, "Convertendo..."));
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            if (File.Exists(job.TargetFilePath))
            {
                try { File.Delete(job.TargetFilePath); } catch { }
            }
            throw;
        }

        if (process.ExitCode != 0)
        {
            var detail = string.Join(Environment.NewLine, errorOutput.TakeLast(10));
            throw new InvalidOperationException($"FFmpeg falhou com código {process.ExitCode}: {detail}");
        }

        progress?.Report(new ConversionProgress(100, null, "Concluído"));
    }

    private string BuildArguments(ConversionJob job)
    {
        var overwriteFlag = job.Overwrite ? "-y" : "-n";
        var extraArgs = "";

        if (job.Preset != null)
        {
            // Aplica parâmetros do preset
            extraArgs = string.Join(" ", job.Preset.EngineParameters.Select(kv => $"{kv.Key} {kv.Value}"));
        }
        else
        {
            // Melhores padrões para áudio
            var targetExt = job.TargetExtension.TrimStart('.').ToLowerInvariant();
            if (targetExt == "mp3") extraArgs = "-b:a 192k";
            else if (targetExt == "m4a") extraArgs = "-c:a aac -b:a 192k";
            else if (targetExt == "opus") extraArgs = "-c:a libopus -b:a 128k";
        }

        return $"-hide_banner {overwriteFlag} -i \"{job.SourceFilePath}\" {extraArgs} \"{job.TargetFilePath}\"";
    }
}
