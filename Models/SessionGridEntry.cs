using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CopilotCleaner.Models;

public sealed class SessionGridEntry : INotifyPropertyChanged
{
    private readonly IReadOnlyList<SessionRow> rows;

    private SessionGridEntry(SessionRow row)
    {
        Row = row;
        rows = [row];
    }

    private SessionGridEntry(string groupKey, string groupName, int level, IReadOnlyList<SessionRow> rows)
    {
        GroupKey = groupKey;
        GroupName = groupName;
        GroupLevel = level;
        this.rows = rows;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SessionRow? Row { get; }

    public string? GroupKey { get; }

    public string? GroupName { get; }

    public int GroupLevel { get; }

    public bool IsGroup => Row is null;

    public bool IsSelected
    {
        get => rows.Count > 0 && rows.All(row => row.IsSelected);
        set
        {
            foreach (var row in rows)
            {
                row.IsSelected = value;
            }

            OnPropertyChanged();
        }
    }

    public string this[string key]
    {
        get
        {
            if (Row is not null)
            {
                return Row[key];
            }

            if (!string.Equals(key, GroupKey, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return $"{new string(' ', GroupLevel * 4)}{GroupKey}: {GroupName} ({rows.Count})";
        }
    }

    public static SessionGridEntry ForRow(SessionRow row)
    {
        return new SessionGridEntry(row);
    }

    public static SessionGridEntry ForGroup(string groupKey, string groupName, int level, IReadOnlyList<SessionRow> rows)
    {
        return new SessionGridEntry(groupKey, groupName, level, rows);
    }

    public void RefreshSelection()
    {
        OnPropertyChanged(nameof(IsSelected));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
