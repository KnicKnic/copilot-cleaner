using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CopilotCleaner.Models;
using CopilotCleaner.Services;

namespace CopilotCleaner;

public partial class MainWindow : Window
{
    private readonly SessionScanner scanner = new();
    private readonly CopilotSdkSessionService copilotSdkSessionService = new();
    private readonly SessionFileOperations fileOperations = new();
    private readonly Dictionary<string, ListSortDirection> activeSorts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ListSortDirection> activeCopilotSdkSorts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> copilotSdkColumnLabels = new(StringComparer.OrdinalIgnoreCase);
    private const string MainLicenseResourceName = "CopilotCleaner.LICENSE";
    private const string MainLicenseFileName = "LICENSE";
    private const string LicenseNoticesResourceName = "CopilotCleaner.THIRD_PARTY_NOTICES.md";
    private const string LicenseNoticesFileName = "THIRD_PARTY_NOTICES.md";
    private readonly ObservableCollection<SessionRow> sessions = [];
    private readonly ObservableCollection<SessionGridEntry> sessionView = [];
    private readonly List<CopilotSdkSessionRow> copilotSdkSessionRows = [];
    private readonly ObservableCollection<CopilotSdkSessionRow> copilotSdkSessionView = [];
    private readonly ObservableCollection<string> aggregateColumns = [];
    private readonly ObservableCollection<string> activeAggregateColumns = [];
    private CancellationTokenSource? scanCancellation;
    private CancellationTokenSource? fileListCancellation;
    private CancellationTokenSource? copilotSdkCancellation;

    public ObservableCollection<SessionGridEntry> SessionView => sessionView;

    public ObservableCollection<CopilotSdkSessionRow> CopilotSdkSessions => copilotSdkSessionView;

    public MainWindow()
    {
        InitializeComponent();
        BindNamedControls();
        DataContext = this;

        SourcePathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "session-state");
        DestinationPathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "old_session-state");
        CopilotHomePathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot");
        AggregateColumnComboBox.ItemsSource = aggregateColumns;
        ActiveAggregateListBox.ItemsSource = activeAggregateColumns;

        RebuildColumns();
        ConfigureCopilotSdkSorting();
        LoadLicenseNotices();
        _ = ScanAsync();
        _ = LoadCopilotSdkSessionsAsync();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void BindNamedControls()
    {
        SourcePathTextBox = GetRequiredControl<TextBox>(nameof(SourcePathTextBox));
        DestinationPathTextBox = GetRequiredControl<TextBox>(nameof(DestinationPathTextBox));
        CopilotHomePathTextBox = GetRequiredControl<TextBox>(nameof(CopilotHomePathTextBox));
        ScanButton = GetRequiredControl<Button>(nameof(ScanButton));
        LoadCopilotSdkSessionsButton = GetRequiredControl<Button>(nameof(LoadCopilotSdkSessionsButton));
        DeleteSelectedCopilotSdkSessionsButton = GetRequiredControl<Button>(nameof(DeleteSelectedCopilotSdkSessionsButton));
        ScanProgressBar = GetRequiredControl<ProgressBar>(nameof(ScanProgressBar));
        CopilotSdkProgressBar = GetRequiredControl<ProgressBar>(nameof(CopilotSdkProgressBar));
        SelectAllCheckBox = GetRequiredControl<CheckBox>(nameof(SelectAllCheckBox));
        AggregateColumnComboBox = GetRequiredControl<ComboBox>(nameof(AggregateColumnComboBox));
        ActiveAggregateListBox = GetRequiredControl<ListBox>(nameof(ActiveAggregateListBox));
        SessionsGrid = GetRequiredControl<DataGrid>(nameof(SessionsGrid));
        FileListGrid = GetRequiredControl<DataGrid>(nameof(FileListGrid));
        MetadataGrid = GetRequiredControl<DataGrid>(nameof(MetadataGrid));
        CopilotSdkSessionsGrid = GetRequiredControl<DataGrid>(nameof(CopilotSdkSessionsGrid));
        SummaryTextBlock = GetRequiredControl<TextBlock>(nameof(SummaryTextBlock));
        FileListSummaryTextBlock = GetRequiredControl<TextBlock>(nameof(FileListSummaryTextBlock));
        MetadataSummaryTextBlock = GetRequiredControl<TextBlock>(nameof(MetadataSummaryTextBlock));
        CopilotSdkSummaryTextBlock = GetRequiredControl<TextBlock>(nameof(CopilotSdkSummaryTextBlock));
        LicenseSummaryTextBlock = GetRequiredControl<TextBlock>(nameof(LicenseSummaryTextBlock));
        LicenseNoticesTextBox = GetRequiredControl<TextBox>(nameof(LicenseNoticesTextBox));
        StatusTextBlock = GetRequiredControl<TextBlock>(nameof(StatusTextBlock));
    }

    private void LoadLicenseNotices()
    {
        var mainLicense = ReadEmbeddedText(MainLicenseResourceName) ?? ReadPublishedText(MainLicenseFileName);
        var thirdPartyNotices = ReadEmbeddedText(LicenseNoticesResourceName) ?? ReadPublishedText(LicenseNoticesFileName);

        if (string.IsNullOrWhiteSpace(mainLicense) && string.IsNullOrWhiteSpace(thirdPartyNotices))
        {
            LicenseNoticesTextBox.Text = "License files were not found in the application resources or next to the executable.";
            LicenseSummaryTextBlock.Text = "Licenses unavailable";
            return;
        }

        var text = string.Join(
            Environment.NewLine + Environment.NewLine,
            new[]
            {
                FormatLicenseSection("Main License", mainLicense, "The main application license was not found."),
                FormatLicenseSection("Third-Party Notices", thirdPartyNotices, "Third-party license notices were not found.")
            });

        LicenseNoticesTextBox.Text = text;
        LicenseSummaryTextBlock.Text = $"{text.Split('\n').Length:N0} lines";
    }

    private static string FormatLicenseSection(string title, string? content, string missingMessage)
    {
        return string.IsNullOrWhiteSpace(content)
            ? $"# {title}{Environment.NewLine}{Environment.NewLine}{missingMessage}"
            : $"# {title}{Environment.NewLine}{Environment.NewLine}{content.Trim()}";
    }

    private static string? ReadEmbeddedText(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string? ReadPublishedText(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private T GetRequiredControl<T>(string name) where T : Control
    {
        return this.FindControl<T>(name) ?? throw new InvalidOperationException($"Control '{name}' was not found.");
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e)
    {
        await ScanAsync();
    }

    private async void LoadCopilotSdkSessions_Click(object? sender, RoutedEventArgs e)
    {
        await LoadCopilotSdkSessionsAsync();
    }

    private void SelectSdkSessionsWithoutState_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var session in copilotSdkSessionRows)
        {
            session.IsSelected = !session.HasSessionState;
        }

        UpdateCopilotSdkSummary();
    }

    private void ClearCopilotSdkSelection_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var session in copilotSdkSessionRows)
        {
            session.IsSelected = false;
        }

        UpdateCopilotSdkSummary();
    }

    private void CopilotSdkSessionsGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space)
        {
            return;
        }

        ToggleCopilotSdkKeyboardSelection();
        e.Handled = true;
    }

    private void ToggleCopilotSdkKeyboardSelection()
    {
        var sessions = CopilotSdkSessionsGrid.SelectedItems
            .OfType<CopilotSdkSessionRow>()
            .ToList();

        if (sessions.Count == 0 && CopilotSdkSessionsGrid.SelectedItem is CopilotSdkSessionRow currentSession)
        {
            sessions.Add(currentSession);
        }

        if (sessions.Count == 0)
        {
            return;
        }

        var nextValue = sessions.Any(session => !session.IsSelected);
        foreach (var session in sessions)
        {
            session.IsSelected = nextValue;
        }

        UpdateCopilotSdkSummary();
    }

    private async void DeleteSelectedCopilotSdkSessions_Click(object? sender, RoutedEventArgs e)
    {
        var selected = copilotSdkSessionRows.Where(session => session.IsSelected).ToList();
        if (selected.Count == 0)
        {
            SetStatus("No Copilot SDK sessions selected.");
            return;
        }

        var confirmed = await ConfirmAsync(
            "Confirm Copilot SDK cleanup",
            $"Delete {selected.Count} selected Copilot session(s) using the Copilot SDK? This removes SDK-managed session data and does not move session-state folders.");

        if (!confirmed)
        {
            return;
        }

        LoadCopilotSdkSessionsButton.IsEnabled = false;
        DeleteSelectedCopilotSdkSessionsButton.IsEnabled = false;
        CopilotSdkProgressBar.IsVisible = true;
        SetStatus($"Deleting {selected.Count} Copilot SDK session(s)...");

        try
        {
            var errors = await copilotSdkSessionService.DeleteSessionsAsync(
                GetText(CopilotHomePathTextBox),
                selected.Select(session => session.SessionId).ToList(),
                CancellationToken.None);

            if (errors.Count > 0)
            {
                await ShowMessageAsync("Some Copilot SDK sessions could not be deleted", string.Join(Environment.NewLine, errors));
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
            await ShowMessageAsync("Copilot SDK cleanup failed", exception.Message);
            SetStatus("Copilot SDK cleanup failed.");
        }
        finally
        {
            CopilotSdkProgressBar.IsVisible = false;
            LoadCopilotSdkSessionsButton.IsEnabled = true;
            DeleteSelectedCopilotSdkSessionsButton.IsEnabled = true;
        }
    }

    private async void BrowseSource_Click(object? sender, RoutedEventArgs e)
    {
        await BrowseIntoAsync(SourcePathTextBox, "Choose session-state folder");
    }

    private async void BrowseDestination_Click(object? sender, RoutedEventArgs e)
    {
        await BrowseIntoAsync(DestinationPathTextBox, "Choose move target folder");
    }

    private async void BrowseCopilotHome_Click(object? sender, RoutedEventArgs e)
    {
        await BrowseIntoAsync(CopilotHomePathTextBox, "Choose Copilot home folder");
    }

    private void MoveSelected_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedRows();
        if (selected.Count == 0)
        {
            SetStatus("No sessions selected.");
            return;
        }

        var errors = fileOperations.Move(selected, GetText(DestinationPathTextBox));
        RemoveCompletedRows(selected, errors);
        _ = ReportOperationAsync("Moved", selected.Count - errors.Count, errors);
    }

    private async void DeleteSelected_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GetSelectedRows();
        if (selected.Count == 0)
        {
            SetStatus("No sessions selected.");
            return;
        }

        var confirmed = await ConfirmAsync("Confirm delete", $"Delete {selected.Count} selected session folder(s)? This cannot be undone.");
        if (!confirmed)
        {
            return;
        }

        var errors = fileOperations.Delete(selected);
        RemoveCompletedRows(selected, errors);
        await ReportOperationAsync("Deleted", selected.Count - errors.Count, errors);
    }

    private void Aggregate_Click(object? sender, RoutedEventArgs e)
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

    private void RemoveAggregate_Click(object? sender, RoutedEventArgs e)
    {
        if (ActiveAggregateListBox.SelectedItem is not string key)
        {
            SetStatus("Choose an aggregation level to remove.");
            return;
        }

        activeAggregateColumns.Remove(key);
        ApplyAggregation();
    }

    private void MoveAggregateUp_Click(object? sender, RoutedEventArgs e)
    {
        MoveSelectedAggregateLevel(-1);
    }

    private void MoveAggregateDown_Click(object? sender, RoutedEventArgs e)
    {
        MoveSelectedAggregateLevel(1);
    }

    private void ApplyAggregation()
    {
        RebuildSessionView();
        SetStatus(activeAggregateColumns.Count == 0
            ? "Aggregation cleared."
            : $"Aggregated by {string.Join(" > ", activeAggregateColumns)}.");
    }

    private void ClearAggregate_Click(object? sender, RoutedEventArgs e)
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

    private void SelectAllVisible_Changed(object? sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var isSelected = SelectAllCheckBox.IsChecked == true;
        foreach (var row in sessionView.Select(entry => entry.Row).Where(row => row is not null).Distinct())
        {
            row!.IsSelected = isSelected;
        }

        RefreshGroupSelectionStates();
        UpdateSummary();
    }

    private void SessionsGrid_KeyDown(object? sender, KeyEventArgs e)
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
        var entries = SessionsGrid.SelectedItems
            .OfType<SessionGridEntry>()
            .ToList();

        if (entries.Count == 0 && SessionsGrid.SelectedItem is SessionGridEntry currentEntry)
        {
            entries.Add(currentEntry);
        }

        if (entries.Count == 0)
        {
            return;
        }

        var nextValue = entries.Any(entry => !entry.IsSelected);
        foreach (var entry in entries)
        {
            entry.IsSelected = nextValue;
        }

        RefreshGroupSelectionStates();
        UpdateSummary();
    }

    private void SessionsGrid_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;

        var key = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        ToggleSessionSort(key);
    }

    private void ToggleSessionSort(string key)
    {
        var nextDirection = GetNextSortDirection(key);
        if (nextDirection is null)
        {
            activeSorts.Remove(key);
        }
        else
        {
            activeSorts[key] = nextDirection.Value;
        }

        RebuildSessionView();
        ApplySessionSortHeaders();
        SetStatus(activeSorts.Count == 0 ? "Sorting cleared." : $"Sorted by {string.Join(", ", GetOrderedSorts().Select(sort => sort.Key))}.");
    }

    private void SessionsGrid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        RebuildSessionView();
        ApplySessionSortHeaders();
    }

    private void CopilotSdkSessionsGrid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        ReorderCopilotSdkSessions();
        ApplyCopilotSdkSortHeaders();
    }

    private void CopilotSdkSessionsGrid_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;

        var key = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        ToggleCopilotSdkSort(key);
    }

    private async void SessionsGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateMetadataList();
        await UpdateFileListAsync();
    }

    private async Task ScanAsync()
    {
        scanCancellation?.Cancel();
        scanCancellation = new CancellationTokenSource();
        var cancellationToken = scanCancellation.Token;
        var source = GetText(SourcePathTextBox);

        foreach (var row in sessions)
        {
            row.PropertyChanged -= SessionRow_PropertyChanged;
        }

        sessions.Clear();
        sessionView.Clear();
        activeSorts.Clear();
        activeAggregateColumns.Clear();
        ClearGridSortGlyphs();
        SessionsGrid.SelectedIndex = -1;
        FileListGrid.ItemsSource = null;
        FileListSummaryTextBlock.Text = "Select a session to load its files";
        MetadataGrid.ItemsSource = null;
        MetadataSummaryTextBlock.Text = "Select a session to view metadata";
        SetStatus($"Scanning {source}...");
        ScanProgressBar.IsVisible = true;
        ScanButton.IsEnabled = false;

        var loaded = 0;
        HashSet<string>? copilotSdkSessionIds = null;
        string? copilotSdkStatus = null;

        try
        {
            try
            {
                SetStatus("Loading Copilot SDK session list...");
                copilotSdkSessionIds = await copilotSdkSessionService.GetSessionIdsAsync(GetText(CopilotHomePathTextBox), cancellationToken);
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
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        AddSessionRow(row);
                        loaded++;

                        if (loaded == 1)
                        {
                            SessionsGrid.SelectedItem = sessionView.FirstOrDefault(entry => entry.Row == row);
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
            ScanProgressBar.IsVisible = false;
            ScanButton.IsEnabled = true;
            UpdateSummary();
        }
    }

    private void AddSessionRow(SessionRow row)
    {
        row.PropertyChanged += SessionRow_PropertyChanged;
        sessions.Add(row);
        RebuildSessionView();
        UpdateSummary();
    }

    private void AddCopilotSdkSession(CopilotSdkSessionRow session)
    {
        session.PropertyChanged += CopilotSdkSession_PropertyChanged;
        copilotSdkSessionRows.Add(session);
    }

    private void RebuildColumns()
    {
        SessionsGrid.Columns.Clear();
        aggregateColumns.Clear();

        SessionsGrid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = string.Empty,
            Binding = new Binding(nameof(SessionGridEntry.IsSelected)) { Mode = BindingMode.TwoWay },
            Width = new DataGridLength(42),
            CanUserReorder = false,
            CanUserSort = false
        });

        foreach (var key in SessionColumns.DefaultVisible)
        {
            aggregateColumns.Add(key);
            SessionsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = key,
                SortMemberPath = key,
                Binding = new Binding(".") { Mode = BindingMode.OneWay, Converter = new SessionGridEntryValueConverter(key) },
                CanUserSort = true,
                FontSize = 11,
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

    private ListSortDirection? GetNextSortDirection(string key)
    {
        return GetNextSortDirection(activeSorts, key);
    }

    private static ListSortDirection? GetNextSortDirection(IReadOnlyDictionary<string, ListSortDirection> sorts, string key)
    {
        if (!sorts.TryGetValue(key, out var current))
        {
            return ListSortDirection.Ascending;
        }

        return current == ListSortDirection.Ascending ? ListSortDirection.Descending : null;
    }

    private void ConfigureCopilotSdkSorting()
    {
        copilotSdkColumnLabels.Clear();

        foreach (var column in CopilotSdkSessionsGrid.Columns)
        {
            var key = column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var label = column.Header?.ToString() ?? key;
            copilotSdkColumnLabels[key] = label;
            column.Header = label;
            column.CanUserSort = true;
        }

        ApplyCopilotSdkSortHeaders();
    }

    private void ToggleCopilotSdkSort(string key)
    {
        var nextDirection = GetNextSortDirection(activeCopilotSdkSorts, key);
        if (nextDirection is null)
        {
            activeCopilotSdkSorts.Remove(key);
        }
        else
        {
            activeCopilotSdkSorts[key] = nextDirection.Value;
        }

        RebuildCopilotSdkSessionView();
        ApplyCopilotSdkSortHeaders();
        SetStatus(activeCopilotSdkSorts.Count == 0 ? "Copilot SDK sorting cleared." : $"Copilot SDK sorted by {string.Join(", ", GetOrderedCopilotSdkSorts().Select(sort => copilotSdkColumnLabels.GetValueOrDefault(sort.Key, sort.Key)))}.");
    }

    private void ReorderCopilotSdkSessions()
    {
        if (activeCopilotSdkSorts.Count == 0)
        {
            return;
        }

        RebuildCopilotSdkSessionView();
    }

    private IReadOnlyList<(string Key, ListSortDirection Direction)> GetOrderedCopilotSdkSorts()
    {
        return CopilotSdkSessionsGrid.Columns
            .Where(column => !string.IsNullOrWhiteSpace(column.SortMemberPath) && activeCopilotSdkSorts.ContainsKey(column.SortMemberPath))
            .OrderBy(column => column.DisplayIndex)
            .Select(column => (column.SortMemberPath!, activeCopilotSdkSorts[column.SortMemberPath!]))
            .ToList();
    }

    private IReadOnlyList<CopilotSdkSessionRow> GetOrderedCopilotSdkRows()
    {
        var orderedSorts = GetOrderedCopilotSdkSorts();
        if (orderedSorts.Count == 0)
        {
            return copilotSdkSessionRows.ToList();
        }

        var comparer = Comparer<CopilotSdkSessionRow>.Create((left, right) => CompareCopilotSdkRows(left, right, orderedSorts));
        return copilotSdkSessionRows.OrderBy(row => row, comparer).ToList();
    }

    private static int CompareCopilotSdkRows(CopilotSdkSessionRow? left, CopilotSdkSessionRow? right, IReadOnlyList<(string Key, ListSortDirection Direction)> sorts)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        foreach (var sort in sorts)
        {
            var comparison = CompareValues(GetCopilotSdkSortValue(left, sort.Key), GetCopilotSdkSortValue(right, sort.Key));
            if (comparison != 0)
            {
                return sort.Direction == ListSortDirection.Ascending ? comparison : -comparison;
            }
        }

        return string.Compare(left.SessionId, right.SessionId, StringComparison.CurrentCultureIgnoreCase);
    }

    private static object? GetCopilotSdkSortValue(CopilotSdkSessionRow row, string key)
    {
        return key switch
        {
            nameof(CopilotSdkSessionRow.HasSessionState) => row.HasSessionState,
            nameof(CopilotSdkSessionRow.ModifiedTime) => row.ModifiedTime,
            nameof(CopilotSdkSessionRow.WorkingDirectory) => row.WorkingDirectory,
            nameof(CopilotSdkSessionRow.GitRoot) => row.GitRoot,
            nameof(CopilotSdkSessionRow.Branch) => row.Branch,
            nameof(CopilotSdkSessionRow.Summary) => row.Summary,
            nameof(CopilotSdkSessionRow.IsRemote) => row.IsRemote,
            nameof(CopilotSdkSessionRow.StartTime) => row.StartTime,
            nameof(CopilotSdkSessionRow.SessionId) => row.SessionId,
            nameof(CopilotSdkSessionRow.Repository) => row.Repository,
            _ => null
        };
    }

    private static int CompareValues(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left.GetType() == right.GetType() && left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        return string.Compare(left.ToString(), right.ToString(), StringComparison.CurrentCultureIgnoreCase);
    }

    private void RebuildCopilotSdkSessionView()
    {
        var selectedSession = CopilotSdkSessionsGrid.SelectedItem as CopilotSdkSessionRow;
        copilotSdkSessionView.Clear();

        foreach (var row in GetOrderedCopilotSdkRows())
        {
            copilotSdkSessionView.Add(row);
        }

        CopilotSdkSessionsGrid.SelectedItem = selectedSession is null
            ? null
            : copilotSdkSessionView.FirstOrDefault(row => ReferenceEquals(row, selectedSession));
    }

    private IReadOnlyList<(string Key, ListSortDirection Direction)> GetOrderedSorts()
    {
        return SessionsGrid.Columns
            .Where(column => !string.IsNullOrWhiteSpace(column.SortMemberPath) && activeSorts.ContainsKey(column.SortMemberPath))
            .OrderBy(column => column.DisplayIndex)
            .Select(column => (column.SortMemberPath!, activeSorts[column.SortMemberPath!]))
            .ToList();
    }

    private IReadOnlyList<SessionRow> GetOrderedRows()
    {
        var orderedSorts = GetOrderedSorts();
        if (orderedSorts.Count == 0)
        {
            return sessions.ToList();
        }

        var comparer = new SessionRowComparer(orderedSorts);
        return sessions.OrderBy(row => row, comparer).ToList();
    }

    private void RebuildSessionView()
    {
        var selectedRow = (SessionsGrid.SelectedItem as SessionGridEntry)?.Row;
        sessionView.Clear();

        var orderedRows = GetOrderedRows();
        if (activeAggregateColumns.Count == 0)
        {
            foreach (var row in orderedRows)
            {
                sessionView.Add(SessionGridEntry.ForRow(row));
            }
        }
        else
        {
            AddGroupedEntries(orderedRows, 0);
        }

        RefreshGroupSelectionStates();
        SessionsGrid.SelectedItem = selectedRow is null
            ? null
            : sessionView.FirstOrDefault(entry => entry.Row == selectedRow);
    }

    private void AddGroupedEntries(IReadOnlyList<SessionRow> rows, int level)
    {
        if (level >= activeAggregateColumns.Count)
        {
            foreach (var row in rows)
            {
                sessionView.Add(SessionGridEntry.ForRow(row));
            }

            return;
        }

        var key = activeAggregateColumns[level];
        foreach (var group in rows.GroupBy(row => NormalizeGroupValue(row[key])).OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var groupRows = group.ToList();
            sessionView.Add(SessionGridEntry.ForGroup(key, group.Key, level, groupRows));
            AddGroupedEntries(groupRows, level + 1);
        }
    }

    private static string NormalizeGroupValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(blank)" : value;
    }

    private void ClearGridSortGlyphs()
    {
        ApplySessionSortHeaders();
    }

    private void ApplySessionSortHeaders()
    {
        var orderedSorts = GetOrderedSorts();
        foreach (var column in SessionsGrid.Columns)
        {
            var key = column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            column.Header = activeSorts.TryGetValue(key, out var direction)
                ? $"{key} ({GetSortOrdinal(orderedSorts, key)} {GetSortLabel(direction)})"
                : key;
        }
    }

    private void ApplyCopilotSdkSortHeaders()
    {
        var orderedSorts = GetOrderedCopilotSdkSorts();
        foreach (var column in CopilotSdkSessionsGrid.Columns)
        {
            var key = column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var label = copilotSdkColumnLabels.GetValueOrDefault(key, key);
            column.Header = activeCopilotSdkSorts.TryGetValue(key, out var direction)
                ? $"{label} ({GetSortOrdinal(orderedSorts, key)} {GetSortLabel(direction)})"
                : label;
        }
    }

    private static int GetSortOrdinal(IReadOnlyList<(string Key, ListSortDirection Direction)> orderedSorts, string key)
    {
        for (var index = 0; index < orderedSorts.Count; index++)
        {
            if (string.Equals(orderedSorts[index].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return index + 1;
            }
        }

        return 0;
    }

    private static string GetSortLabel(ListSortDirection direction)
    {
        return direction == ListSortDirection.Ascending ? "asc" : "desc";
    }

    private async Task BrowseIntoAsync(TextBox textBox, string title)
    {
        var currentPath = GetText(textBox);
        IStorageFolder? suggestedFolder = null;
        if (Directory.Exists(currentPath))
        {
            suggestedFolder = await StorageProvider.TryGetFolderFromPathAsync(currentPath);
        }

        suggestedFolder ??= await StorageProvider.TryGetFolderFromPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = suggestedFolder
        });

        var selectedPath = folders.FirstOrDefault()?.Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            textBox.Text = selectedPath;
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

        RebuildSessionView();
        _ = UpdateFileListAsync();
        UpdateSummary();
    }

    private async Task ReportOperationAsync(string verb, int successCount, IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            SetStatus($"{verb} {successCount} session folder(s).");
            return;
        }

        await ShowMessageAsync("Some sessions could not be processed", string.Join(Environment.NewLine, errors));
        SetStatus($"{verb} {successCount} session folder(s); {errors.Count} failed.");
    }

    private void SessionRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionRow.IsSelected))
        {
            RefreshGroupSelectionStates();
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

    private void RefreshGroupSelectionStates()
    {
        foreach (var entry in sessionView.Where(entry => entry.IsGroup))
        {
            entry.RefreshSelection();
        }
    }

    private void UpdateSummary()
    {
        SummaryTextBlock.Text = $"{sessions.Count} sessions | {sessions.Count(row => row.IsSelected)} selected";
    }

    private void UpdateCopilotSdkSummary()
    {
        CopilotSdkSummaryTextBlock.Text = $"{copilotSdkSessionRows.Count} SDK sessions | {copilotSdkSessionRows.Count(session => session.IsSelected)} selected | {copilotSdkSessionRows.Count(session => !session.HasSessionState)} without session-state";
    }

    private async Task LoadCopilotSdkSessionsAsync()
    {
        copilotSdkCancellation?.Cancel();
        copilotSdkCancellation = new CancellationTokenSource();
        var cancellationToken = copilotSdkCancellation.Token;

        foreach (var session in copilotSdkSessionRows)
        {
            session.PropertyChanged -= CopilotSdkSession_PropertyChanged;
        }

        copilotSdkSessionRows.Clear();
        copilotSdkSessionView.Clear();
        UpdateCopilotSdkSummary();
        CopilotSdkProgressBar.IsVisible = true;
        LoadCopilotSdkSessionsButton.IsEnabled = false;
        SetStatus("Loading Copilot SDK sessions...");

        try
        {
            var loaded = await copilotSdkSessionService.LoadSessionsAsync(
                GetText(CopilotHomePathTextBox),
                GetText(SourcePathTextBox),
                cancellationToken);

            foreach (var session in loaded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddCopilotSdkSession(session);
            }

            RebuildCopilotSdkSessionView();
            UpdateCopilotSdkSummary();
            SetStatus($"Loaded {loaded.Count} Copilot SDK session(s).");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Copilot SDK session load canceled.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await ShowMessageAsync("Unable to load Copilot SDK sessions", exception.Message);
            SetStatus("Unable to load Copilot SDK sessions.");
        }
        finally
        {
            CopilotSdkProgressBar.IsVisible = false;
            LoadCopilotSdkSessionsButton.IsEnabled = true;
        }
    }

    private async Task UpdateFileListAsync()
    {
        if (SessionsGrid.SelectedItem is SessionGridEntry { Row: { } row })
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
            FileListSummaryTextBlock.Text = SessionsGrid.SelectedItem is SessionGridEntry { IsGroup: true }
                ? "Group selected"
                : "No session selected";
        }
    }

    private void UpdateMetadataList()
    {
        if (SessionsGrid.SelectedItem is SessionGridEntry { Row: { } row })
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
            MetadataSummaryTextBlock.Text = SessionsGrid.SelectedItem is SessionGridEntry { IsGroup: true }
                ? "Group selected"
                : "No session selected";
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var result = await ShowDialogAsync(title, message, "Yes", "No");
        return result == "Yes";
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        await ShowDialogAsync(title, message, "OK");
    }

    private async Task<string> ShowDialogAsync(string title, string message, string primaryText, string? secondaryText = null)
    {
        var body = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 0, 0, 18)
        };
        Grid.SetRow(body, 0);

        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        Grid.SetRow(buttons, 1);

        var primaryButton = CreateDialogButton(primaryText);
        buttons.Children.Add(primaryButton);
        if (secondaryText is not null)
        {
            buttons.Children.Add(CreateDialogButton(secondaryText));
        }

        var content = new Grid
        {
            Margin = new Avalonia.Thickness(18),
            RowDefinitions = new RowDefinitions("*,Auto")
        };
        content.Children.Add(body);
        content.Children.Add(buttons);

        var dialog = new Window
        {
            Title = title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = content
        };

        string? result = null;
        foreach (var button in buttons.Children.OfType<Button>())
        {
            button.Click += (_, _) =>
            {
                result = button.Content?.ToString();
                dialog.Close();
            };
        }

        await dialog.ShowDialog(this);
        return result ?? primaryText;
    }

    private static Button CreateDialogButton(string text)
    {
        return new Button
        {
            Content = text,
            MinWidth = 88,
            Margin = new Avalonia.Thickness(8, 0, 0, 0),
            Padding = new Avalonia.Thickness(14, 8)
        };
    }

    private static string GetText(TextBox textBox)
    {
        return textBox.Text?.Trim() ?? string.Empty;
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private sealed class SessionGridEntryValueConverter(string key) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is SessionGridEntry entry ? entry[key] : string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
