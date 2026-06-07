using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CopilotCleaner.Models;

public sealed class CopilotSdkSessionRow : INotifyPropertyChanged
{
    private bool isSelected;

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

    public required string SessionId { get; init; }

    public DateTimeOffset StartTime { get; init; }

    public DateTimeOffset ModifiedTime { get; init; }

    public required string Summary { get; init; }

    public bool IsRemote { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string GitRoot { get; init; }

    public required string Repository { get; init; }

    public required string Branch { get; init; }

    public bool HasSessionState { get; init; }

    public string SessionState => HasSessionState ? "Yes" : "No";

    public string Remote => IsRemote ? "Yes" : "No";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}