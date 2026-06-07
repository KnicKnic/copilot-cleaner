using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CopilotCleaner.Models;
using CopilotCleaner.Services;
using Binding = System.Windows.Data.Binding;
using MessageBox = System.Windows.MessageBox;
using WinForms = System.Windows.Forms;

namespace CopilotCleaner;

public partial class MainWindow : Window
{
    private readonly SessionScanner scanner = new();
    private readonly CopilotSdkSessionService copilotSdkSessionService = new();
    private readonly SessionFileOperations fileOperations = new();
    private readonly Dictionary<string, ListSortDirection> activeSorts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<SessionRow> sessions = [];
    private readonly ObservableCollection<CopilotSdkSessionRow> copilotSdkSessions = [];
    private readonly ObservableCollection<string> aggregateColumns = [];
    private readonly ObservableCollection<string> activeAggregateColumns = [];
    private CancellationTokenSource? scanCancellation;
    private CancellationTokenSource? fileListCancellation;
    private CancellationTokenSource? copilotSdkCancellation;

    public ObservableCollection<SessionRow> Sessions => sessions;

    public ObservableCollection<CopilotSdkSessionRow> CopilotSdkSessions => copilotSdkSessions;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        SourcePathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "session-state");
        DestinationPathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "old_session-state");
        CopilotHomePathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot");
        AggregateColumnComboBox.ItemsSource = aggregateColumns;
        ActiveAggregateListBox.ItemsSource = activeAggregateColumns;

        RebuildColumns();
        _ = ScanAsync();
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        await ScanAsync();
    }

    private async void LoadCopilotSdkSessions_Click(object sender, RoutedEventArgs e)
    {
        await LoadCopilotSdkSessionsAsync();
    }

    private void SelectSdkSessionsWithoutState_Click(object sender, RoutedEventArgs e)
    {
        foreach (var session in copilotSdkSessions)
        {
            session.IsSelected = !session.HasSessionState;
        }

        UpdateCopilotSdkSummary();
    }

    private void ClearCopilotSdkSelection_Click(object sender, RoutedEventArgs e)
    {
        foreach (var session in copilotSdkSessions)
        {
            session.IsSelected = false;
        }

        UpdateCopilotSdkSummary();
    }

    private async void DeleteSelectedCopilotSdkSessions_Click(object sender, RoutedEventArgs e)
    {
        var selected = copilotSdkSessions.Where(session => session.IsSelected).ToList();
        if (selected.Count == 0)
        {
            SetStatus("No Copilot SDK sessions selected.");
            return;
        }

        var result = MessageBox.Show(
            $"Delete {selected.Count} selected Copilot session(s) using the Copilot SDK? This removes SDK-managed session data and does not move session-state folders.",
            "Confirm Copilot SDK cleanup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        LoadCopilotSdkSessionsButton.IsEnabled = false;
        DeleteSelectedCopilotSdkSessionsButton.IsEnabled = false;
        CopilotSdkProgressBar.Visibility = Visibility.Visible;
        SetStatus($"Deleting {selected.Count} Copilot SDK session(s)...");

        try
        {
            var errors = await copilotSdkSessionService.DeleteSessionsAsync(
                CopilotHomePathTextBox.Text.Trim(),
                selected.Select(session => session.SessionId).ToList(),
                CancellationToken.None);

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, errors), "Some Copilot SDK sessions could not be deleted", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetStatus($"Deleted {selected.Count - errors.Count} Copilot SDK session(s); {errors.Count} failed.");
            }
            else
            {
                SetStatus($"Deleted {selected.Count} Copilot SDK session(s).");
            }

            await LoadCopilotSdkSessionsAsync();
            await ScanAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MessageBox.Show(exception.Message, "Copilot SDK cleanup failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStatus("Copilot SDK cleanup failed.");
        }
        finally
        {
            CopilotSdkProgressBar.Visibility = Visibility.Collapsed;
            LoadCopilotSdkSessionsButton.IsEnabled = true;
            DeleteSelectedCopilotSdkSessionsButton.IsEnabled = true;
        }
    }

    private void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        BrowseInto(SourcePathTextBox);
    }

    private void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        BrowseInto(DestinationPathTextBox);
    }

    private void BrowseCopilotHome_Click(object sender, RoutedEventArgs e)
    {
        BrowseInto(CopilotHomePathTextBox);
    }

    private void MoveSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedRows();
        if (selected.Count == 0)
        {
            SetStatus("No sessions selected.");
            return;
        }

        var errors = fileOperations.Move(selected, DestinationPathTextBox.Text.Trim());
        RemoveCompletedRows(selected, errors);
        ReportOperation("Moved", selected.Count - errors.Count, errors);
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedRows();
        if (selected.Count == 0)
        {
            SetStatus("No sessions selected.");
            return;
        }

        var result = MessageBox.Show(
            $"Delete {selected.Count} selected session folder(s)? This cannot be undone.",
            "Confirm delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var errors = fileOperations.Delete(selected);
        RemoveCompletedRows(selected, errors);
        ReportOperation("Deleted", selected.Count - errors.Count, errors);
    }

    private void Aggregate_Click(object sender, RoutedEventArgs e)
    {
        if (AggregateColumnComboBox.SelectedItem is not string key)
        {
            SetStatus("Choose a column to aggregate.");
            return;
        }

        if (!activeAggregateColumns.Contains(key))
        {
            activeAggregateColumns.Add(key);
        }

        ApplyAggregation();
    }

    private void RemoveAggregate_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveAggregateListBox.SelectedItem is not string key)
        {
            SetStatus("Choose an aggregation level to remove.");
            return;
        }

        activeAggregateColumns.Remove(key);
        ApplyAggregation();
    }

    private void MoveAggregateUp_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedAggregateLevel(-1);
    }

    private void MoveAggregateDown_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedAggregateLevel(1);
    }

    private void ApplyAggregation()
    {
        var view = CollectionViewSource.GetDefaultView(sessions);
        view.GroupDescriptions.Clear();
        foreach (var key in activeAggregateColumns)
        {
            view.GroupDescriptions.Add(new SessionValueGroupDescription(key));
        }

        view.Refresh();
        SetStatus(activeAggregateColumns.Count == 0
            ? "Aggregation cleared."
            : $"Aggregated by {string.Join(" > ", activeAggregateColumns)}.");
    }

    private void ClearAggregate_Click(object sender, RoutedEventArgs e)
    {
        activeAggregateColumns.Clear();
        ApplyAggregation();
    }

    private void MoveSelectedAggregateLevel(int offset)
    {
        if (ActiveAggregateListBox.SelectedItem is not string key)
        {
            SetStatus("Choose an aggregation level to move.");
            return;
        }

        var oldIndex = activeAggregateColumns.IndexOf(key);
        var newIndex = oldIndex + offset;
        if (newIndex < 0 || newIndex >= activeAggregateColumns.Count)
        {
            return;
        }

        activeAggregateColumns.Move(oldIndex, newIndex);
        ActiveAggregateListBox.SelectedItem = key;
        ApplyAggregation();
    }

    private void SelectAllVisible_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var isSelected = SelectAllCheckBox.IsChecked == true;
        foreach (var row in sessions)
        {
            row.IsSelected = isSelected;
        }

        UpdateSummary();
    }

    private void GroupSelection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox checkBox || checkBox.Tag is not System.Collections.IEnumerable items)
        {
            return;
        }

        var isSelected = checkBox.IsChecked == true;
        foreach (var row in EnumerateGroupRows(items))
        {
            row.IsSelected = isSelected;
        }

        UpdateSummary();
    }

    private void SessionsGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Space)
        {
            return;
        }

        ToggleKeyboardSelection();
        e.Handled = true;
    }

    private void ToggleKeyboardSelection()
    {
        var rows = SessionsGrid.SelectedItems
            .OfType<SessionRow>()
            .ToList();

        if (rows.Count == 0 && SessionsGrid.CurrentItem is SessionRow currentRow)
        {
            rows.Add(currentRow);
        }

        if (rows.Count == 0)
        {
            return;
        }

        var nextValue = rows.Any(row => !row.IsSelected);
        foreach (var row in rows)
        {
            row.IsSelected = nextValue;
        }

        UpdateSummary();
    }

    private void SessionsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        var key = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        e.Handled = true;
        var nextDirection = GetNextSortDirection(key);
        if (nextDirection is null)
        {
            activeSorts.Remove(key);
            e.Column.SortDirection = null;
        }
        else
        {
            activeSorts[key] = nextDirection.Value;
            e.Column.SortDirection = nextDirection;
        }

        ApplySorts();
    }

    private void SessionsGrid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        ApplySorts();
    }

    private async void SessionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMetadataList();
        await UpdateFileListAsync();
    }

    private async Task ScanAsync()
    {
        scanCancellation?.Cancel();
        scanCancellation = new CancellationTokenSource();
        var cancellationToken = scanCancellation.Token;
        var source = SourcePathTextBox.Text.Trim();
        sessions.Clear();
        activeSorts.Clear();
        activeAggregateColumns.Clear();
        ClearGridSortGlyphs();
        CollectionViewSource.GetDefaultView(sessions).GroupDescriptions.Clear();
        SessionsGrid.SelectedIndex = -1;
        FileListGrid.ItemsSource = null;
        FileListSummaryTextBlock.Text = "Select a session to load its files";
        MetadataGrid.ItemsSource = null;
        MetadataSummaryTextBlock.Text = "Select a session to view metadata";
        SetStatus($"Scanning {source}...");
        ScanProgressBar.Visibility = Visibility.Visible;
        ScanButton.IsEnabled = false;

        var loaded = 0;
        HashSet<string>? copilotSdkSessionIds = null;
        string? copilotSdkStatus = null;

        try
        {
            try
            {
                SetStatus("Loading Copilot SDK session list...");
                copilotSdkSessionIds = await copilotSdkSessionService.GetSessionIdsAsync(CopilotHomePathTextBox.Text.Trim(), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                copilotSdkStatus = $" Copilot SDK session list unavailable: {exception.Message}";
            }

            SetStatus($"Scanning {source}...");
            await Task.Run(async () =>
            {
                foreach (var row in scanner.EnumerateRows(source, copilotSdkSessionIds))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Dispatcher.InvokeAsync(() =>
                    {
                        AddSessionRow(row);
                        loaded++;
                        if (loaded == 1)
                        {
                            SessionsGrid.SelectedItem = row;
                        }

                        SetStatus($"Scanning {source}... loaded {loaded} session(s).");
                    });
                }
            }, cancellationToken);

            SetStatus(loaded == 0
                ? $"No session folders found in {source}.{copilotSdkStatus}"
                : $"Loaded {loaded} session folder(s).{copilotSdkStatus}");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Scan canceled.");
        }
        finally
        {
            ScanProgressBar.Visibility = Visibility.Collapsed;
            ScanButton.IsEnabled = true;
            UpdateSummary();
        }
    }

    private void AddSessionRow(SessionRow row)
    {
        row.PropertyChanged += SessionRow_PropertyChanged;
        sessions.Add(row);
        UpdateSummary();
    }

    private void AddCopilotSdkSession(CopilotSdkSessionRow session)
    {
        session.PropertyChanged += CopilotSdkSession_PropertyChanged;
        copilotSdkSessions.Add(session);
    }

    private void RebuildColumns()
    {
        SessionsGrid.Columns.Clear();
        aggregateColumns.Clear();

        SessionsGrid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = string.Empty,
            Binding = new Binding(nameof(SessionRow.IsSelected)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = 42,
            CanUserReorder = false,
            CanUserSort = false
        });

        var keys = SessionColumns.DefaultVisible;

        foreach (var key in keys)
        {
            aggregateColumns.Add(key);
            SessionsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = key,
                SortMemberPath = key,
                Binding = new Binding { Path = new PropertyPath("[(0)]", key), Mode = BindingMode.OneWay },
                IsReadOnly = true,
                MinWidth = GetMinimumColumnWidth(key),
                Width = GetInitialColumnWidth(key)
            });
        }

        if (aggregateColumns.Count > 0)
        {
            AggregateColumnComboBox.SelectedIndex = 0;
        }
    }

    private static double GetMinimumColumnWidth(string key)
    {
        return key switch
        {
            SessionColumns.Path => 160,
            SessionColumns.TopLevelFiles or SessionColumns.TopLevelFolders => 120,
            _ => 70
        };
    }

    private static DataGridLength GetInitialColumnWidth(string key)
    {
        var width = key switch
        {
            SessionColumns.Folder => 220,
            SessionColumns.WorkspaceCwd => 260,
            SessionColumns.WorkspaceGitRoot => 260,
            SessionColumns.WorkspaceBranch => 160,
            SessionColumns.WorkspaceSummary => 320,
            SessionColumns.MetadataOrigin => 180,
            SessionColumns.HasCopilotShell => 150,
            SessionColumns.HasCopilotSdkSession => 160,
            SessionColumns.LastModified => 150,
            SessionColumns.Size => 90,
            SessionColumns.InUseLock => 130,
            SessionColumns.HasMetadata or SessionColumns.HasWorkspace => 115,
            SessionColumns.EventsSize => 105,
            SessionColumns.RequestsCount => 120,
            SessionColumns.HasSessionDatabase => 120,
            SessionColumns.HasPlan => 105,
            SessionColumns.FileCount or SessionColumns.DirectoryCount => 95,
            SessionColumns.TopLevelFiles or SessionColumns.TopLevelFolders => 260,
            SessionColumns.Path => 360,
            _ => 120
        };

        return new DataGridLength(width, DataGridLengthUnitType.Pixel);
    }

    private static IEnumerable<SessionRow> EnumerateGroupRows(System.Collections.IEnumerable items)
    {
        foreach (var item in items)
        {
            if (item is SessionRow row)
            {
                yield return row;
            }
            else if (item is CollectionViewGroup group)
            {
                foreach (var childRow in EnumerateGroupRows(group.Items))
                {
                    yield return childRow;
                }
            }
        }
    }

    private ListSortDirection? GetNextSortDirection(string key)
    {
        if (!activeSorts.TryGetValue(key, out var current))
        {
            return ListSortDirection.Ascending;
        }

        return current == ListSortDirection.Ascending ? ListSortDirection.Descending : null;
    }

    private void ApplySorts()
    {
        var view = CollectionViewSource.GetDefaultView(sessions);
        if (view is not ListCollectionView listView)
        {
            return;
        }

        var orderedSorts = SessionsGrid.Columns
            .Where(column => !string.IsNullOrWhiteSpace(column.SortMemberPath) && activeSorts.ContainsKey(column.SortMemberPath))
            .OrderBy(column => column.DisplayIndex)
            .Select(column => (column.SortMemberPath, activeSorts[column.SortMemberPath]))
            .ToList();

        listView.CustomSort = orderedSorts.Count == 0 ? null : new SessionRowComparer(orderedSorts);
        listView.Refresh();

        if (orderedSorts.Count == 0)
        {
            SetStatus("Sorting cleared.");
        }
        else
        {
            SetStatus($"Sorted by {string.Join(", ", orderedSorts.Select(sort => sort.Item1))}.");
        }
    }

    private void ClearGridSortGlyphs()
    {
        foreach (var column in SessionsGrid.Columns)
        {
            column.SortDirection = null;
        }
    }

    private void BrowseInto(System.Windows.Controls.TextBox textBox)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            InitialDirectory = Directory.Exists(textBox.Text) ? textBox.Text : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            textBox.Text = dialog.SelectedPath;
        }
    }

    private List<SessionRow> GetSelectedRows()
    {
        return sessions.Where(row => row.IsSelected).ToList();
    }

    private void RemoveCompletedRows(IReadOnlyCollection<SessionRow> selected, IReadOnlyList<string> errors)
    {
        var failedFolders = errors
            .Select(error => error.Split(':', 2)[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in selected.Where(row => !failedFolders.Contains(row.FolderName)).ToList())
        {
            row.PropertyChanged -= SessionRow_PropertyChanged;
            sessions.Remove(row);
        }

        _ = UpdateFileListAsync();
        UpdateSummary();
    }

    private void ReportOperation(string verb, int successCount, IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            SetStatus($"{verb} {successCount} session folder(s).");
            return;
        }

        MessageBox.Show(string.Join(Environment.NewLine, errors), "Some sessions could not be processed", MessageBoxButton.OK, MessageBoxImage.Warning);
        SetStatus($"{verb} {successCount} session folder(s); {errors.Count} failed.");
    }

    private void SessionRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionRow.IsSelected))
        {
            UpdateSummary();
        }
    }

    private void CopilotSdkSession_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CopilotSdkSessionRow.IsSelected))
        {
            UpdateCopilotSdkSummary();
        }
    }

    private void UpdateSummary()
    {
        SummaryTextBlock.Text = $"{sessions.Count} sessions | {sessions.Count(row => row.IsSelected)} selected";
    }

    private void UpdateCopilotSdkSummary()
    {
        CopilotSdkSummaryTextBlock.Text = $"{copilotSdkSessions.Count} SDK sessions | {copilotSdkSessions.Count(session => session.IsSelected)} selected | {copilotSdkSessions.Count(session => !session.HasSessionState)} without session-state";
    }

    private async Task LoadCopilotSdkSessionsAsync()
    {
        copilotSdkCancellation?.Cancel();
        copilotSdkCancellation = new CancellationTokenSource();
        var cancellationToken = copilotSdkCancellation.Token;

        foreach (var session in copilotSdkSessions)
        {
            session.PropertyChanged -= CopilotSdkSession_PropertyChanged;
        }

        copilotSdkSessions.Clear();
        UpdateCopilotSdkSummary();
        CopilotSdkProgressBar.Visibility = Visibility.Visible;
        LoadCopilotSdkSessionsButton.IsEnabled = false;
        SetStatus("Loading Copilot SDK sessions...");

        try
        {
            var loaded = await copilotSdkSessionService.LoadSessionsAsync(
                CopilotHomePathTextBox.Text.Trim(),
                SourcePathTextBox.Text.Trim(),
                cancellationToken);

            foreach (var session in loaded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddCopilotSdkSession(session);
            }

            UpdateCopilotSdkSummary();
            SetStatus($"Loaded {loaded.Count} Copilot SDK session(s).");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Copilot SDK session load canceled.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MessageBox.Show(exception.Message, "Unable to load Copilot SDK sessions", MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStatus("Unable to load Copilot SDK sessions.");
        }
        finally
        {
            CopilotSdkProgressBar.Visibility = Visibility.Collapsed;
            LoadCopilotSdkSessionsButton.IsEnabled = true;
        }
    }

    private async Task UpdateFileListAsync()
    {
        if (SessionsGrid.SelectedItem is SessionRow row)
        {
            fileListCancellation?.Cancel();
            fileListCancellation = new CancellationTokenSource();
            var cancellationToken = fileListCancellation.Token;

            if (row.HasLoadedFileItems)
            {
                FileListGrid.ItemsSource = row.FileItems;
                FileListSummaryTextBlock.Text = $"{row.FolderName} | {row.FileItems.Count} item(s)";
                return;
            }

            FileListGrid.ItemsSource = null;
            FileListSummaryTextBlock.Text = $"Loading files for {row.FolderName}...";

            try
            {
                var items = await Task.Run(() => scanner.GetFileItems(row.SessionPath), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                row.SetFileItems(items);
                FileListGrid.ItemsSource = items;
                FileListSummaryTextBlock.Text = $"{row.FolderName} | {items.Count} item(s)";
            }
            catch (OperationCanceledException)
            {
            }
        }
        else
        {
            fileListCancellation?.Cancel();
            FileListGrid.ItemsSource = null;
            FileListSummaryTextBlock.Text = "No session selected";
        }
    }

    private void UpdateMetadataList()
    {
        if (SessionsGrid.SelectedItem is SessionRow row)
        {
            var values = row.Values
                .OrderBy(value => value.Key, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            MetadataGrid.ItemsSource = values;
            MetadataSummaryTextBlock.Text = $"{row.FolderName} | {values.Count} value(s)";
        }
        else
        {
            MetadataGrid.ItemsSource = null;
            MetadataSummaryTextBlock.Text = "No session selected";
        }
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }
}