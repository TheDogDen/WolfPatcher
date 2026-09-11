namespace WolfPatcher.Core;

public sealed class StateInspector(GameProfile profile, BaselineDefinition baseline)
{
    private readonly Dictionary<string, FileHashRecord> _source = baseline.Files.ToDictionary(x => x.Path, StringComparer.Ordinal);
    private readonly Dictionary<string, FileHashRecord> _target = profile.TargetFiles.ToDictionary(x => x.Path, StringComparer.Ordinal);

    public async Task<InspectionResult> InspectAsync(string gameRoot, CancellationToken ct = default)
    {
        var dataRoot = CoreUtil.SafeCombine(gameRoot, profile.DataDirectory);
        var results = new List<FileInspection>(_source.Count);

        foreach (var path in _source.Keys.Order(StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var file = CoreUtil.SafeCombine(dataRoot, path);
            if (!File.Exists(file))
            {
                results.Add(new FileInspection(path, FileState.Missing, null, null));
                continue;
            }

            var size = new FileInfo(file).Length;
            var hash = await CoreUtil.Sha256Async(file, ct);
            var src = _source[path];
            var dst = _target[path];
            var state = size == src.Size && CoreUtil.StringEqualsHash(hash, src.Sha256)
                ? FileState.Original
                : size == dst.Size && CoreUtil.StringEqualsHash(hash, dst.Sha256)
                    ? FileState.Installed
                    : FileState.Unknown;
            results.Add(new FileInspection(path, state, size, hash));
        }

        InstallationState aggregate;
        if (results.Any(x => x.State == FileState.Missing))
            aggregate = InstallationState.Missing;
        else if (results.All(x => x.State == FileState.Original))
            aggregate = InstallationState.Original;
        else if (results.All(x => x.State == FileState.Installed))
            aggregate = InstallationState.Installed;
        else if (results.All(x => x.State is FileState.Original or FileState.Installed))
            aggregate = InstallationState.Mixed;
        else
            aggregate = InstallationState.Unknown;

        return new InspectionResult(aggregate, results);
    }
}
