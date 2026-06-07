using System.IO;
using CopilotCleaner.Models;
using GitHub.Copilot;

namespace CopilotCleaner.Services;

public sealed class CopilotSdkSessionService
{
    public async Task<HashSet<string>> GetSessionIdsAsync(string copilotHomePath, CancellationToken cancellationToken)
    {
        var sessions = await ListSessionMetadataAsync(copilotHomePath, cancellationToken);
        return sessions
            .Select(session => session.SessionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<CopilotSdkSessionRow>> LoadSessionsAsync(string copilotHomePath, string sessionStatePath, CancellationToken cancellationToken)
    {
        var sessions = await ListSessionMetadataAsync(copilotHomePath, cancellationToken);
        return sessions
            .Select(session => CreateRow(session, sessionStatePath))
            .OrderByDescending(session => session.ModifiedTime)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> DeleteSessionsAsync(string copilotHomePath, IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        await using var client = CreateClient(copilotHomePath);
        await client.StartAsync(cancellationToken);

        try
        {
            foreach (var sessionId in sessionIds)
            {
                try
                {
                    await client.DeleteSessionAsync(sessionId, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    errors.Add($"{sessionId}: {exception.Message}");
                }
            }
        }
        finally
        {
            await StopClientAsync(client);
        }

        return errors;
    }

    private static async Task<IList<SessionMetadata>> ListSessionMetadataAsync(string copilotHomePath, CancellationToken cancellationToken)
    {
        await using var client = CreateClient(copilotHomePath);
        await client.StartAsync(cancellationToken);

        try
        {
            return await client.ListSessionsAsync(new SessionListFilter(), cancellationToken);
        }
        finally
        {
            await StopClientAsync(client);
        }
    }

    private static CopilotSdkSessionRow CreateRow(SessionMetadata session, string sessionStatePath)
    {
        var context = session.Context;
        var sessionId = session.SessionId ?? string.Empty;
        return new CopilotSdkSessionRow
        {
            SessionId = sessionId,
            StartTime = session.StartTime,
            ModifiedTime = session.ModifiedTime,
            Summary = Normalize(session.Summary),
            IsRemote = session.IsRemote,
            WorkingDirectory = context?.WorkingDirectory ?? string.Empty,
            GitRoot = context?.GitRoot ?? string.Empty,
            Repository = context?.Repository ?? string.Empty,
            Branch = context?.Branch ?? string.Empty,
            HasSessionState = Directory.Exists(Path.Combine(sessionStatePath, sessionId))
        };
    }

    private static CopilotClient CreateClient(string copilotHomePath)
    {
        var options = new CopilotClientOptions();
        if (!string.IsNullOrWhiteSpace(copilotHomePath))
        {
            options.BaseDirectory = copilotHomePath;
        }

        return new CopilotClient(options);
    }

    private static async Task StopClientAsync(CopilotClient client)
    {
        try
        {
            await client.StopAsync();
        }
        catch
        {
            await client.ForceStopAsync();
        }
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.ReplaceLineEndings(" ").Trim();
    }
}