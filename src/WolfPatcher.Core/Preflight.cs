using System.ComponentModel;
using System.Diagnostics;

namespace WolfPatcher.Core;

public enum PreflightIssueCode
{
    CandidateBlocked,
    UnsupportedAction,
    StructureInvalid,
    StateChanged,
    GameRunning,
    InsufficientDiskSpace,
    FreeSpaceUnavailable
}

public sealed record PreflightIssue(
    PreflightIssueCode Code,
    string Message);

public sealed record InstallationPreflightReport(
    InstallationCandidateAssessment Assessment,
    InstallationState CurrentState,
    long RequiredFreeBytes,
    long? AvailableFreeBytes,
    IReadOnlyList<PreflightIssue> Issues)
{
    public bool CanProceed => Issues.Count == 0;
}

public interface IFreeSpaceProbe
{
    long GetAvailableBytes(string path);
}

public sealed class DriveFreeSpaceProbe : IFreeSpaceProbe
{
    public long GetAvailableBytes(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(root))
            throw new IOException($"Não foi possível determinar a unidade de: {full}");
        return new DriveInfo(root).AvailableFreeSpace;
    }
}

public interface IGameProcessProbe
{
    bool IsRunning(string executablePath);
}

/// <summary>
/// Conservative process probe: if a process with the expected executable name
/// exists but its image path cannot be inspected, treat the game as running.
/// The installer must prefer a false positive over modifying files while the
/// game may be active.
/// </summary>
public sealed class GameProcessProbe : IGameProcessProbe
{
    public bool IsRunning(string executablePath)
    {
        var target = Path.GetFullPath(executablePath);
        var processName = Path.GetFileNameWithoutExtension(target);
        if (string.IsNullOrWhiteSpace(processName)) return false;

        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    var image = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(image))
                        return true;

                    if (Path.GetFullPath(image).Equals(target, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// Conservative space estimator used before the first write. It intentionally
/// overestimates: full original backup + four times the largest source/target
/// file as transient workspace + patch payload + fixed safety margin.
/// Game-specific values remain in profile/baseline data.
/// </summary>
public static class PreflightSpaceEstimator
{
    private const long DefaultSafetyMargin = 64L * 1024 * 1024;

    public static long EstimateInstallBytes(
        GameProfile profile,
        BaselineDefinition baseline,
        long patchPayloadBytes = 0,
        long safetyMarginBytes = DefaultSafetyMargin)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(baseline);
        if (patchPayloadBytes < 0) throw new ArgumentOutOfRangeException(nameof(patchPayloadBytes));
        if (safetyMarginBytes < 0) throw new ArgumentOutOfRangeException(nameof(safetyMarginBytes));

        var backupBytes = baseline.Files.Sum(x => checked((long)x.Size));
        var largestSource = baseline.Files.Count == 0 ? 0L : baseline.Files.Max(x => checked((long)x.Size));
        var largestTarget = profile.TargetFiles.Count == 0 ? 0L : profile.TargetFiles.Max(x => checked((long)x.Size));
        var largest = Math.Max(largestSource, largestTarget);

        return checked(backupBytes + checked(largest * 4) + patchPayloadBytes + safetyMarginBytes);
    }

    public static long EstimateRestoreBytes(
        BaselineDefinition baseline,
        long safetyMarginBytes = DefaultSafetyMargin)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (safetyMarginBytes < 0) throw new ArgumentOutOfRangeException(nameof(safetyMarginBytes));
        var largest = baseline.Files.Count == 0 ? 0L : baseline.Files.Max(x => checked((long)x.Size));
        return checked(largest * 2 + safetyMarginBytes);
    }
}

/// <summary>
/// Final read-only guard between candidate selection and any modifying engine
/// operation. Re-inspects hashes immediately before execution, validates the
/// expected action, checks that the game is not running and checks free space.
/// </summary>
public static class InstallationPreflightService
{
    public static async Task<InstallationPreflightReport> CheckAsync(
        StateInspector inspector,
        DiscoveryProfile profile,
        InstallationCandidateAssessment assessment,
        long requiredFreeBytes,
        IFreeSpaceProbe? freeSpaceProbe = null,
        IGameProcessProbe? processProbe = null)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(assessment);
        if (requiredFreeBytes < 0) throw new ArgumentOutOfRangeException(nameof(requiredFreeBytes));

        freeSpaceProbe ??= new DriveFreeSpaceProbe();
        processProbe ??= new GameProcessProbe();

        var issues = new List<PreflightIssue>();
        var root = Path.GetFullPath(assessment.Candidate.RootPath);

        if (assessment.IsBlocked)
        {
            issues.Add(new PreflightIssue(
                PreflightIssueCode.CandidateBlocked,
                "A instalação selecionada está bloqueada e não pode ser modificada."));
        }

        var expectedState = assessment.PrimaryAction switch
        {
            CandidateAction.Install => InstallationState.Original,
            CandidateAction.Restore => InstallationState.Installed,
            _ => (InstallationState?)null
        };

        if (expectedState is null)
        {
            issues.Add(new PreflightIssue(
                PreflightIssueCode.UnsupportedAction,
                "A seleção atual não possui uma ação modificadora segura."));
        }

        if (!CandidateValidator.IsGameRoot(profile, root))
        {
            issues.Add(new PreflightIssue(
                PreflightIssueCode.StructureInvalid,
                "A estrutura mínima da instalação não corresponde ao perfil selecionado."));
        }

        var currentState = (await inspector.InspectAsync(root)).State;
        if (currentState != assessment.State || (expectedState is not null && currentState != expectedState.Value))
        {
            issues.Add(new PreflightIssue(
                PreflightIssueCode.StateChanged,
                $"O estado da instalação mudou desde a seleção ({assessment.State} -> {currentState}). Reavalie antes de continuar."));
        }

        var executablePath = Path.Combine(root, profile.ExecutableName);
        if (File.Exists(executablePath) && processProbe.IsRunning(executablePath))
        {
            issues.Add(new PreflightIssue(
                PreflightIssueCode.GameRunning,
                "O jogo parece estar em execução. Feche-o antes de modificar os arquivos."));
        }

        long? available = null;
        try
        {
            available = freeSpaceProbe.GetAvailableBytes(root);
            if (available.Value < requiredFreeBytes)
            {
                issues.Add(new PreflightIssue(
                    PreflightIssueCode.InsufficientDiskSpace,
                    $"Espaço livre insuficiente. Necessário: {requiredFreeBytes} bytes; disponível: {available.Value} bytes."));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            issues.Add(new PreflightIssue(
                PreflightIssueCode.FreeSpaceUnavailable,
                "Não foi possível confirmar o espaço livre da unidade com segurança."));
        }

        return new InstallationPreflightReport(
            assessment,
            currentState,
            requiredFreeBytes,
            available,
            issues);
    }
}
