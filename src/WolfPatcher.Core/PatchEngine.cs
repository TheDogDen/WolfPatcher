namespace WolfPatcher.Core;

public sealed class PatchEngine(
    string packageRoot,
    GameProfile profile,
    BaselineDefinition baseline,
    PatchSetDefinition patchSet,
    TransformerRegistry transformers)
{
    private readonly StateInspector _inspector = new(profile, baseline);

    public Task<InspectionResult> InspectAsync(string gameRoot, CancellationToken ct = default) =>
        _inspector.InspectAsync(gameRoot, ct);

    public async Task VerifyPatchSetAsync(CancellationToken ct = default)
    {
        foreach (var file in patchSet.Files)
        {
            var patch = CoreUtil.SafeCombine(packageRoot, file.PatchFile);
            await VerifyFileAsync(patch, file.PatchSize, file.PatchSha256, "patch", ct);
            if (file.MetadataFile is not null)
            {
                if (file.MetadataSize is null || file.MetadataSha256 is null)
                    throw new InvalidDataException($"Metadata sem tamanho/hash: {file.Path}");
                var metadata = CoreUtil.SafeCombine(packageRoot, file.MetadataFile);
                await VerifyFileAsync(metadata, file.MetadataSize.Value, file.MetadataSha256, "metadata", ct);
            }
        }
    }

    public async Task InstallAsync(string gameRoot, CancellationToken ct = default)
    {
        var initial = await InspectAsync(gameRoot, ct);
        if (initial.State != InstallationState.Original)
            throw new InvalidOperationException($"Instalação recusada. Estado atual: {initial.State}.");

        await VerifyPatchSetAsync(ct);
        var dataRoot = CoreUtil.SafeCombine(gameRoot, profile.DataDirectory);
        var auxRoot = CoreUtil.SafeCombine(gameRoot, profile.AuxiliaryDirectoryName);
        var backupRoot = CoreUtil.SafeCombine(auxRoot, $"backup/{baseline.Id}");
        var workRoot = CoreUtil.SafeCombine(auxRoot, $"work/{Guid.NewGuid():N}");
        var candidatesRoot = CoreUtil.SafeCombine(workRoot, "candidates");
        Directory.CreateDirectory(candidatesRoot);

        try
        {
            await EnsureDiskSpaceAsync(gameRoot, backupRoot);
            await EnsureBackupAsync(dataRoot, backupRoot, ct);

            foreach (var file in patchSet.Files)
            {
                ct.ThrowIfCancellationRequested();
                var source = CoreUtil.SafeCombine(dataRoot, file.Path);
                var patch = CoreUtil.SafeCombine(packageRoot, file.PatchFile);
                var metadata = file.MetadataFile is null ? null : CoreUtil.SafeCombine(packageRoot, file.MetadataFile);
                var candidate = CoreUtil.SafeCombine(candidatesRoot, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(candidate)!);
                var transformWork = CoreUtil.SafeCombine(workRoot, $"transform/{Sanitize(file.Path)}");
                var request = new TransformRequest(source, patch, metadata, candidate, file, transformWork);
                await transformers.Get(file.Transformer).ApplyAsync(request, ct);
                await VerifyFileAsync(candidate, file.TargetSize, file.TargetSha256, "candidato", ct);
            }

            var committed = new List<PatchFileDefinition>();
            try
            {
                foreach (var file in patchSet.Files)
                {
                    var candidate = CoreUtil.SafeCombine(candidatesRoot, file.Path);
                    var target = CoreUtil.SafeCombine(dataRoot, file.Path);
                    File.Move(candidate, target, true);
                    committed.Add(file);
                }
            }
            catch
            {
                await RollbackCommittedAsync(dataRoot, backupRoot, committed, ct);
                throw;
            }

            var finalState = await InspectAsync(gameRoot, ct);
            if (finalState.State != InstallationState.Installed)
            {
                await RollbackCommittedAsync(dataRoot, backupRoot, patchSet.Files, ct);
                throw new InvalidDataException("Validação final 13/13 falhou; rollback executado.");
            }

            var manifest = new InstallManifest
            {
                GameProfileId = profile.Id,
                BaselineId = baseline.Id,
                PatchSetId = patchSet.Id,
                LocalizationVersion = profile.LocalizationVersion,
                InstalledAt = DateTimeOffset.Now,
                Status = "Installed",
                Files = patchSet.Files.Select(x => new InstallManifestFile
                {
                    Path = x.Path,
                    OriginalSha256 = x.SourceSha256,
                    InstalledSha256 = x.TargetSha256,
                    BackupRelativePath = x.Path
                }).ToList()
            };
            await CoreUtil.SaveJsonAsync(CoreUtil.SafeCombine(auxRoot, "install_manifest.json"), manifest, ct);
        }
        finally
        {
            TryDeleteDirectory(workRoot);
        }
    }

    public async Task RestoreAsync(string gameRoot, CancellationToken ct = default)
    {
        var current = await InspectAsync(gameRoot, ct);
        if (current.State != InstallationState.Installed)
            throw new InvalidOperationException($"Restauração recusada. Estado atual: {current.State}. Arquivos posteriores não serão sobrescritos.");

        var dataRoot = CoreUtil.SafeCombine(gameRoot, profile.DataDirectory);
        var auxRoot = CoreUtil.SafeCombine(gameRoot, profile.AuxiliaryDirectoryName);
        var backupRoot = CoreUtil.SafeCombine(auxRoot, $"backup/{baseline.Id}");
        var workRoot = CoreUtil.SafeCombine(auxRoot, $"work/restore-{Guid.NewGuid():N}");
        var restoreCandidates = CoreUtil.SafeCombine(workRoot, "restore-candidates");
        var installedRollback = CoreUtil.SafeCombine(workRoot, "installed-rollback");
        Directory.CreateDirectory(restoreCandidates);
        Directory.CreateDirectory(installedRollback);

        try
        {
            foreach (var sourceDef in baseline.Files)
            {
                var backup = CoreUtil.SafeCombine(backupRoot, sourceDef.Path);
                await VerifyFileAsync(backup, sourceDef.Size, sourceDef.Sha256, "backup", ct);
                var candidate = CoreUtil.SafeCombine(restoreCandidates, sourceDef.Path);
                await CoreUtil.CopyVerifiedAsync(backup, candidate, sourceDef.Size, sourceDef.Sha256, ct);

                var targetDef = profile.TargetFiles.Single(x => x.Path == sourceDef.Path);
                var active = CoreUtil.SafeCombine(dataRoot, sourceDef.Path);
                var rollback = CoreUtil.SafeCombine(installedRollback, sourceDef.Path);
                await CoreUtil.CopyVerifiedAsync(active, rollback, targetDef.Size, targetDef.Sha256, ct);
            }

            var committed = new List<FileHashRecord>();
            try
            {
                foreach (var sourceDef in baseline.Files)
                {
                    var candidate = CoreUtil.SafeCombine(restoreCandidates, sourceDef.Path);
                    var active = CoreUtil.SafeCombine(dataRoot, sourceDef.Path);
                    File.Move(candidate, active, true);
                    committed.Add(sourceDef);
                }
            }
            catch
            {
                foreach (var sourceDef in committed.AsEnumerable().Reverse())
                {
                    var rollback = CoreUtil.SafeCombine(installedRollback, sourceDef.Path);
                    var active = CoreUtil.SafeCombine(dataRoot, sourceDef.Path);
                    var targetDef = profile.TargetFiles.Single(x => x.Path == sourceDef.Path);
                    await CoreUtil.CopyVerifiedAsync(rollback, active, targetDef.Size, targetDef.Sha256, ct);
                }
                throw;
            }

            var restored = await InspectAsync(gameRoot, ct);
            if (restored.State != InstallationState.Original)
            {
                foreach (var sourceDef in baseline.Files.AsEnumerable().Reverse())
                {
                    var rollback = CoreUtil.SafeCombine(installedRollback, sourceDef.Path);
                    var active = CoreUtil.SafeCombine(dataRoot, sourceDef.Path);
                    var targetDef = profile.TargetFiles.Single(x => x.Path == sourceDef.Path);
                    await CoreUtil.CopyVerifiedAsync(rollback, active, targetDef.Size, targetDef.Sha256, ct);
                }
                throw new InvalidDataException("Restauração não retornou 13/13 arquivos à baseline original; estado PT-BR restaurado.");
            }

            var manifestPath = CoreUtil.SafeCombine(auxRoot, "install_manifest.json");
            if (File.Exists(manifestPath))
            {
                var manifest = CoreUtil.LoadJson<InstallManifest>(manifestPath);
                manifest.Status = "Restored";
                manifest.RestoredAt = DateTimeOffset.Now;
                await CoreUtil.SaveJsonAsync(manifestPath, manifest, ct);
            }
        }
        finally
        {
            TryDeleteDirectory(workRoot);
        }
    }

    private async Task EnsureBackupAsync(string dataRoot, string backupRoot, CancellationToken ct)
    {
        foreach (var sourceDef in baseline.Files)
        {
            var source = CoreUtil.SafeCombine(dataRoot, sourceDef.Path);
            var backup = CoreUtil.SafeCombine(backupRoot, sourceDef.Path);
            if (File.Exists(backup))
            {
                await VerifyFileAsync(backup, sourceDef.Size, sourceDef.Sha256, "backup existente", ct);
                continue;
            }
            await CoreUtil.CopyVerifiedAsync(source, backup, sourceDef.Size, sourceDef.Sha256, ct);
        }
    }

    private async Task RollbackCommittedAsync(string dataRoot, string backupRoot, IEnumerable<PatchFileDefinition> committed,
        CancellationToken ct)
    {
        foreach (var file in committed.Reverse())
        {
            var backup = CoreUtil.SafeCombine(backupRoot, file.Path);
            var active = CoreUtil.SafeCombine(dataRoot, file.Path);
            var rollbackCandidate = active + $".wolfpatcher.rollback.{Guid.NewGuid():N}";
            await CoreUtil.CopyVerifiedAsync(backup, rollbackCandidate, file.SourceSize, file.SourceSha256, ct);
            File.Move(rollbackCandidate, active, true);
        }
    }

    private async Task EnsureDiskSpaceAsync(string gameRoot, string backupRoot)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(gameRoot)) ?? throw new InvalidDataException("Não foi possível resolver o volume do jogo.");
        var drive = new DriveInfo(root);
        long backupBytes = baseline.Files.Where(x => !File.Exists(CoreUtil.SafeCombine(backupRoot, x.Path))).Sum(x => x.Size);
        long candidateBytes = profile.TargetFiles.Sum(x => x.Size);
        long margin = 64L * 1024 * 1024;
        var required = checked(backupBytes + candidateBytes + margin);
        if (drive.AvailableFreeSpace < required)
            throw new IOException($"Espaço insuficiente. Necessário aproximadamente {required} bytes; disponível {drive.AvailableFreeSpace} bytes.");
        await Task.CompletedTask;
    }

    private static async Task VerifyFileAsync(string path, long expectedSize, string expectedSha256, string label, CancellationToken ct)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"{label} ausente: {path}", path);
        if (new FileInfo(path).Length != expectedSize) throw new InvalidDataException($"Tamanho inválido de {label}: {path}");
        var hash = await CoreUtil.Sha256Async(path, ct);
        if (!CoreUtil.StringEqualsHash(hash, expectedSha256)) throw new InvalidDataException($"SHA-256 inválido de {label}: {path}");
    }

    private static string Sanitize(string path) => string.Concat(path.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { /* QA/core: cleanup is best effort */ }
    }
}
