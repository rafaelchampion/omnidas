using System.IO;
using System.Windows;
using System.Windows.Controls;
using Omnidas.Core.Interfaces;
using Omnidas.Core.Models;
using Omnidas.Core.Services;

namespace Omnidas.Gui;

public partial class MainWindow : Window
{
    private readonly string? _targetPath;
    private readonly ConversionManager _conversionManager = new();
    private readonly BatchConversionManager _batchManager;
    private string? _selectedExtension;

    public MainWindow(string? targetPath)
    {
        InitializeComponent();
        _targetPath = targetPath;
        _batchManager = new BatchConversionManager(_conversionManager);

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_targetPath))
        {
            TxtSourceItem.Text = "Nenhum arquivo ou pasta recebido";
            TxtSourceMeta.Text = "Use o botão direito no Windows Explorer";
            return;
        }

        bool isDir = Directory.Exists(_targetPath);
        bool isFile = File.Exists(_targetPath);

        if (!isDir && !isFile)
        {
            TxtSourceItem.Text = "Caminho não encontrado";
            TxtSourceMeta.Text = _targetPath;
            return;
        }

        string sourceExtension = "";
        if (isDir)
        {
            var files = Directory.GetFiles(_targetPath, "*.*");
            TxtSourceItem.Text = Path.GetFileName(_targetPath);
            TxtSourceMeta.Text = $"Pasta com {files.Length} arquivo(s)";
            if (files.Length > 0)
            {
                sourceExtension = Path.GetExtension(files[0]).TrimStart('.');
            }
        }
        else
        {
            TxtSourceItem.Text = Path.GetFileName(_targetPath);
            var fi = new FileInfo(_targetPath);
            TxtSourceMeta.Text = $"Arquivo {fi.Length / 1024:N0} KB";
            sourceExtension = fi.Extension.TrimStart('.');
        }

        PopulateTargetFormats(sourceExtension);
    }

    private void PopulateTargetFormats(string sourceExtension)
    {
        FormatsPanel.Children.Clear();
        var targets = _conversionManager.Registry.GetSupportedTargets(sourceExtension).ToList();

        if (!targets.Any())
        {
            targets = new List<FormatDefinition>
            {
                _conversionManager.Registry.GetFormat("mp3")!,
                _conversionManager.Registry.GetFormat("m4a")!,
                _conversionManager.Registry.GetFormat("wav")!,
                _conversionManager.Registry.GetFormat("opus")!,
                _conversionManager.Registry.GetFormat("flac")!
            }.Where(f => f != null).ToList();
        }

        RadioButton? firstBtn = null;
        foreach (var target in targets)
        {
            var radio = new RadioButton
            {
                Content = $".{target.Extension.ToUpperInvariant()}",
                GroupName = "FormatsGroup",
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(12, 6, 12, 6),
                Tag = target
            };

            radio.Checked += (s, e) =>
            {
                _selectedExtension = target.Extension;
                BtnConvert.IsEnabled = true;
                UpdateInsight(sourceExtension, target.Extension);
            };

            FormatsPanel.Children.Add(radio);
            firstBtn ??= radio;
        }

        if (firstBtn != null)
        {
            firstBtn.IsChecked = true;
        }
    }

    private void UpdateInsight(string sourceExt, string targetExt)
    {
        var insight = _batchManager.Analyzer.Analyze(sourceExt, targetExt, 1);
        TxtInsightTitle.Text = insight.Summary;
        TxtInsightBody.Text = insight.Explanation;
    }

    private async void BtnConvert_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_targetPath) || string.IsNullOrWhiteSpace(_selectedExtension))
            return;

        BtnConvert.IsEnabled = false;
        ConversionProgressBar.Visibility = Visibility.Visible;
        TxtProgressStatus.Visibility = Visibility.Visible;
        TxtProgressStatus.Text = "Iniciando processamento...";

        try
        {
            if (Directory.Exists(_targetPath))
            {
                var files = Directory.GetFiles(_targetPath, "*.*");
                var convertible = files.Where(f => _conversionManager.FindAdapter(Path.GetExtension(f).TrimStart('.'), _selectedExtension) != null).ToList();

                var progress = new Progress<BatchProgressItem>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var done = p.CompletedFiles + p.FailedFiles;
                        TxtProgressStatus.Text = $"Convertendo [{done}/{p.TotalFiles}] | {p.CurrentFile}";
                    });
                });

                var result = await _batchManager.ConvertBatchAsync(convertible, _selectedExtension, progress: progress);

                ConversionProgressBar.Visibility = Visibility.Collapsed;
                TxtProgressStatus.Text = $"Concluído! {result.SuccessCount} arquivo(s) convertidos em {result.TotalTime.TotalSeconds:F1}s.";
                BtnConvert.Content = "Fechar";
                BtnConvert.IsEnabled = true;
                BtnConvert.Click -= BtnConvert_Click;
                BtnConvert.Click += (s, ev) => Close();
            }
            else
            {
                var progress = new Progress<ConversionProgress>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (p.TimeProcessed.HasValue)
                            TxtProgressStatus.Text = $"Processando áudio: {p.TimeProcessed.Value:mm\\:ss}";
                    });
                });

                var result = await _conversionManager.ConvertAsync(_targetPath, _selectedExtension, progress: progress);

                ConversionProgressBar.Visibility = Visibility.Collapsed;
                TxtProgressStatus.Text = "Concluído com sucesso!";
                BtnConvert.Content = "Abrir Pasta";
                BtnConvert.IsEnabled = true;
                BtnConvert.Click -= BtnConvert_Click;
                BtnConvert.Click += (s, ev) =>
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{result}\"");
                    Close();
                };
            }
        }
        catch (Exception ex)
        {
            ConversionProgressBar.Visibility = Visibility.Collapsed;
            TxtProgressStatus.Text = $"Erro: {ex.Message}";
            BtnConvert.IsEnabled = true;
        }
    }
}
