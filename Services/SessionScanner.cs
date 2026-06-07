using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using CopilotCleaner.Models;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace CopilotCleaner.Services;

public sealed class SessionScanner
{
    public IReadOnlyList<SessionRow> Scan(string sourcePath, IReadOnlySet<string>? copilotSdkSessionIds = null)
    {
        if (!Directory.Exists(sourcePath))
        {
            return [];
        }

        return Directory.EnumerateDirectories(sourcePath)
            .Select(path => CreateRow(path, copilotSdkSessionIds))
            .OrderByDescending(row => row.LastWriteTime)
            .ToList();
    }

    public IEnumerable<SessionRow> EnumerateRows(string sourcePath, IReadOnlySet<string>? copilotSdkSessionIds = null)
    {
        if (!Directory.Exists(sourcePath))
        {
            yield break;
        }

        foreach (var sessionPath in Directory.EnumerateDirectories(sourcePath)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(directory => directory.LastWriteTime)
            .Select(directory => directory.FullName))
        {
            yield return CreateRow(sessionPath, copilotSdkSessionIds);
        }
    }

    private static SessionRow CreateRow(string sessionPath, IReadOnlySet<string>? copilotSdkSessionIds)
    {
        var directory = new DirectoryInfo(sessionPath);
        var metadataPath = Path.Combine(sessionPath, "vscode.metadata.json");
        var copilotShellPath = Path.Combine(sessionPath, "copilotshell.json");
        var workspacePath = Path.Combine(sessionPath, "workspace.yaml");
        var topLevelFiles = GetTopLevelNames(sessionPath, directories: false);
        var topLevelFolders = GetTopLevelNames(sessionPath, directories: true);
        var sizeBytes = GetTopLevelFileSize(sessionPath);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SessionColumns.Folder] = directory.Name,
            [SessionColumns.LastModified] = directory.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
            [SessionColumns.Size] = FormatBytes(sizeBytes),
            [SessionColumns.Path] = sessionPath,
            [SessionColumns.HasMetadata] = File.Exists(metadataPath) ? "Yes" : "No",
            [SessionColumns.HasCopilotShell] = File.Exists(copilotShellPath) ? "Yes" : "No",
            [SessionColumns.HasCopilotSdkSession] = copilotSdkSessionIds is null ? "Unknown" : copilotSdkSessionIds.Contains(directory.Name) ? "Yes" : "No",
            [SessionColumns.HasWorkspace] = File.Exists(workspacePath) ? "Yes" : "No",
            [SessionColumns.FileCount] = topLevelFiles.Count.ToString(CultureInfo.CurrentCulture),
            [SessionColumns.DirectoryCount] = topLevelFolders.Count.ToString(CultureInfo.CurrentCulture),
            [SessionColumns.TopLevelFiles] = topLevelFiles.Count == 0 ? string.Empty : string.Join(", ", topLevelFiles),
            [SessionColumns.TopLevelFolders] = topLevelFolders.Count == 0 ? string.Empty : string.Join(", ", topLevelFolders),
            [SessionColumns.EventsSize] = GetFileSize(sessionPath, "events.jsonl"),
            [SessionColumns.EventsLines] = string.Empty,
            [SessionColumns.RequestsCount] = CountJsonArrayItems(Path.Combine(sessionPath, "vscode.requests.metadata.json")),
            [SessionColumns.HasSessionDatabase] = File.Exists(Path.Combine(sessionPath, "session.db")) ? "Yes" : "No",
            [SessionColumns.HasPlan] = File.Exists(Path.Combine(sessionPath, "plan.md")) ? "Yes" : "No",
            [SessionColumns.InUseLock] = GetInUseLock(sessionPath),
            [SessionColumns.CheckpointItems] = string.Empty,
            [SessionColumns.FileStoreItems] = string.Empty,
            [SessionColumns.ResearchItems] = string.Empty,
            [SessionColumns.RewindSnapshotItems] = string.Empty
        };

        if (File.Exists(metadataPath))
        {
            foreach (var item in ReadJson(metadataPath, "metadata"))
            {
                values[item.Key] = item.Value;
            }
        }

        if (File.Exists(copilotShellPath))
        {
            foreach (var item in ReadJson(copilotShellPath, "copilotshell"))
            {
                values[item.Key] = item.Value;
            }
        }

        if (File.Exists(workspacePath))
        {
            foreach (var item in ReadYaml(workspacePath, "workspace"))
            {
                values[item.Key] = item.Value;
            }
        }

        return new SessionRow
        {
            FolderName = directory.Name,
            SessionPath = sessionPath,
            LastWriteTime = directory.LastWriteTime,
            SizeBytes = sizeBytes,
            Values = values
        };
    }

    public IReadOnlyList<SessionFileItem> GetFileItems(string sessionPath)
    {
        var items = new List<SessionFileItem>();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(sessionPath, "*", SearchOption.AllDirectories))
            {
                var info = new DirectoryInfo(directory);
                items.Add(new SessionFileItem
                {
                    Name = info.Name,
                    RelativePath = Path.GetRelativePath(sessionPath, directory),
                    Kind = "Folder",
                    SizeBytes = 0,
                    Size = string.Empty,
                    LastModified = info.LastWriteTime
                });
            }

            foreach (var file in Directory.EnumerateFiles(sessionPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    items.Add(new SessionFileItem
                    {
                        Name = info.Name,
                        RelativePath = Path.GetRelativePath(sessionPath, file),
                        Kind = "File",
                        SizeBytes = info.Length,
                        Size = FormatBytes(info.Length),
                        LastModified = info.LastWriteTime
                    });
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    items.Add(new SessionFileItem
                    {
                        Name = Path.GetFileName(file),
                        RelativePath = Path.GetRelativePath(sessionPath, file),
                        Kind = "File",
                        SizeBytes = 0,
                        Size = "Unavailable",
                        LastModified = DateTime.MinValue
                    });
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            items.Add(new SessionFileItem
            {
                Name = "Unable to read folder",
                RelativePath = exception.Message,
                Kind = "Error",
                SizeBytes = 0,
                Size = string.Empty,
                LastModified = DateTime.MinValue
            });
        }

        return items
            .OrderBy(item => item.RelativePath, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> GetTopLevelNames(string sessionPath, bool directories)
    {
        try
        {
            var entries = directories ? Directory.EnumerateDirectories(sessionPath) : Directory.EnumerateFiles(sessionPath);
            return entries
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string GetFileSize(string sessionPath, string fileName)
    {
        var path = Path.Combine(sessionPath, fileName);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            return FormatBytes(new FileInfo(path).Length);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "Unavailable";
        }
    }

    private static long GetTopLevelFileSize(string sessionPath)
    {
        try
        {
            return Directory.EnumerateFiles(sessionPath, "*", SearchOption.TopDirectoryOnly).Sum(file =>
            {
                try
                {
                    return new FileInfo(file).Length;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    return 0L;
                }
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string CountLines(string path)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            return File.ReadLines(path).LongCount().ToString(CultureInfo.CurrentCulture);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "Unavailable";
        }
    }

    private static string CountJsonArrayItems(string path)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true });
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.GetArrayLength().ToString(CultureInfo.CurrentCulture)
                : string.Empty;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return "Unavailable";
        }
    }

    private static string GetInUseLock(string sessionPath)
    {
        try
        {
            return Directory.EnumerateFiles(sessionPath, "inuse.*.lock", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .FirstOrDefault() ?? string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "Unavailable";
        }
    }

    private static string CountChildItems(string path)
    {
        if (!Directory.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            return Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories).LongCount().ToString(CultureInfo.CurrentCulture);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "Unavailable";
        }
    }

    private static Dictionary<string, string> ReadJson(string path, string prefix)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true });
            FlattenJson(document.RootElement, prefix, values);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            values[$"{prefix}.parseError"] = exception.Message;
        }

        return values;
    }

    private static void FlattenJson(JsonElement element, string path, Dictionary<string, string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    FlattenJson(property.Value, $"{path}.{property.Name}", values);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenJson(item, $"{path}[{index}]", values);
                    index++;
                }

                break;
            case JsonValueKind.String:
                values[path] = element.GetString() ?? string.Empty;
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                values[path] = element.ToString();
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                values[path] = string.Empty;
                break;
        }
    }

    private static Dictionary<string, string> ReadYaml(string path, string prefix)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var reader = File.OpenText(path);
            var stream = new YamlStream();
            stream.Load(reader);

            if (stream.Documents.Count > 0 && stream.Documents[0].RootNode is not null)
            {
                FlattenYaml(stream.Documents[0].RootNode, prefix, values);
            }
        }
        catch (Exception exception) when (exception is YamlException or IOException or UnauthorizedAccessException)
        {
            foreach (var item in ReadMalformedYamlFallback(path, prefix))
            {
                values[item.Key] = item.Value;
            }

            if (values.Count == 0)
            {
                values[$"{prefix}.parseError"] = exception.Message;
            }
        }

        return values;
    }

    private static void FlattenYaml(YamlNode node, string path, Dictionary<string, string> values)
    {
        switch (node)
        {
            case YamlMappingNode mappingNode:
                foreach (var child in mappingNode.Children)
                {
                    var key = child.Key is YamlScalarNode keyNode ? keyNode.Value : child.Key.ToString();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        FlattenYaml(child.Value, $"{path}.{key}", values);
                    }
                }

                break;
            case YamlSequenceNode sequenceNode:
                for (var index = 0; index < sequenceNode.Children.Count; index++)
                {
                    FlattenYaml(sequenceNode.Children[index], $"{path}[{index}]", values);
                }

                break;
            case YamlScalarNode scalarNode:
                values[path] = NormalizeYamlValue(scalarNode.Value ?? string.Empty);
                break;
            default:
                values[path] = NormalizeYamlValue(node.ToString());
                break;
        }
    }

    private static Dictionary<string, string> ReadMalformedYamlFallback(string path, string prefix)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<(int Indent, string Key)>();
        var listIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string? multilineKey = null;
        string? multilineParent = null;
        char multilineQuote = '\0';
        StringBuilder? multilineValue = null;

        try
        {
            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.TrimEnd();
                if (multilineValue is not null)
                {
                    AppendYamlContinuation(multilineValue, line);
                    if (EndsQuotedYamlScalar(line.Trim(), multilineQuote))
                    {
                        values[$"{multilineParent}.{multilineKey}"] = NormalizeYamlValue(multilineValue.ToString());
                        multilineKey = null;
                        multilineParent = null;
                        multilineQuote = '\0';
                        multilineValue = null;
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                {
                    continue;
                }

                var indent = CountLeadingSpaces(line);
                var content = line.TrimStart();
                while (stack.Count > 0 && stack.Peek().Indent >= indent)
                {
                    stack.Pop();
                }

                var parent = stack.Count == 0 ? prefix : $"{prefix}.{string.Join('.', stack.Reverse().Select(item => item.Key))}";
                if (content.StartsWith("- ", StringComparison.Ordinal))
                {
                    var listItem = content[2..].Trim();
                    var index = listIndexes.TryGetValue(parent, out var current) ? current : 0;
                    listIndexes[parent] = index + 1;

                    if (TrySplitYamlPair(listItem, out var itemKey, out var itemValue))
                    {
                        values[$"{parent}[{index}].{itemKey}"] = NormalizeYamlValue(itemValue);
                    }
                    else if (!string.IsNullOrWhiteSpace(listItem))
                    {
                        values[$"{parent}[{index}]"] = NormalizeYamlValue(listItem);
                    }

                    continue;
                }

                if (!TrySplitYamlPair(content, out var key, out var value))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    stack.Push((indent, key));
                }
                else if (StartsUnclosedQuotedYamlScalar(value, out var quote))
                {
                    multilineKey = key;
                    multilineParent = parent;
                    multilineQuote = quote;
                    multilineValue = new StringBuilder(value);
                }
                else
                {
                    values[$"{parent}.{key}"] = NormalizeYamlValue(value);
                }
            }

            if (multilineValue is not null)
            {
                values[$"{multilineParent}.{multilineKey}"] = NormalizeYamlValue(multilineValue.ToString());
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            values[$"{prefix}.parseError"] = exception.Message;
        }

        return values;
    }

    private static bool TrySplitYamlPair(string content, out string key, out string value)
    {
        var separator = content.IndexOf(':');
        if (separator < 0)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = content[..separator].Trim().Trim('"', '\'');
        value = content[(separator + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(key);
    }

    private static bool StartsUnclosedQuotedYamlScalar(string value, out char quote)
    {
        var trimmed = value.TrimStart();
        quote = '\0';
        if (trimmed.Length == 0 || (trimmed[0] != '"' && trimmed[0] != '\''))
        {
            return false;
        }

        quote = trimmed[0];
        return !EndsQuotedYamlScalar(trimmed, quote);
    }

    private static bool EndsQuotedYamlScalar(string value, char quote)
    {
        if (value.Length <= 1 || value[^1] != quote)
        {
            return false;
        }

        if (quote == '\'')
        {
            return true;
        }

        var slashCount = 0;
        for (var index = value.Length - 2; index >= 0 && value[index] == '\\'; index--)
        {
            slashCount++;
        }

        return slashCount % 2 == 0;
    }

    private static void AppendYamlContinuation(StringBuilder builder, string line)
    {
        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append(line.TrimStart());
    }

    private static string NormalizeYamlValue(string value)
    {
        var trimmed = value.Trim();
        if ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) || (trimmed.StartsWith('\'') && trimmed.EndsWith('\'')))
        {
            trimmed = trimmed[1..^1];
        }
        else if (trimmed.StartsWith('"') || trimmed.StartsWith('\''))
        {
            trimmed = trimmed[1..];
        }

        return trimmed
            .Replace("\\r", " ", StringComparison.Ordinal)
            .Replace("\\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static int CountLeadingSpaces(string line)
    {
        var count = 0;
        foreach (var character in line)
        {
            if (character != ' ')
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}