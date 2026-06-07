namespace CopilotCleaner.Models;

public static class SessionColumns
{
    public const string Folder = "Folder";
    public const string WorkspaceCwd = "workspace.cwd";
    public const string WorkspaceGitRoot = "workspace.git_root";
    public const string WorkspaceBranch = "workspace.branch";
    public const string WorkspaceSummary = "workspace.summary";
    public const string MetadataOrigin = "metadata.origin";
    public const string HasCopilotShell = "Has copilotshell.json";
    public const string HasCopilotSdkSession = "In Copilot SDK list";
    public const string LastModified = "Last modified";
    public const string Size = "Size";
    public const string Path = "Path";
    public const string HasMetadata = "Has metadata";
    public const string HasWorkspace = "Has workspace";
    public const string FileCount = "File count";
    public const string DirectoryCount = "Directory count";
    public const string TopLevelFiles = "Top-level files";
    public const string TopLevelFolders = "Top-level folders";
    public const string EventsSize = "Events size";
    public const string EventsLines = "Events lines";
    public const string RequestsCount = "Requests count";
    public const string HasSessionDatabase = "Has session.db";
    public const string HasPlan = "Has plan.md";
    public const string InUseLock = "In-use lock";
    public const string CheckpointItems = "Checkpoint items";
    public const string FileStoreItems = "File-store items";
    public const string ResearchItems = "Research items";
    public const string RewindSnapshotItems = "Rewind snapshot items";

    public static readonly string[] BuiltIn =
    [
        Folder,
        WorkspaceCwd,
        WorkspaceGitRoot,
        WorkspaceBranch,
        WorkspaceSummary,
        MetadataOrigin,
        HasCopilotShell,
        HasCopilotSdkSession,
        LastModified,
        Size,
        Path,
        HasMetadata,
        HasWorkspace,
        FileCount,
        DirectoryCount,
        TopLevelFiles,
        TopLevelFolders,
        EventsSize,
        EventsLines,
        RequestsCount,
        HasSessionDatabase,
        HasPlan,
        InUseLock,
        CheckpointItems,
        FileStoreItems,
        ResearchItems,
        RewindSnapshotItems
    ];

    public static readonly string[] DefaultVisible =
    [
        HasCopilotShell,
        WorkspaceCwd,
        LastModified,
        HasCopilotSdkSession,
        WorkspaceGitRoot,
        WorkspaceBranch,
        WorkspaceSummary,
        MetadataOrigin,
        Folder,
        Size,
        InUseLock,
        HasMetadata,
        HasWorkspace,
        EventsSize,
        RequestsCount,
        HasSessionDatabase,
        HasPlan,
        FileCount,
        DirectoryCount,
        TopLevelFiles,
        TopLevelFolders,
        Path
    ];
}