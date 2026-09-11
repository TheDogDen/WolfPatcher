using System.Security.Cryptography;
using System.Text.Json;

namespace WolfPatcher.Core;

public static class CoreUtil
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static T LoadJson<T>(string path) where T : class
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidDataException($"JSON vazio ou inválido: {path}");
    }

    public static async Task SaveJsonAsync<T>(string path, T value, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, ct);
    }

    public static async Task<string> Sha256Async(string path, CancellationToken ct = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string SafeCombine(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new InvalidDataException($"Caminho absoluto não permitido no perfil: {relativePath}");

        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Path traversal recusado: {relativePath}");
        return combined;
    }

    public static async Task CopyVerifiedAsync(string source, string destination, long expectedSize, string expectedSha256,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true))
        await using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true))
            await input.CopyToAsync(output, 1024 * 1024, ct);

        var fi = new FileInfo(destination);
        if (fi.Length != expectedSize || !StringEqualsHash(await Sha256Async(destination, ct), expectedSha256))
            throw new InvalidDataException($"Cópia não corresponde ao hash esperado: {destination}");
    }

    public static bool StringEqualsHash(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}

public static class ProfileLoader
{
    public static (GameProfile Profile, BaselineDefinition Baseline, PatchSetDefinition PatchSet) Load(
        string packageRoot, string profilePath, string baselinePath, string patchManifestPath)
    {
        var profile = CoreUtil.LoadJson<GameProfile>(CoreUtil.SafeCombine(packageRoot, profilePath));
        var baseline = CoreUtil.LoadJson<BaselineDefinition>(CoreUtil.SafeCombine(packageRoot, baselinePath));
        var patchSet = CoreUtil.LoadJson<PatchSetDefinition>(CoreUtil.SafeCombine(packageRoot, patchManifestPath));

        if (profile.Id != baseline.GameProfileId || profile.Id != patchSet.GameProfileId)
            throw new InvalidDataException("Perfil, baseline e patch set pertencem a jogos diferentes.");
        if (baseline.Id != patchSet.BaselineId)
            throw new InvalidDataException("Patch set não corresponde à baseline informada.");
        if (!profile.SupportedBaselineIds.Contains(baseline.Id, StringComparer.Ordinal))
            throw new InvalidDataException("Baseline não registrada como suportada pelo perfil.");
        if (patchSet.Files.Count != baseline.Files.Count || patchSet.Files.Count != profile.TargetFiles.Count)
            throw new InvalidDataException("Quantidade de arquivos divergente entre perfil, baseline e patch set.");

        foreach (var patchFile in patchSet.Files)
        {
            var src = baseline.Files.SingleOrDefault(x => x.Path == patchFile.Path)
                ?? throw new InvalidDataException($"Arquivo ausente na baseline: {patchFile.Path}");
            var dst = profile.TargetFiles.SingleOrDefault(x => x.Path == patchFile.Path)
                ?? throw new InvalidDataException($"Arquivo ausente no target: {patchFile.Path}");
            if (src.Size != patchFile.SourceSize || !CoreUtil.StringEqualsHash(src.Sha256, patchFile.SourceSha256))
                throw new InvalidDataException($"Fonte inconsistente no patch set: {patchFile.Path}");
            if (dst.Size != patchFile.TargetSize || !CoreUtil.StringEqualsHash(dst.Sha256, patchFile.TargetSha256))
                throw new InvalidDataException($"Destino inconsistente no patch set: {patchFile.Path}");
        }

        return (profile, baseline, patchSet);
    }
}
