using Omnidas.Core.Models;
using Omnidas.Core.Services;

namespace Omnidas.Core.Tests;

public class FormatRegistryTests
{
    private readonly FormatRegistry _registry = new();

    [Fact]
    public void GetFormat_Opus_ReturnsValidDefinition()
    {
        var format = _registry.GetFormat("opus");
        Assert.NotNull(format);
        Assert.Equal("Opus Audio", format.Name);
        Assert.Equal(MediaCategory.Audio, format.Category);
    }

    [Fact]
    public void GetSupportedTargets_FromOpus_IncludesMp3AndM4a()
    {
        var targets = _registry.GetSupportedTargets("opus").Select(t => t.Extension).ToList();
        Assert.Contains("mp3", targets);
        Assert.Contains("m4a", targets);
        Assert.Contains("wav", targets);
        Assert.DoesNotContain("opus", targets); // Não sugere o mesmo formato
    }

    [Fact]
    public void GenerateSafeOutputPath_WhenFileExists_AddsNumberedSuffix()
    {
        var manager = new ConversionManager(_registry);
        var tempFile = Path.GetTempFileName();
        try
        {
            var output1 = manager.GenerateSafeOutputPath(tempFile, "mp3");
            File.WriteAllText(output1, "dummy");

            var output2 = manager.GenerateSafeOutputPath(tempFile, "mp3");
            Assert.Contains("(1)", output2);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
