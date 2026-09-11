using System.Text.Json;
using WolfPatcher.Core;

namespace WolfPatcher.Worker;

internal sealed class WorkerPackageConfig
{
    public string Profile { get; init; } = "";
    public string Baseline { get; init; } = "";
    public string PatchManifest { get; init; } = "";
    public string XdeltaPath { get; init; } = "";
    public string XzPath { get; init; } = "";

    public static WorkerPackageConfig Load(string path)
    {
        var value = JsonSerializer.Deserialize<WorkerPackageConfig>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Configuração do worker inválida.");

        foreach (var item in new[] { value.Profile, value.Baseline, value.PatchManifest, value.XdeltaPath, value.XzPath })
            if (string.IsNullOrWhiteSpace(item))
                throw new InvalidDataException("Configuração do worker incompleta.");

        return value;
    }

    public string Resolve(string packageRoot, string value)
        => Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(packageRoot, value));
}

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string? resultPath = null;
        try
        {
            var packageRoot = Path.GetFullPath(RequiredArg(args, "--package"));
            var configArg = RequiredArg(args, "--config");
            var configPath = Path.GetFullPath(Path.IsPathRooted(configArg) ? configArg : Path.Combine(packageRoot, configArg));
            var gameRoot = Path.GetFullPath(RequiredArg(args, "--game-root"));
            var actionText = RequiredArg(args, "--action");
            var progressPath = Path.GetFullPath(RequiredArg(args, "--progress"));
            resultPath = Path.GetFullPath(RequiredArg(args, "--result"));

            var requestedAction = actionText.Equals("install", StringComparison.OrdinalIgnoreCase)
                ? CandidateAction.Install
                : actionText.Equals("restore", StringComparison.OrdinalIgnoreCase)
                    ? CandidateAction.Restore
                    : throw new ArgumentException("Ação inválida. Use install ou restore.");

            var config = WorkerPackageConfig.Load(configPath);
            var loaded = ProfileLoader.Load(packageRoot, config.Profile, config.Baseline, config.PatchManifest);
            var discoveryProfile = DiscoveryProfile.Load(config.Resolve(packageRoot, config.Profile));
            var inspector = new StateInspector(loaded.Profile, loaded.Baseline);

            var state = (await inspector.InspectAsync(gameRoot)).State;
            var candidate = new GameInstallationCandidate(Storefront.Manual, gameRoot);
            var assessment = InstallationSelectionService.FromState(candidate, state);
            if (assessment.IsBlocked || assessment.PrimaryAction != requestedAction)
                throw new InvalidOperationException($"Estado atual {state} não permite a ação solicitada {requestedAction}.");

            var xdelta = config.Resolve(packageRoot, config.XdeltaPath);
            var xz = config.Resolve(packageRoot, config.XzPath);
            var vcdiff = new Xdelta3VcdiffCodec(xdelta);
            var registry = new TransformerRegistry([
                new VcdiffDirectTransformer(vcdiff),
                new UnityFsSingleLzmaTransformer(vcdiff, new XzRawLzmaCodec(xz))
            ]);
            var patchEngine = new PatchEngine(packageRoot, loaded.Profile, loaded.Baseline, loaded.PatchSet, registry);
            var service = new InstallerOperationService(
                new PatchEngineModificationAdapter(patchEngine),
                inspector,
                discoveryProfile,
                loaded.Profile,
                loaded.Baseline);

            var patchPayloadBytes = requestedAction == CandidateAction.Install
                ? Directory.EnumerateFiles(Path.Combine(packageRoot, "patches"), "*", SearchOption.AllDirectories)
                    .Sum(path => new FileInfo(path).Length)
                : 0L;

            var progress = new FileProgress(progressPath);
            var result = await service.ExecuteAsync(assessment, patchPayloadBytes, progress);
            ElevatedOperationChannel.WriteResult(
                resultPath,
                new ElevatedOperationResultRecord(result.Success, result.FinalState, result.Message, result.TechnicalDetail));
            return result.Success ? 0 : 2;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(resultPath))
            {
                try
                {
                    ElevatedOperationChannel.WriteResult(
                        resultPath,
                        new ElevatedOperationResultRecord(false, InstallationState.Unknown, "A operação elevada falhou.", ex.ToString()));
                }
                catch { }
            }
            return 3;
        }
    }

    private sealed class FileProgress(string path) : IProgress<InstallerOperationProgress>
    {
        private long _sequence;

        public void Report(InstallerOperationProgress value)
        {
            ElevatedOperationChannel.WriteProgress(
                path,
                new ElevatedOperationProgressRecord(
                    Interlocked.Increment(ref _sequence),
                    DateTimeOffset.UtcNow,
                    value.Stage,
                    value.Message));
        }
    }

    private static string RequiredArg(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        if (index >= 0 && index + 1 < args.Length) return args[index + 1];
        throw new ArgumentException($"Argumento ausente: {key}");
    }
}
