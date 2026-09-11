namespace WolfPatcher.Core;

public enum InstallerOperationKind
{
    Install,
    Restore
}

public enum InstallerOperationStage
{
    Preflight,
    VerifyingPatchSet,
    ApplyingChanges,
    VerifyingResult,
    Completed,
    Failed
}

public sealed record InstallerOperationProgress(
    InstallerOperationStage Stage,
    string Message);

public sealed record InstallerOperationResult(
    bool Success,
    InstallerOperationKind? Operation,
    InstallationPreflightReport Preflight,
    InstallationState FinalState,
    string Message,
    string? TechnicalDetail = null);

/// <summary>
/// Narrow write-capable abstraction around the already validated PatchEngine.
/// It keeps orchestration testable without duplicating transaction, backup,
/// rollback or transformer logic.
/// </summary>
public interface IModificationEngine
{
    Task VerifyPatchSetAsync();
    Task InstallAsync(string gameRoot);
    Task RestoreAsync(string gameRoot);
}

public sealed class PatchEngineModificationAdapter : IModificationEngine
{
    private readonly PatchEngine _engine;

    public PatchEngineModificationAdapter(PatchEngine engine)
        => _engine = engine ?? throw new ArgumentNullException(nameof(engine));

    public Task VerifyPatchSetAsync() => _engine.VerifyPatchSetAsync();
    public Task InstallAsync(string gameRoot) => _engine.InstallAsync(gameRoot);
    public Task RestoreAsync(string gameRoot) => _engine.RestoreAsync(gameRoot);
}

/// <summary>
/// Safe application-layer bridge intended for the future GUI.
/// It never decides compatibility from storefront metadata. A candidate must
/// already have a hash-backed assessment, then it is revalidated by Preflight
/// immediately before any write. PatchEngine remains the sole owner of backup,
/// transaction, rollback, patch application and restore mechanics.
/// </summary>
public sealed class InstallerOperationService
{
    private readonly IModificationEngine _engine;
    private readonly StateInspector _inspector;
    private readonly DiscoveryProfile _discoveryProfile;
    private readonly GameProfile _profile;
    private readonly BaselineDefinition _baseline;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public InstallerOperationService(
        IModificationEngine engine,
        StateInspector inspector,
        DiscoveryProfile discoveryProfile,
        GameProfile profile,
        BaselineDefinition baseline)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _discoveryProfile = discoveryProfile ?? throw new ArgumentNullException(nameof(discoveryProfile));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
    }

    public async Task<InstallerOperationResult> ExecuteAsync(
        InstallationCandidateAssessment assessment,
        long patchPayloadBytes = 0,
        IProgress<InstallerOperationProgress>? progress = null,
        IFreeSpaceProbe? freeSpaceProbe = null,
        IGameProcessProbe? processProbe = null)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        if (patchPayloadBytes < 0) throw new ArgumentOutOfRangeException(nameof(patchPayloadBytes));

        var operation = assessment.PrimaryAction switch
        {
            CandidateAction.Install => InstallerOperationKind.Install,
            CandidateAction.Restore => InstallerOperationKind.Restore,
            _ => (InstallerOperationKind?)null
        };

        if (!_operationGate.Wait(0))
        {
            var busyPreflight = await InstallationPreflightService.CheckAsync(
                _inspector,
                _discoveryProfile,
                assessment,
                requiredFreeBytes: 0,
                freeSpaceProbe,
                processProbe);

            return new InstallerOperationResult(
                Success: false,
                operation,
                busyPreflight,
                busyPreflight.CurrentState,
                "Outra operação do instalador já está em andamento.");
        }

        try
        {
            var requiredBytes = operation switch
            {
                InstallerOperationKind.Install => PreflightSpaceEstimator.EstimateInstallBytes(
                    _profile, _baseline, patchPayloadBytes),
                InstallerOperationKind.Restore => PreflightSpaceEstimator.EstimateRestoreBytes(_baseline),
                _ => 0L
            };

            progress?.Report(new InstallerOperationProgress(
                InstallerOperationStage.Preflight,
                "Verificando jogo e condições de segurança."));

            var preflight = await InstallationPreflightService.CheckAsync(
                _inspector,
                _discoveryProfile,
                assessment,
                requiredBytes,
                freeSpaceProbe,
                processProbe);

            if (operation is null || !preflight.CanProceed)
            {
                progress?.Report(new InstallerOperationProgress(
                    InstallerOperationStage.Failed,
                    "A operação foi bloqueada antes de qualquer modificação."));

                return new InstallerOperationResult(
                    Success: false,
                    operation,
                    preflight,
                    preflight.CurrentState,
                    preflight.Issues.Count == 0
                        ? "A seleção atual não possui uma operação modificadora segura."
                        : string.Join(" ", preflight.Issues.Select(x => x.Message)));
            }

            try
            {
                if (operation == InstallerOperationKind.Install)
                {
                    progress?.Report(new InstallerOperationProgress(
                        InstallerOperationStage.VerifyingPatchSet,
                        "Verificando integridade dos dados de patch."));

                    await _engine.VerifyPatchSetAsync();
                }

                progress?.Report(new InstallerOperationProgress(
                    InstallerOperationStage.ApplyingChanges,
                    operation == InstallerOperationKind.Install
                        ? "Criando backup e aplicando a localização PT-BR."
                        : "Restaurando os arquivos originais com segurança."));

                if (operation == InstallerOperationKind.Install)
                    await _engine.InstallAsync(assessment.Candidate.RootPath);
                else
                    await _engine.RestoreAsync(assessment.Candidate.RootPath);

                progress?.Report(new InstallerOperationProgress(
                    InstallerOperationStage.VerifyingResult,
                    "Verificando o estado final pelos hashes."));

                var finalState = (await _inspector.InspectAsync(assessment.Candidate.RootPath)).State;
                var expected = operation == InstallerOperationKind.Install
                    ? InstallationState.Installed
                    : InstallationState.Original;

                if (finalState != expected)
                {
                    progress?.Report(new InstallerOperationProgress(
                        InstallerOperationStage.Failed,
                        "A validação final não correspondeu ao estado esperado."));

                    return new InstallerOperationResult(
                        Success: false,
                        operation.Value,
                        preflight,
                        finalState,
                        $"Estado final inesperado: esperado {expected}, obtido {finalState}.");
                }

                progress?.Report(new InstallerOperationProgress(
                    InstallerOperationStage.Completed,
                    operation == InstallerOperationKind.Install
                        ? "Localização instalada e validada com sucesso."
                        : "Arquivos originais restaurados e validados com sucesso."));

                return new InstallerOperationResult(
                    Success: true,
                    operation.Value,
                    preflight,
                    finalState,
                    operation == InstallerOperationKind.Install
                        ? "Localização PT-BR instalada com sucesso."
                        : "Restauração concluída com sucesso.");
            }
            catch (Exception ex)
            {
                var finalState = InstallationState.Unknown;
                try
                {
                    finalState = (await _inspector.InspectAsync(assessment.Candidate.RootPath)).State;
                }
                catch
                {
                    // Preserve the original operation error. Unknown is safer
                    // than inventing a state if post-failure inspection fails.
                }

                progress?.Report(new InstallerOperationProgress(
                    InstallerOperationStage.Failed,
                    "A operação falhou. Consulte os detalhes técnicos; nenhum sucesso foi declarado."));

                return new InstallerOperationResult(
                    Success: false,
                    operation.Value,
                    preflight,
                    finalState,
                    "A operação não pôde ser concluída com segurança.",
                    ex.ToString());
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }
}
