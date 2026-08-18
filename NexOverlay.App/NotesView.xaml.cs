using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NexOverlay.Core.Notes;
using NexOverlay.Storage.Notes;
using NexOverlay.Storage.Paths;

namespace NexOverlay.App;

public partial class NotesView : UserControl
{
    private readonly NoteRepository _repository;

    private IReadOnlyList<NoteItem> _items =
        Array.Empty<NoteItem>();

    private Guid? _editingId;
    private bool _initialized;

    public event EventHandler? BackRequested;
    public event EventHandler? DataChanged;

    public NotesView()
    {
        InitializeComponent();

        _repository =
            new NoteRepository(
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

        IEnumerable<NoteItem> filtered =
            _items;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered =
                filtered.Where(item =>
                    item.Title.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase) ||
                    item.Content.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase));
        }

        var visible =
            filtered.ToList();

        NoteList.ItemsSource =
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
            "NEW NOTE";

        TitleBox.Text =
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
            button.Tag is not NoteItem item)
        {
            return;
        }

        _editingId =
            item.Id;

        EditorTitle.Text =
            "EDIT NOTE";

        TitleBox.Text =
            item.Title;

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

        var item =
            new NoteItem(
                _editingId ?? Guid.NewGuid(),
                title,
                content);

        try
        {
            var wasEditing =
                _editingId.HasValue;

            await _repository.UpsertAsync(item);
            await ReloadAsync();

            DataChanged?.Invoke(
                this,
                EventArgs.Empty);

            StatusText.Text =
                wasEditing
                    ? "Note updated"
                    : "Note created";

            ShowList();
        }
        catch (Exception ex)
        {
            EditorErrorText.Text =
                ex.Message;
        }
    }

    private async void DeleteButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not NoteItem item)
        {
            return;
        }

        try
        {
            await _repository.DeleteAsync(
                item.Id);

            await ReloadAsync();

            DataChanged?.Invoke(
                this,
                EventArgs.Empty);

            StatusText.Text =
                "Note deleted";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Delete failed: {ex.Message}";
        }
    }

    private void CancelEditorButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        ShowList();
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