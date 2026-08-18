using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NexOverlay.Core.Files;
using NexOverlay.Storage.Files;
using NexOverlay.Storage.Paths;

namespace NexOverlay.App;

public partial class FilesView : UserControl
{
    private readonly WorkspaceFileRepository _repository;

    private IReadOnlyList<WorkspaceFileItem> _items =
        Array.Empty<WorkspaceFileItem>();

    private bool _initialized;

    public event EventHandler? BackRequested;
    public event EventHandler? DataChanged;

    public FilesView()
    {
        InitializeComponent();

        _repository =
            new WorkspaceFileRepository(
                new AppDataPathService());

        Loaded +=
            OnLoaded;
    }

    private async void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_initialized)
            return;

        _initialized = true;

        try
        {
            await _repository.InitializeAsync();
            await ReloadAsync();
            StatusText.Text = "Workspace ready";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Storage error: {ex.Message}";
        }
    }

    private async Task ReloadAsync()
    {
        _items =
            await _repository.GetAllAsync();

        CountText.Text =
            $"{_items.Count} linked";

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query =
            SearchBox.Text.Trim();

        IEnumerable<WorkspaceFileItem> filtered =
            _items;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered =
                filtered.Where(item =>
                    item.Name.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase) ||
                    item.Path.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase));
        }

        var visible =
            filtered.ToList();

        FileList.ItemsSource =
            visible;

        EmptyText.Visibility =
            visible.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
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
    private void BackButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    public void BeginAddFileFromCommandPalette()
    {
        AddButton_OnClick(
            this,
            new RoutedEventArgs());
    }
    private async void AddButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        var dialog =
            new OpenFileDialog
            {
                CheckFileExists = true,
                Multiselect = false,
                Title = "Add file to NexOverlay"
            };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var fullPath =
                Path.GetFullPath(dialog.FileName);

            await _repository.AddAsync(
                new WorkspaceFileItem(
                    Guid.NewGuid(),
                    Path.GetFileName(fullPath),
                    fullPath));

            await ReloadAsync();

            DataChanged?.Invoke(
                this,
                EventArgs.Empty);

            StatusText.Text =
                "File linked";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Add failed: {ex.Message}";
        }
    }

    private void SearchBox_OnTextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_initialized)
            ApplyFilter();
    }

    private void OpenButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not WorkspaceFileItem item)
        {
            return;
        }

        if (!File.Exists(item.Path))
        {
            StatusText.Text =
                "File no longer exists";
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = item.Path,
                    UseShellExecute = true
                });

            StatusText.Text = "Opened";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Open failed: {ex.Message}";
        }
    }

    private void RevealButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not WorkspaceFileItem item)
        {
            return;
        }

        if (!File.Exists(item.Path))
        {
            StatusText.Text =
                "File no longer exists";
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments =
                        $"/select,\"{item.Path}\"",
                    UseShellExecute = true
                });

            StatusText.Text =
                "Shown in Explorer";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Explorer failed: {ex.Message}";
        }
    }

    private async void DeleteButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not WorkspaceFileItem item)
        {
            return;
        }

        try
        {
            await _repository.DeleteAsync(item.Id);
            await ReloadAsync();

            DataChanged?.Invoke(
                this,
                EventArgs.Empty);

            StatusText.Text =
                "Removed from workspace";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Remove failed: {ex.Message}";
        }
    }
}