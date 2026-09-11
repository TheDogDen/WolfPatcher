namespace WolfPatcher.Core;

public enum CandidateAction
{
    Install,
    Restore,
    Diagnose,
    None
}

public sealed record InstallationCandidateAssessment(
    GameInstallationCandidate Candidate,
    InstallationState State,
    CandidateAction PrimaryAction,
    bool CanInstall,
    bool CanRestore,
    bool IsBlocked,
    string StatusText,
    string Message);

public sealed record InstallationSelectionDecision(
    IReadOnlyList<InstallationCandidateAssessment> Candidates,
    InstallationCandidateAssessment? Selected,
    bool RequiresUserChoice,
    string Reason)
{
    public bool HasActionableCandidate => Candidates.Any(x => !x.IsBlocked);
    public bool HasInstallableCandidate => Candidates.Any(x => x.CanInstall);
    public bool HasInstalledCandidate => Candidates.Any(x => x.CanRestore);
}

/// <summary>
/// Converts discovered locations into hash-backed installation assessments and
/// resolves only unambiguous safe defaults. Storefront is never used as a
/// compatibility signal or tie-breaker.
/// </summary>
public static class InstallationSelectionService
{
    public static async Task<IReadOnlyList<InstallationCandidateAssessment>> AssessAsync(
        StateInspector inspector,
        IEnumerable<GameInstallationCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(candidates);

        var deduplicated = InstallationDiscoveryService.Deduplicate(candidates);
        var results = new List<InstallationCandidateAssessment>(deduplicated.Count);

        foreach (var candidate in deduplicated)
        {
            var state = (await inspector.InspectAsync(candidate.RootPath)).State;
            results.Add(FromState(candidate, state));
        }

        return results;
    }

    public static InstallationCandidateAssessment FromState(
        GameInstallationCandidate candidate,
        InstallationState state) => state switch
    {
        InstallationState.Original => new(
            candidate,
            state,
            CandidateAction.Install,
            CanInstall: true,
            CanRestore: false,
            IsBlocked: false,
            StatusText: "COMPATÍVEL",
            Message: "Arquivos correspondem a uma baseline suportada. Pronto para instalar a localização PT-BR."),

        InstallationState.Installed => new(
            candidate,
            state,
            CandidateAction.Restore,
            CanInstall: false,
            CanRestore: true,
            IsBlocked: false,
            StatusText: "LOCALIZAÇÃO JÁ INSTALADA",
            Message: "Os arquivos correspondem ao release PT-BR conhecido. Não reaplicar patches cegamente."),

        InstallationState.Mixed => new(
            candidate,
            state,
            CandidateAction.Diagnose,
            CanInstall: false,
            CanRestore: false,
            IsBlocked: true,
            StatusText: "ESTADO MISTO",
            Message: "Há uma combinação de arquivos originais e PT-BR. Ação automática bloqueada."),

        InstallationState.Unknown => new(
            candidate,
            state,
            CandidateAction.None,
            CanInstall: false,
            CanRestore: false,
            IsBlocked: true,
            StatusText: "VERSÃO NÃO SUPORTADA",
            Message: "Os hashes não correspondem a uma baseline suportada nem ao release PT-BR conhecido. Nenhum arquivo será alterado."),

        InstallationState.Missing => new(
            candidate,
            state,
            CandidateAction.None,
            CanInstall: false,
            CanRestore: false,
            IsBlocked: true,
            StatusText: "ARQUIVOS AUSENTES",
            Message: "A instalação está incompleta ou a pasta selecionada não contém todos os arquivos necessários."),

        _ => new(
            candidate,
            state,
            CandidateAction.None,
            CanInstall: false,
            CanRestore: false,
            IsBlocked: true,
            StatusText: "ESTADO NÃO RECONHECIDO",
            Message: "O instalador não pode prosseguir com segurança.")
    };

    public static InstallationSelectionDecision Decide(
        IEnumerable<InstallationCandidateAssessment> assessments)
    {
        ArgumentNullException.ThrowIfNull(assessments);

        var all = assessments.ToArray();
        var actionable = all.Where(x => !x.IsBlocked).ToArray();

        if (actionable.Length == 0)
        {
            return new InstallationSelectionDecision(
                all,
                Selected: null,
                RequiresUserChoice: false,
                Reason: "Nenhuma instalação compatível ou gerenciável foi encontrada.");
        }

        if (actionable.Length == 1)
        {
            return new InstallationSelectionDecision(
                all,
                Selected: actionable[0],
                RequiresUserChoice: false,
                Reason: "Há exatamente uma instalação segura e não ambígua.");
        }

        return new InstallationSelectionDecision(
            all,
            Selected: null,
            RequiresUserChoice: true,
            Reason: "Mais de uma instalação válida foi encontrada. O usuário deve escolher explicitamente; a loja não é usada como desempate.");
    }
}
