using WolfPatcher.Core;

static string Arg(string[] args, string key, bool required = true)
{
    var i = Array.IndexOf(args, key);
    if (i >= 0 && i + 1 < args.Length) return args[i + 1];
    if (required) throw new ArgumentException($"Argumento obrigatório ausente: {key}");
    return "";
}

if (args.Length == 0 || args.Contains("--help"))
{
    Console.WriteLine("WolfPatcher.Cli QA shell");
    Console.WriteLine("--package <dir> --game-root <dir> --xdelta <exe> --xz <exe> inspect|verify-patches|install|restore");
    return;
}

var command = args[^1];
var packageRoot = Path.GetFullPath(Arg(args, "--package"));
var gameRoot = Path.GetFullPath(Arg(args, "--game-root", command is not "verify-patches"));
var xdelta = Arg(args, "--xdelta");
var xz = Arg(args, "--xz");
var loaded = ProfileLoader.Load(packageRoot,
    "profiles/hotf.profile.json",
    "baselines/HOTF_WIN_BASELINE_A.json",
    "patches/HOTF_WIN_BASELINE_A/patch_manifest.json");
var vcdiff = new Xdelta3VcdiffCodec(xdelta);
var lzma = new XzRawLzmaCodec(xz);
var registry = new TransformerRegistry([
    new VcdiffDirectTransformer(vcdiff),
    new UnityFsSingleLzmaTransformer(vcdiff, lzma)
]);
var engine = new PatchEngine(packageRoot, loaded.Profile, loaded.Baseline, loaded.PatchSet, registry);

switch (command)
{
    case "inspect":
        var result = await engine.InspectAsync(gameRoot);
        Console.WriteLine($"STATE={result.State}");
        foreach (var f in result.Files) Console.WriteLine($"{f.State}\t{f.Path}\t{f.Size}\t{f.Sha256}");
        break;
    case "verify-patches":
        await engine.VerifyPatchSetAsync();
        Console.WriteLine("PATCH_SET=PASS");
        break;
    case "install":
        await engine.InstallAsync(gameRoot);
        Console.WriteLine("INSTALL=PASS");
        break;
    case "restore":
        await engine.RestoreAsync(gameRoot);
        Console.WriteLine("RESTORE=PASS");
        break;
    default:
        throw new ArgumentException($"Comando desconhecido: {command}");
}
