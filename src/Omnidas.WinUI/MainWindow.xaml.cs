using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Omnidas.Core.Interfaces;
using Omnidas.Core.Models;
using Omnidas.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Graphics;

namespace Omnidas_WinUI;

public sealed partial class MainWindow : Window
{
    private readonly string? _targetPath;
    private readonly ConversionManager _conversionManager = new();
    private readonly BatchConversionManager _batchManager;
    private string? _selectedExtension;
    private AppWindow? _appWindow;

    public MainWindow(string? targetPath)
    {
        InitializeComponent();
        _targetPath = targetPath;
        _batchManager = new BatchConversionManager(_conversionManager);

        ConfigureCompactWindow();
        LoadSourceInfo();
    }

    private void ConfigureCompactWindow()
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow != null)
        {
            _appWindow.Resize(new SizeInt32(380, 520));

            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var centeredX = (displayArea.WorkArea.Width - 380) / 2;
                var centeredY = (displayArea.WorkArea.Height - 520) / 2;
                _appWindow.Move(new PointInt32(centeredX, centeredY));
            }
        }
    }

    private void LoadSourceInfo()
    {
        if (string.IsNullOrWhiteSpace(_targetPath))
        {
            TxtSourceItem.Text = "Nenhum arquivo recebido";
            TxtSourceMeta.Text = "Execute pelo menu de contexto do Explorer";
            return;
        }

        bool isDir = Directory.Exists(_targetPath);
        bool isFile = File.Exists(_targetPath);

        if (!isDir && !isFile)
        {
            TxtSourceItem.Text = "Item não encontrado";
            TxtSourceMeta.Text = _targetPath;
            return;
        }

        string sourceExtension = "";
        if (isDir)
        {
            IconItemType.Glyph = "\uE8B7";
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
            IconItemType.Glyph = "\uE8A5";
            var fi = new FileInfo(_targetPath);
            TxtSourceItem.Text = fi.Name;
            TxtSourceMeta.Text = $"Arquivo • {fi.Length / 1024:N0} KB";
            sourceExtension = fi.Extension.TrimStart('.');
        }

        PopulatePills(sourceExtension);
    }

    private void PopulatePills(string sourceExtension)
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
                _conversionManager.Registry.GetFormat("flac")!,
                _conversionManager.Registry.GetFormat("opus")!
            }.Where(f => f != null).ToList();
        }

        RadioButton? first = null;
        foreach (var t in targets)
        {
            var radio = new RadioButton
            {
                Content = $".{t.Extension.ToUpperInvariant()}",
                GroupName = "Formats",
                Margin = new Thickness(0, 0, 8, 4),
                Tag = t
            };

            radio.Checked += (s, e) =>
            {
                _selectedExtension = t.Extension;
                BtnConvert.IsEnabled = true;
                UpdateDiagnostic(sourceExtension, t.Extension);
            };

            FormatsPanel.Children.Add(radio);
            first ??= radio;
        }

        if (first != null) first.IsChecked = true;
    }

    private void UpdateDiagnostic(string sourceExt, string targetExt)
    {
        var insight = _batchManager.Analyzer.Analyze(sourceExt, targetExt, 1);
        TxtInsightTitle.Text = insight.Summary;
        TxtInsightBody.Text = insight.Explanation;

        var dst = targetExt.ToLowerInvariant();
        if (dst == "mp3")
        {
            BarQuality.Value = 88;
            BarSize.Value = 45;
            BarCompat.Value = 100;
        }
        else if (dst == "flac" || dst == "wav")
        {
            BarQuality.Value = 100;
            BarSize.Value = 90;
            BarCompat.Value = 75;
        }
        else if (dst == "m4a")
        {
            BarQuality.Value = 92;
            BarSize.Value = 40;
            BarCompat.Value = 95;
        }
        else if (dst == "opus")
        {
            BarQuality.Value = 95;
            BarSize.Value = 30;
            BarCompat.Value = 70;
        }
    }

    private async void BtnConvert_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_targetPath) || string.IsNullOrWhiteSpace(_selectedExtension))
            return;

        BtnConvert.IsEnabled = false;
        ConversionProgressBar.Visibility = Visibility.Visible;
        TxtProgressStatus.Visibility = Visibility.Visible;
        TxtProgressStatus.Text = "Iniciando conversão...";

        try
        {
            if (Directory.Exists(_targetPath))
            {
                var files = Directory.GetFiles(_targetPath, "*.*");
                var convertible = files.Where(f => _conversionManager.FindAdapter(Path.GetExtension(f).TrimStart('.'), _selectedExtension) != null).ToList();

                var progress = new Progress<BatchProgressItem>(p =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        var done = p.CompletedFiles + p.FailedFiles;
                        TxtProgressStatus.Text = $"Convertendo [{done}/{p.TotalFiles}] • {p.CurrentFile}";
                    });
                });

                var result = await _batchManager.ConvertBatchAsync(convertible, _selectedExtension, progress: progress);

                ConversionProgressBar.Visibility = Visibility.Collapsed;
                TxtProgressStatus.Text = $"✓ Concluído! {result.SuccessCount} arquivo(s) em {result.TotalTime.TotalSeconds:F1}s";
                BtnConvert.Content = "Fechar";
                BtnConvert.IsEnabled = true;
                BtnConvert.Click -= BtnConvert_Click;
                BtnConvert.Click += (s, ev) => Close();
            }
            else
            {
                var progress = new Progress<ConversionProgress>(p =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (p.TimeProcessed.HasValue)
                            TxtProgressStatus.Text = $"Processando: {p.TimeProcessed.Value:mm\\:ss}";
                    });
                });

                var result = await _conversionManager.ConvertAsync(_targetPath, _selectedExtension, progress: progress);

                ConversionProgressBar.Visibility = Visibility.Collapsed;
                TxtProgressStatus.Text = "✓ Conversão concluída!";
                BtnConvert.Content = "Abrir no Explorer";
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
            TxtProgressStatus.Text = $"Falha: {ex.Message}";
            BtnConvert.IsEnabled = true;
        }
    }
}
