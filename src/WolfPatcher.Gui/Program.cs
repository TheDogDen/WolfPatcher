namespace WolfPatcher.Gui;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        try
        {
            var packageRoot = ReadArg(args, "--package") ?? AppContext.BaseDirectory;
            packageRoot = Path.GetFullPath(packageRoot);

            var configPath = ReadArg(args, "--config") ?? "installer.gui.json";
            configPath = Path.IsPathRooted(configPath)
                ? configPath
                : Path.Combine(packageRoot, configPath);

            var manual = ReadArg(args, "--manual");
            var config = InstallerGuiConfig.Load(configPath);
            Application.Run(new InstallerForm(packageRoot, config, manual));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "O instalador não pôde ser iniciado.\n\n" + ex.Message,
                "WolfPatcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string? ReadArg(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
