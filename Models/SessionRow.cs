using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CopilotCleaner.Models;

public sealed class SessionRow : INotifyPropertyChanged
{
    private bool isSelected;
    private IReadOnlyList<SessionFileItem> fileItems = [];
    private bool hasLoadedFileItems;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }

            isSelected = value;
            OnPropertyChanged();
        }
    }

    public required string FolderName { get; init; }

    public required string SessionPath { get; init; }

    public DateTime LastWriteTime { get; init; }

    public long SizeBytes { get; init; }

    public IReadOnlyList<SessionFileItem> FileItems
    {
        get => fileItems;
        private set
        {
            fileItems = value;
            OnPropertyChanged();
        }
    }

    public bool HasLoadedFileItems
    {
        get => hasLoadedFileItems;
        private set
        {
            hasLoadedFileItems = value;
            OnPropertyChanged();
        }
    }

    public required Dictionary<string, string> Values { get; init; }

    public string this[string key] => Values.TryGetValue(key, out var value) ? value : string.Empty;

    public object GetSortValue(string key)
    {
        return key switch
        {
            SessionColumns.Folder => FolderName,
            SessionColumns.LastModified => LastWriteTime,
            SessionColumns.Size => SizeBytes,
            SessionColumns.Path => SessionPath,
            _ => ConvertSortValue(this[key])
        };
    }

    public void NotifySelectedChanged()
    {
        OnPropertyChanged(nameof(IsSelected));
    }

    public void SetFileItems(IReadOnlyList<SessionFileItem> items)
    {
        FileItems = items;
        HasLoadedFileItems = true;
    }

    private static object ConvertSortValue(string value)
    {
        if (long.TryParse(value, out var longValue))
        {
            return longValue;
        }

        if (DateTime.TryParse(value, out var dateValue))
        {
            return dateValue;
        }

        return value;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}