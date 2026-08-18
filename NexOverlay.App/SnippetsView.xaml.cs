using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NexOverlay.Core.Snippets;
using NexOverlay.Storage.Paths;
using NexOverlay.Storage.Snippets;

namespace NexOverlay.App;

public partial class SnippetsView : UserControl
{
    private readonly SnippetRepository _repository;

    private IReadOnlyList<SnippetItem> _items =
        Array.Empty<SnippetItem>();

    private Guid? _editingId;
    private bool _initialized;

    public event EventHandler? BackRequested;
    public event EventHandler? DataChanged;

    public SnippetsView()
    {
        InitializeComponent();

        var paths =
            new AppDataPathService();

        _repository =
            new SnippetRepository(paths);

        Loaded += OnLoaded;
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

            StatusText.Text =
                "Storage ready";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Storage error: {ex.Message}";
        }
    }

    private async Task ReloadAsync()
    {
        _items =
            await _repository.GetAllAsync();

        CountText.Text =
            $"{_items.Count} saved";

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query =
            SearchBox.Text.Trim();

        IEnumerable<SnippetItem> filtered =
            _items;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered =
                filtered.Where(item =>
                    item.Title.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase) ||
                    item.Category.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase) ||
                    item.Content.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase));
        }

        var visible =
            filtered.ToList();

        SnippetList.ItemsSource =
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

    private void SearchBox_OnTextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (!_initialized)
            return;

        ApplyFilter();
    }

    public void BeginNewFromCommandPalette()
    {
        NewButton_OnClick(
            this,
            new RoutedEventArgs());
    }
    private void NewButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        _editingId = null;

        EditorTitle.Text =
            "NEW SNIPPET";

        TitleBox.Text =
            string.Empty;

        CategoryBox.Text =
            string.Empty;

        ContentBox.Text =
            string.Empty;

        EditorErrorText.Text =
            string.Empty;

        ShowEditor();
    }

    private void EditButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not SnippetItem item)
        {
            return;
        }

        _editingId = item.Id;

        EditorTitle.Text =
            "EDIT SNIPPET";

        TitleBox.Text =
            item.Title;

        CategoryBox.Text =
            item.Category;

        ContentBox.Text =
            item.Content;

        EditorErrorText.Text =
            string.Empty;

        ShowEditor();
    }

    private async void SaveButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        var title =
            TitleBox.Text.Trim();

        var category =
            CategoryBox.Text.Trim();

        var content =
            ContentBox.Text;

        if (string.IsNullOrWhiteSpace(title))
        {
            EditorErrorText.Text =
                "Title is required.";

            return;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            EditorErrorText.Text =
                "Content is required.";

            return;
        }

        if (string.IsNullOrWhiteSpace(category))
            category = "General";

        var item =
            new SnippetItem(
                _editingId ?? Guid.NewGuid(),
                title,
                content,
                category);

        try
        {
            await _repository.UpsertAsync(item);
            await ReloadAsync();

            DataChanged?.Invoke(
                this,
                EventArgs.Empty);

            StatusText.Text =
                _editingId.HasValue
                    ? "Snippet updated"
                    : "Snippet created";

            ShowList();
        }
        catch (Exception ex)
        {
            EditorErrorText.Text =
                ex.Message;
        }
    }

    private void CancelEditorButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        ShowList();
    }

    private async void DeleteButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not SnippetItem item)
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
                "Snippet deleted";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Delete failed: {ex.Message}";
        }
    }

    private void CopyButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not SnippetItem item)
        {
            return;
        }

        Clipboard.SetText(item.Content);

        var oldContent = button.Content;

        button.Content = "COPIED";

        var timer =
            new System.Windows.Threading.DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(850)
            };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            button.Content = oldContent;
        };

        timer.Start();
    }

    private void ShowEditor()
    {
        ListPanel.Visibility =
            Visibility.Collapsed;

        EditorPanel.Visibility =
            Visibility.Visible;

        TitleBox.Focus();
    }

    private void ShowList()
    {
        EditorPanel.Visibility =
            Visibility.Collapsed;

        ListPanel.Visibility =
            Visibility.Visible;

        _editingId = null;
        EditorErrorText.Text =
            string.Empty;
    }
}