using System.IO;
using CopilotCleaner.Models;

namespace CopilotCleaner.Services;

public sealed class SessionFileOperations
{
    public IReadOnlyList<string> Move(IReadOnlyCollection<SessionRow> rows, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return rows.Select(row => $"{row.FolderName}: choose a destination folder.").ToList();
        }

        var errors = new List<string>();

        try
        {
            Directory.CreateDirectory(destinationPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return rows.Select(row => $"{row.FolderName}: {exception.Message}").ToList();
        }

        foreach (var row in rows)
        {
            try
            {
                var targetPath = GetUniqueTargetPath(destinationPath, row.FolderName);
                Directory.Move(row.SessionPath, targetPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                errors.Add($"{row.FolderName}: {exception.Message}");
            }
        }

        return errors;
    }

    public IReadOnlyList<string> Delete(IReadOnlyCollection<SessionRow> rows)
    {
        var errors = new List<string>();

        foreach (var row in rows)
        {
            try
            {
                Directory.Delete(row.SessionPath, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                errors.Add($"{row.FolderName}: {exception.Message}");
            }
        }

        return errors;
    }

    private static string GetUniqueTargetPath(string destinationPath, string folderName)
    {
        var targetPath = Path.Combine(destinationPath, folderName);
        if (!Directory.Exists(targetPath) && !File.Exists(targetPath))
        {
            return targetPath;
        }

        var suffix = 1;
        while (true)
        {
            var candidate = Path.Combine(destinationPath, $"{folderName}-{suffix:000}");
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }
}