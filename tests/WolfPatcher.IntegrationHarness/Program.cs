using WolfPatcher.Core;

static string Arg(string[] args, string key)
{
    var i = Array.IndexOf(args, key);
    if (i >= 0 && i + 1 < args.Length) return args[i + 1];
    throw new ArgumentException($"Argumento ausente: {key}");
}

var packageRoot = Path.GetFullPath(Arg(args, "--package"));
var gameRoot = Path.GetFullPath(Arg(args, "--game-root"));
var xdelta = Arg(args, "--xdelta");
var xz = Arg(args, "--xz");

var loaded = ProfileLoader.Load(packageRoot,
    "profiles/hotf.profile.json",
    "baselines/HOTF_WIN_BASELINE_A.json",
    "patches/HOTF_WIN_BASELINE_A/patch_manifest.json");
var vcdiff = new Xdelta3VcdiffCodec(xdelta);
var registry = new TransformerRegistry([
    new VcdiffDirectTransformer(vcdiff),
    new UnityFsSingleLzmaTransformer(vcdiff, new XzRawLzmaCodec(xz))
]);
var engine = new PatchEngine(packageRoot, loaded.Profile, loaded.Baseline, loaded.PatchSet, registry);

await engine.VerifyPatchSetAsync();
if ((await engine.InspectAsync(gameRoot)).State != InstallationState.Original)
    throw new Exception("Fixture precisa iniciar em ORIGINAL.");
await engine.InstallAsync(gameRoot);
if ((await engine.InspectAsync(gameRoot)).State != InstallationState.Installed)
    throw new Exception("ORIGINAL -> PTBR falhou.");
await engine.RestoreAsync(gameRoot);
if ((await engine.InspectAsync(gameRoot)).State != InstallationState.Original)
    throw new Exception("PTBR -> RESTAURAÇÃO falhou.");
Console.WriteLine("WolfPatcher.IntegrationHarness: PASS (ORIGINAL -> PTBR -> ORIGINAL)");
