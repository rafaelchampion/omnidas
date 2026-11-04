using Omnidas.Core.Interfaces;
using Omnidas.Core.Models;

namespace Omnidas.Core.Services;

public record BatchProgressItem(
    int TotalFiles,
    int CompletedFiles,
    int FailedFiles,
    string CurrentFile,
    TimeSpan ElapsedTime,
    TimeSpan? EstimatedRemainingTime
);

public class BatchConversionManager
{
    private readonly ConversionManager _conversionManager;
    private readonly ConversionAnalyzer _analyzer = new();

    public BatchConversionManager(ConversionManager? conversionManager = null)
    {
        _conversionManager = conversionManager ?? new ConversionManager();
    }

    public ConversionAnalyzer Analyzer => _analyzer;

    public async Task<BatchResult> ConvertBatchAsync(
        IReadOnlyList<string> filePaths,
        string targetExtension,
        int? maxConcurrency = null,
        IProgress<BatchProgressItem>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (filePaths.Count == 0)
        {
            return new BatchResult(0, 0, TimeSpan.Zero, new List<BatchFileError>());
        }

        var sampleExt = Path.GetExtension(filePaths[0]).TrimStart('.');
        var insight = _analyzer.Analyze(sampleExt, targetExtension, filePaths.Count);
        int concurrency = maxConcurrency ?? insight.RecommendedConcurrency;

        int completed = 0;
        int failed = 0;
        var errors = new System.Collections.Concurrent.ConcurrentBag<BatchFileError>();
        var swTotal = System.Diagnostics.Stopwatch.StartNew();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = concurrency,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(filePaths, parallelOptions, async (file, ct) =>
        {
            var fileName = Path.GetFileName(file);
            try
            {
                await _conversionManager.ConvertAsync(file, targetExtension, cancellationToken: ct);
                Interlocked.Increment(ref completed);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                errors.Add(new BatchFileError(file, ex.Message));
            }

            var currentDone = completed + failed;
            TimeSpan? eta = null;
            if (currentDone > 0)
            {
                var avgPerFile = swTotal.Elapsed.TotalMilliseconds / currentDone;
                var remaining = filePaths.Count - currentDone;
                eta = TimeSpan.FromMilliseconds(avgPerFile * remaining);
            }

            progress?.Report(new BatchProgressItem(
                TotalFiles: filePaths.Count,
                CompletedFiles: completed,
                FailedFiles: failed,
                CurrentFile: fileName,
                ElapsedTime: swTotal.Elapsed,
                EstimatedRemainingTime: eta
            ));
        });

        swTotal.Stop();
        return new BatchResult(completed, failed, swTotal.Elapsed, errors.ToList());
    }
}

public record BatchResult(
    int SuccessCount,
    int FailureCount,
    TimeSpan TotalTime,
    IReadOnlyList<BatchFileError> Errors
);

public record BatchFileError(string FilePath, string ErrorMessage);
