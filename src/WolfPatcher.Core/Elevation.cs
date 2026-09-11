using System.Text.Json;

namespace WolfPatcher.Core;

public enum WriteAccessStatus
{
    Writable,
    AccessDenied,
    Unavailable
}

public sealed record WriteAccessReport(
    WriteAccessStatus Status,
    string Message,
    string? TechnicalDetail = null)
{
    public bool IsWritable => Status == WriteAccessStatus.Writable;
    public bool RequiresElevation => Status == WriteAccessStatus.AccessDenied;
}

public interface IWriteAccessProbe
{
    WriteAccessReport Check(string gameRoot);
}

/// <summary>
/// Permission probe used only after the user has confirmed a modifying action
/// and a read-only preflight has passed. It creates a uniquely named empty
/// temporary file in the game root and removes it immediately. No game file is
/// opened or changed. AccessDenied is intentionally distinct from other I/O
/// failures so the GUI requests UAC only when elevation can actually help.
/// </summary>
public sealed class DirectoryWriteAccessProbe : IWriteAccessProbe
{
    public WriteAccessReport Check(string gameRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameRoot);
        var root = Path.GetFullPath(gameRoot);
        if (!Directory.Exists(root))
            return new WriteAccessReport(WriteAccessStatus.Unavailable, "A pasta da instalação não existe.");

        var probePath = Path.Combine(root, $".wolfpatcher-write-probe-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                options: FileOptions.DeleteOnClose))
            {
                stream.Flush(flushToDisk: false);
            }

            if (File.Exists(probePath))
                File.Delete(probePath);

            return new WriteAccessReport(WriteAccessStatus.Writable, "A pasta pode ser modificada sem elevação.");
        }
        catch (UnauthorizedAccessException ex)
        {
            TryDelete(probePath);
            return new WriteAccessReport(
                WriteAccessStatus.AccessDenied,
                "O Windows exige privilégios adicionais para modificar esta pasta.",
                ex.Message);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException)
        {
            TryDelete(probePath);
            return new WriteAccessReport(
                WriteAccessStatus.Unavailable,
                "Não foi possível confirmar a permissão de escrita com segurança.",
                ex.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}

public enum PrivilegeDecision
{
    ProceedNormally,
    ElevationRequired,
    Blocked
}

public sealed record PrivilegePreflightReport(
    PrivilegeDecision Decision,
    InstallationPreflightReport Preflight,
    WriteAccessReport? WriteAccess,
    string Message);

/// <summary>
/// Enforces ordering: first a completely read-only preflight/hash recheck;
/// only after that passes may the write-access probe touch a temporary file.
/// This prevents privilege probing from writing anything to an unknown or
/// changed build.
/// </summary>
public static class InstallerPrivilegeService
{
    public static async Task<PrivilegePreflightReport> CheckAsync(
        StateInspector inspector,
        DiscoveryProfile discoveryProfile,
        GameProfile profile,
        BaselineDefinition baseline,
        InstallationCandidateAssessment assessment,
        long patchPayloadBytes = 0,
        IFreeSpaceProbe? freeSpaceProbe = null,
        IGameProcessProbe? processProbe = null,
        IWriteAccessProbe? writeAccessProbe = null)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(discoveryProfile);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(assessment);
        if (patchPayloadBytes < 0) throw new ArgumentOutOfRangeException(nameof(patchPayloadBytes));

        var requiredBytes = assessment.PrimaryAction switch
        {
            CandidateAction.Install => PreflightSpaceEstimator.EstimateInstallBytes(profile, baseline, patchPayloadBytes),
            CandidateAction.Restore => PreflightSpaceEstimator.EstimateRestoreBytes(baseline),
            _ => 0L
        };

        var preflight = await InstallationPreflightService.CheckAsync(
            inspector,
            discoveryProfile,
            assessment,
            requiredBytes,
            freeSpaceProbe,
            processProbe);

        if (!preflight.CanProceed)
        {
            return new PrivilegePreflightReport(
                PrivilegeDecision.Blocked,
                preflight,
                null,
                string.Join(" ", preflight.Issues.Select(x => x.Message)));
        }

        writeAccessProbe ??= new DirectoryWriteAccessProbe();
        var writeAccess = writeAccessProbe.Check(assessment.Candidate.RootPath);
        var decision = writeAccess.Status switch
        {
            WriteAccessStatus.Writable => PrivilegeDecision.ProceedNormally,
            WriteAccessStatus.AccessDenied when OperatingSystem.IsWindows() => PrivilegeDecision.ElevationRequired,
            _ => PrivilegeDecision.Blocked
        };

        return new PrivilegePreflightReport(decision, preflight, writeAccess, writeAccess.Message);
    }
}

public sealed record ElevatedOperationProgressRecord(
    long Sequence,
    DateTimeOffset TimestampUtc,
    InstallerOperationStage Stage,
    string Message);

public sealed record ElevatedOperationResultRecord(
    bool Success,
    InstallationState FinalState,
    string Message,
    string? TechnicalDetail = null);

/// <summary>
/// Tiny file-based IPC used between the unelevated GUI and the short-lived
/// elevated worker. Files live in a unique temporary directory chosen by the
/// GUI. Writes are replace-style and contain no game data or personal data.
/// </summary>
public static class ElevatedOperationChannel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public static void WriteProgress(string path, ElevatedOperationProgressRecord value)
        => WriteAtomic(path, JsonSerializer.Serialize(value, JsonOptions));

    public static ElevatedOperationProgressRecord? TryReadProgress(string path)
        => TryRead<ElevatedOperationProgressRecord>(path);

    public static void WriteResult(string path, ElevatedOperationResultRecord value)
        => WriteAtomic(path, JsonSerializer.Serialize(value, JsonOptions));

    public static ElevatedOperationResultRecord? TryReadResult(string path)
        => TryRead<ElevatedOperationResultRecord>(path);

    private static T? TryRead<T>(string path)
    {
        try
        {
            if (!File.Exists(path)) return default;
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return default;
            return JsonSerializer.Deserialize<T>(text, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return default;
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, content);
        File.Move(temp, full, overwrite: true);
    }
}
