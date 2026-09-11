using System.Text.Json.Serialization;

namespace WolfPatcher.Core;

public sealed class GameProfile
{
    public int SchemaVersion { get; set; }
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string LocalizationName { get; set; } = "";
    public string LocalizationVersion { get; set; } = "";
    public string PublisherLabel { get; set; } = "";
    public string ExecutableName { get; set; } = "";
    public string DataDirectory { get; set; } = "";
    public string Platform { get; set; } = "";
    public long? SteamAppId { get; set; }
    public string AuxiliaryDirectoryName { get; set; } = "TheDogDen_PTBR";
    public List<string> SupportedBaselineIds { get; set; } = [];
    public ReleaseReference CanonicalRelease { get; set; } = new();
    public List<FileHashRecord> TargetFiles { get; set; } = [];
    public List<string> RequiredTransformers { get; set; } = [];
    public Dictionary<string, object>? ToolRequirements { get; set; }
}

public sealed class ReleaseReference
{
    public int Stage { get; set; }
    public string ArchiveName { get; set; } = "";
    public long ArchiveSize { get; set; }
    public string ArchiveSha256 { get; set; } = "";
}

public sealed class BaselineDefinition
{
    public int SchemaVersion { get; set; }
    public string Id { get; set; } = "";
    public string GameProfileId { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string CompatibilityRule { get; set; } = "";
    public Dictionary<string, object>? Provenance { get; set; }
    public List<FileHashRecord> Files { get; set; } = [];
}

public sealed class FileHashRecord
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class PatchSetDefinition
{
    public int SchemaVersion { get; set; }
    public string Id { get; set; } = "";
    public string GameProfileId { get; set; } = "";
    public string BaselineId { get; set; } = "";
    public string TargetLocalizationVersion { get; set; } = "";
    public string DeltaFormat { get; set; } = "";
    public string VcdiffImplementation { get; set; } = "";
    public List<PatchFileDefinition> Files { get; set; } = [];
    public PatchTotals Totals { get; set; } = new();
}

public sealed class PatchTotals
{
    public int FileCount { get; set; }
    public long VcdiffBytes { get; set; }
    public long UnityFsMetadataBytes { get; set; }
    public long PayloadBytes { get; set; }
}

public sealed class PatchFileDefinition
{
    public string Path { get; set; } = "";
    public string Transformer { get; set; } = "";
    public long SourceSize { get; set; }
    public string SourceSha256 { get; set; } = "";
    public long TargetSize { get; set; }
    public string TargetSha256 { get; set; } = "";
    public string PatchFile { get; set; } = "";
    public long PatchSize { get; set; }
    public string PatchSha256 { get; set; } = "";
    public string? MetadataFile { get; set; }
    public long? MetadataSize { get; set; }
    public string? MetadataSha256 { get; set; }
    public TransformerOptions Options { get; set; } = new();
}

public sealed class TransformerOptions
{
    public UnityFsLzmaShape? Source { get; set; }
    public UnityFsLzmaShape? Target { get; set; }
    public string? MetadataFormat { get; set; }
}

public sealed class UnityFsLzmaShape
{
    public string Signature { get; set; } = "";
    public int Version { get; set; }
    public string UnityVersion { get; set; } = "";
    public string UnityRevision { get; set; } = "";
    public long BundleSize { get; set; }
    public int DataOffset { get; set; }
    public long UncompressedSize { get; set; }
    public long CompressedSize { get; set; }
    public int BlockFlags { get; set; }
    public int LzmaHeaderBytes { get; set; }
    public int LzmaProperty { get; set; }
    public int LzmaDictionarySize { get; set; }
    public int LzmaLc { get; set; }
    public int LzmaLp { get; set; }
    public int LzmaPb { get; set; }
    public int? LzmaPreset { get; set; }
}

public enum InstallationState
{
    Original,
    Installed,
    Mixed,
    Unknown,
    Missing
}

public enum FileState
{
    Original,
    Installed,
    Unknown,
    Missing
}

public sealed record FileInspection(string Path, FileState State, long? Size, string? Sha256);
public sealed record InspectionResult(InstallationState State, IReadOnlyList<FileInspection> Files);

public sealed class InstallManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string GameProfileId { get; set; } = "";
    public string BaselineId { get; set; } = "";
    public string PatchSetId { get; set; } = "";
    public string LocalizationVersion { get; set; } = "";
    public string Status { get; set; } = "Installed";
    public DateTimeOffset InstalledAt { get; set; }
    public DateTimeOffset? RestoredAt { get; set; }
    public List<InstallManifestFile> Files { get; set; } = [];
}

public sealed class InstallManifestFile
{
    public string Path { get; set; } = "";
    public string OriginalSha256 { get; set; } = "";
    public string InstalledSha256 { get; set; } = "";
    public string BackupRelativePath { get; set; } = "";
}
