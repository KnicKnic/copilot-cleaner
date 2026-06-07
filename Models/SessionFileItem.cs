namespace CopilotCleaner.Models;

public sealed class SessionFileItem
{
    public required string Name { get; init; }

    public required string RelativePath { get; init; }

    public required string Kind { get; init; }

    public long SizeBytes { get; init; }

    public required string Size { get; init; }

    public DateTime LastModified { get; init; }
}