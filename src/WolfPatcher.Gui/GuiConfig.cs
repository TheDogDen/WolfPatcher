using System.Text.Json;
using WolfPatcher.Core;

namespace WolfPatcher.Gui;

public sealed class InstallerGuiConfig
{
    public string Profile { get; init; } = "";
    public string Baseline { get; init; } = "";
    public string PatchManifest { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string LocalizationName { get; init; } = "Localização PT-BR";
    public string Publisher { get; init; } = "";
    public string XdeltaPath { get; init; } = "";
    public string XzPath { get; init; } = "";
    public string WorkerPath { get; init; } = "";

    public static InstallerGuiConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Configuração da interface não encontrada.", path);

        var config = JsonSerializer.Deserialize<InstallerGuiConfig>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Configuração da interface inválida.");

        foreach (var (value, name) in new[]
        {
            (config.Profile, nameof(Profile)),
            (config.Baseline, nameof(Baseline)),
            (config.PatchManifest, nameof(PatchManifest)),
            (config.DisplayName, nameof(DisplayName)),
            (config.LocalizationName, nameof(LocalizationName)),
            (config.XdeltaPath, nameof(XdeltaPath)),
            (config.XzPath, nameof(XzPath)),
            (config.WorkerPath, nameof(WorkerPath))
        })
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException($"Configuração sem campo obrigatório: {name}");
        }

        return config;
    }

    public string Resolve(string packageRoot, string configuredPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPath);
        return Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(packageRoot, configuredPath));
    }
}

public static class GuiPresentation
{
    public static string StoreText(Storefront store) => store switch
    {
        Storefront.Steam => "Steam",
        Storefront.Gog => "GOG",
        Storefront.Epic => "Epic Games Store",
        Storefront.Manual => "Manual",
        _ => store.ToString()
    };

    public static string ActionText(InstallationCandidateAssessment? assessment) => assessment?.PrimaryAction switch
    {
        CandidateAction.Install => "Instalar localização PT-BR",
        CandidateAction.Restore => "Restaurar arquivos originais",
        CandidateAction.Diagnose => "Ação bloqueada",
        _ => "Nenhuma ação disponível"
    };
}
