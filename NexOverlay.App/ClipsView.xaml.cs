using System.Windows.Media;
using System.Windows.Media.Effects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NexOverlay.Core.Clipboard;
using NexOverlay.Storage.Clipboard;
using NexOverlay.Storage.Paths;

namespace NexOverlay.App;

public partial class ClipsView : UserControl
{
    private readonly ClipboardRepository _repository;

    private IReadOnlyList<ClipboardItem> _items =
        Array.Empty<ClipboardItem>();

    private bool _initialized;

    public event EventHandler? BackRequested;
    public event EventHandler? DataChanged;

    public ClipsView()
    {
        InitializeComponent();

        _repository =
            new ClipboardRepository(
                new AppDataPathService());

        Loaded +=
            OnLoaded;

        IsVisibleChanged +=
            OnIsVisibleChanged;
    }

    private async void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (!_initialized)
        {
            _initialized = true;
            await _repository.InitializeAsync();
        }

        RefreshHistoryState();
        await ReloadAsync();
    }

    private async void OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (!_initialized ||
            IsVisible != true)
        {
            return;
        }

        RefreshHistoryState();
        await ReloadAsync();
    }

    public async Task ReloadFromExternalAsync()
    {
        if (!_initialized)
            return;

        await ReloadAsync();
    }

    public void SetExternalSearch(
        string query)
    {
        SearchBox.Text =
            query ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(query))
        {
            SearchBox.Focus();

            SearchBox.CaretIndex =
                SearchBox.Text.Length;
        }
    }

    private async Task ReloadAsync()
    {
        try
        {
            _items =
                await _repository.GetAllAsync();

            CountText.Text =
                $"{_items.Count} saved";

            ApplyFilter();

            StatusText.Text =
                "Listening for clipboard text";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Storage error: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        var query =
            SearchBox.Text.Trim();

        IEnumerable<ClipboardItem> filtered =
            _items;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered =
                filtered.Where(item =>
                    item.Content.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase));
        }

        var visible =
            filtered.ToList();

        ClipList.ItemsSource =
            visible;

        EmptyText.Visibility =
            visible.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void RefreshHistoryState()
    {
        var state =
            ClipboardHistoryStatusService.GetState();

        HistoryBanner.Visibility =
            state == ClipboardHistoryState.Enabled
                ? Visibility.Collapsed
                : Visibility.Visible;

        if (state ==
            ClipboardHistoryState.BlockedByPolicy)
        {
            HistoryTitle.Text =
                "CLIPBOARD HISTORY IS BLOCKED BY POLICY";

            HistoryDescription.Text =
                "Windows policy currently prevents Clipboard History from being enabled.";
        }
        else
        {
            HistoryTitle.Text =
                "WINDOWS CLIPBOARD HISTORY IS OFF";

            HistoryDescription.Text =
                "Enable it in Windows Settings for the full clipboard-history experience.";
        }
    }

    public void SetPinTutorialHighlight(
        bool active)
    {
        Dispatcher.BeginInvoke(
            new Action(
                () =>
                {
                    foreach (var button
                             in FindVisualChildren<Button>(
                                 ClipList))
                    {
                        var tooltip =
                            button.ToolTip?.ToString();

                        if (string.IsNullOrWhiteSpace(tooltip) ||
                            !tooltip.Contains(
                                "Pin",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (active)
                        {
                            button.BorderBrush =
                                new SolidColorBrush(
                                    Color.FromRgb(
                                        169,
                                        216,
                                        255));

                            button.BorderThickness =
                                new Thickness(2);

                            button.Effect =
                                new DropShadowEffect
                                {
                                    Color =
                                        Color.FromRgb(
                                            169,
                                            216,
                                            255),

                                    BlurRadius = 20,
                                    ShadowDepth = 0,
                                    Opacity = 0.9,
                                    RenderingBias =
                                        RenderingBias.Performance
                                };
                        }
                        else
                        {
                            button.ClearValue(
                                BorderBrushProperty);

                            button.ClearValue(
                                BorderThicknessProperty);

                            button.ClearValue(
                                EffectProperty);
                        }
                    }
                }),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static IEnumerable<T>
        FindVisualChildren<T>(
            DependencyObject root)
        where T : DependencyObject
    {
        if (root is null)
            yield break;

        var count =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (var i = 0; i < count; i++)
        {
            var child =
                VisualTreeHelper.GetChild(
                    root,
                    i);

            if (child is T typed)
                yield return typed;

            foreach (var nested
                     in FindVisualChildren<T>(
                         child))
            {
                yield return nested;
            }
        }
    }
    private void BackButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void SearchBox_OnTextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_initialized)
            ApplyFilter();
    }

    private async void PinButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not ClipboardItem item)
        {
            return;
        }

        await _repository.SetPinnedAsync(
            item.Id,
            !item.IsPinned);

        await ReloadAsync();

        DataChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void CopyButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not ClipboardItem item)
        {
            return;
        }

        try
        {
            Clipboard.SetText(
                item.Content);

            StatusText.Text =
                "Copied";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Copy failed: {ex.Message}";
        }
    }

    private async void DeleteButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not ClipboardItem item)
        {
            return;
        }

        await _repository.DeleteAsync(
            item.Id);

        await ReloadAsync();

        DataChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OpenClipboardSettings_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        "ms-settings:clipboard",

                    UseShellExecute =
                        true
                });
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Settings failed: {ex.Message}";
        }
    }
}