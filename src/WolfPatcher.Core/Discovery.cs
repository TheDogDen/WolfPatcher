using Microsoft.Win32;
using System.Security;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WolfPatcher.Core;

public enum Storefront
{
    Steam,
    Gog,
    Epic,
    Manual
}

public sealed record GameInstallationCandidate(
    Storefront Store,
    string RootPath,
    string? EvidencePath = null,
    string? Evidence = null);

public sealed class DiscoveryProfile
{
    public string Id { get; init; } = "";
    public string ExecutableName { get; init; } = "";
    public string DataDirectory { get; init; } = "";
    public int? SteamAppId { get; init; }
    public IReadOnlyList<string> GogProductIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EpicAppNames { get; init; } = Array.Empty<string>();

    public static DiscoveryProfile Load(string profileJsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(profileJsonPath));
        var root = doc.RootElement;

        var id = RequiredString(root, "id");
        var executableName = RequiredString(root, "executableName");
        var dataDirectory = RequiredString(root, "dataDirectory");

        ValidateLeaf(executableName, "executableName");
        ValidateLeaf(dataDirectory, "dataDirectory");

        int? steamAppId = null;
        if (root.TryGetProperty("steamAppId", out var steam))
        {
            if (steam.ValueKind == JsonValueKind.Number && steam.TryGetInt32(out var n))
                steamAppId = n;
            else if (steam.ValueKind == JsonValueKind.String && int.TryParse(steam.GetString(), out n))
                steamAppId = n;
        }

        return new DiscoveryProfile
        {
            Id = id,
            ExecutableName = executableName,
            DataDirectory = dataDirectory,
            SteamAppId = steamAppId,
            GogProductIds = OptionalStringArray(root, "gogProductIds"),
            EpicAppNames = OptionalStringArray(root, "epicAppNames")
        };
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Perfil sem campo obrigatório válido: {name}");
        return value.GetString()!;
    }

    private static IReadOnlyList<string> OptionalStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
            .Select(x => x.GetString()!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ValidateLeaf(string value, string field)
    {
        if (Path.IsPathRooted(value) || value.Contains('/') || value.Contains('\\') || value is "." or "..")
            throw new InvalidDataException($"Campo {field} deve ser um nome relativo simples.");
    }
}

public static class InstallationDiscoveryService
{
    public static IReadOnlyList<GameInstallationCandidate> Discover(DiscoveryProfile profile)
    {
        var all = new List<GameInstallationCandidate>();
        all.AddRange(SteamLocator.Discover(profile));
        all.AddRange(GogLocator.Discover(profile));
        all.AddRange(EpicLocator.Discover(profile));
        return Deduplicate(all);
    }

    public static GameInstallationCandidate? TryManual(DiscoveryProfile profile, string rootPath)
    {
        if (!CandidateValidator.IsGameRoot(profile, rootPath))
            return null;
        return new GameInstallationCandidate(Storefront.Manual, Path.GetFullPath(rootPath), null, "Seleção manual");
    }

    public static IReadOnlyList<GameInstallationCandidate> Deduplicate(IEnumerable<GameInstallationCandidate> candidates)
    {
        var map = new Dictionary<string, GameInstallationCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            string full;
            try { full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate.RootPath)); }
            catch { continue; }
            if (!map.ContainsKey(full))
                map[full] = candidate with { RootPath = full };
        }
        return map.Values.OrderBy(x => x.Store).ThenBy(x => x.RootPath, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

public static class CandidateValidator
{
    public static bool IsGameRoot(DiscoveryProfile profile, string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) return false;
        try
        {
            var full = Path.GetFullPath(rootPath);
            return File.Exists(Path.Combine(full, profile.ExecutableName)) &&
                   Directory.Exists(Path.Combine(full, profile.DataDirectory));
        }
        catch
        {
            return false;
        }
    }
}

public static class SteamLocator
{
    private static readonly Regex VdfPair = new("\\\"(?<key>[^\\\"]+)\\\"\\s*\\\"(?<value>[^\\\"]*)\\\"", RegexOptions.Compiled);

    public static IReadOnlyList<GameInstallationCandidate> Discover(DiscoveryProfile profile)
    {
        if (!OperatingSystem.IsWindows() || profile.SteamAppId is null)
            return Array.Empty<GameInstallationCandidate>();

        var results = new List<GameInstallationCandidate>();
        foreach (var root in FindSteamRoots())
            results.AddRange(DiscoverFromSteamRoot(profile, root));
        return InstallationDiscoveryService.Deduplicate(results);
    }

    public static IReadOnlyList<GameInstallationCandidate> DiscoverFromSteamRoot(DiscoveryProfile profile, string steamRoot)
    {
        if (profile.SteamAppId is null || !Directory.Exists(steamRoot))
            return Array.Empty<GameInstallationCandidate>();

        var results = new List<GameInstallationCandidate>();
        foreach (var library in FindLibrariesFromRoot(steamRoot))
        {
            var manifest = Path.Combine(library, "steamapps", $"appmanifest_{profile.SteamAppId.Value}.acf");
            if (!File.Exists(manifest)) continue;

            string? installDir = null;
            try
            {
                installDir = ReadVdfValue(File.ReadAllText(manifest), "installdir");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            if (string.IsNullOrWhiteSpace(installDir)) continue;
            var gameRoot = Path.Combine(library, "steamapps", "common", installDir);
            if (!CandidateValidator.IsGameRoot(profile, gameRoot)) continue;

            results.Add(new GameInstallationCandidate(
                Storefront.Steam,
                Path.GetFullPath(gameRoot),
                manifest,
                $"Steam AppID {profile.SteamAppId.Value}"));
        }
        return InstallationDiscoveryService.Deduplicate(results);
    }

    public static IReadOnlyList<string> FindLibrariesFromRoot(string steamRoot)
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TryAddDirectory(libraries, steamRoot);

        var file = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(file)) return libraries.ToArray();

        try
        {
            var text = File.ReadAllText(file);
            foreach (Match m in VdfPair.Matches(text))
            {
                var key = m.Groups["key"].Value;
                var value = UnescapeVdfPath(m.Groups["value"].Value);
                if (key.Equals("path", StringComparison.OrdinalIgnoreCase) || int.TryParse(key, out _))
                    TryAddDirectory(libraries, value);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return libraries.ToArray();
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> FindSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(pf86)) TryAddDirectory(roots, Path.Combine(pf86, "Steam"));

        TryRegistrySteamRoot(roots, RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath");
        TryRegistrySteamRoot(roots, RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamExe", useParent: true);
        TryRegistrySteamRoot(roots, RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Valve\Steam", "InstallPath");
        TryRegistrySteamRoot(roots, RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Valve\Steam", "InstallPath");

        return roots;
    }

    [SupportedOSPlatform("windows")]
    private static void TryRegistrySteamRoot(HashSet<string> roots, RegistryHive hive, RegistryView view,
        string keyPath, string valueName, bool useParent = false)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(keyPath);
            var value = key?.GetValue(valueName) as string;
            if (string.IsNullOrWhiteSpace(value)) return;
            if (useParent) value = Path.GetDirectoryName(value);
            TryAddDirectory(roots, value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PlatformNotSupportedException) { }
    }

    private static string? ReadVdfValue(string text, string wantedKey)
    {
        foreach (Match m in VdfPair.Matches(text))
            if (m.Groups["key"].Value.Equals(wantedKey, StringComparison.OrdinalIgnoreCase))
                return m.Groups["value"].Value.Replace("\\\\", "\\");
        return null;
    }

    private static string UnescapeVdfPath(string value) => value.Replace("\\\\", "\\");

    private static void TryAddDirectory(HashSet<string> set, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var full = Path.GetFullPath(path);
            if (Directory.Exists(full)) set.Add(Path.TrimEndingDirectorySeparator(full));
        }
        catch { }
    }
}

public static class EpicLocator
{
    public static IReadOnlyList<GameInstallationCandidate> Discover(DiscoveryProfile profile)
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<GameInstallationCandidate>();
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(common)) return Array.Empty<GameInstallationCandidate>();
        return DiscoverFromManifestDirectory(profile,
            Path.Combine(common, "Epic", "EpicGamesLauncher", "Data", "Manifests"));
    }

    public static IReadOnlyList<GameInstallationCandidate> DiscoverFromManifestDirectory(DiscoveryProfile profile, string manifestDirectory)
    {
        if (!Directory.Exists(manifestDirectory)) return Array.Empty<GameInstallationCandidate>();
        var results = new List<GameInstallationCandidate>();

        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(manifestDirectory, "*.item", SearchOption.TopDirectoryOnly).ToArray(); }
        catch { return Array.Empty<GameInstallationCandidate>(); }

        foreach (var file in files)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                var install = GetString(root, "InstallLocation");
                if (!CandidateValidator.IsGameRoot(profile, install)) continue;

                if (profile.EpicAppNames.Count > 0)
                {
                    var names = new[]
                    {
                        GetString(root, "AppName"), GetString(root, "ArtifactId"),
                        GetString(root, "CatalogItemId"), GetString(root, "DisplayName")
                    }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                    if (!names.Any(n => profile.EpicAppNames.Contains(n!, StringComparer.OrdinalIgnoreCase)))
                        continue;
                }

                var display = GetString(root, "DisplayName") ?? GetString(root, "AppName") ?? "Manifesto Epic";
                results.Add(new GameInstallationCandidate(Storefront.Epic, Path.GetFullPath(install!), file, display));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        }

        return InstallationDiscoveryService.Deduplicate(results);
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

public static class GogLocator
{
    public static IReadOnlyList<GameInstallationCandidate> Discover(DiscoveryProfile profile)
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<GameInstallationCandidate>();
        var results = new List<GameInstallationCandidate>();

        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            results.AddRange(DiscoverGogGamesKey(profile, RegistryHive.LocalMachine, view, @"Software\GOG.com\Games"));
            results.AddRange(DiscoverGogGamesKey(profile, RegistryHive.CurrentUser, view, @"Software\GOG.com\Games"));
            results.AddRange(DiscoverUninstallKey(profile, RegistryHive.LocalMachine, view, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"));
            results.AddRange(DiscoverUninstallKey(profile, RegistryHive.CurrentUser, view, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"));
        }

        return InstallationDiscoveryService.Deduplicate(results);
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<GameInstallationCandidate> DiscoverGogGamesKey(
        DiscoveryProfile profile, RegistryHive hive, RegistryView view, string keyPath)
    {
        var results = new List<GameInstallationCandidate>();
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var games = baseKey.OpenSubKey(keyPath);
            if (games is null) return results;

            foreach (var subName in games.GetSubKeyNames())
            {
                if (profile.GogProductIds.Count > 0 && !profile.GogProductIds.Contains(subName, StringComparer.OrdinalIgnoreCase))
                    continue;
                using var game = games.OpenSubKey(subName);
                if (game is null) continue;
                var install = FirstString(game, "path", "installLocation", "workingDir");
                if (!CandidateValidator.IsGameRoot(profile, install)) continue;
                results.Add(new GameInstallationCandidate(Storefront.Gog, Path.GetFullPath(install!),
                    $"{hive}/{view}/{keyPath}/{subName}", $"GOG product {subName}"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PlatformNotSupportedException) { }
        return results;
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<GameInstallationCandidate> DiscoverUninstallKey(
        DiscoveryProfile profile, RegistryHive hive, RegistryView view, string keyPath)
    {
        var results = new List<GameInstallationCandidate>();
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(keyPath);
            if (uninstall is null) return results;

            foreach (var subName in uninstall.GetSubKeyNames())
            {
                using var entry = uninstall.OpenSubKey(subName);
                if (entry is null) continue;
                var install = FirstString(entry, "InstallLocation");
                if (!CandidateValidator.IsGameRoot(profile, install)) continue;

                var display = FirstString(entry, "DisplayName") ?? "Entrada de instalação";
                results.Add(new GameInstallationCandidate(Storefront.Gog, Path.GetFullPath(install!),
                    $"{hive}/{view}/{keyPath}/{subName}", display));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PlatformNotSupportedException) { }
        return results;
    }

    [SupportedOSPlatform("windows")]
    private static string? FirstString(RegistryKey key, params string[] names)
    {
        foreach (var name in names)
            if (key.GetValue(name) is string value && !string.IsNullOrWhiteSpace(value))
                return value.Trim().Trim('"');
        return null;
    }
}
