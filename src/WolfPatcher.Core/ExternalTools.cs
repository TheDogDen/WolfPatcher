using System.Diagnostics;

namespace WolfPatcher.Core;

public sealed class ExternalToolException(string message) : Exception(message);

public static class ExternalToolRunner
{
    public static async Task RunAsync(string executable, IEnumerable<string> arguments, string? stdoutFile = null,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = stdoutFile is not null
        };
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        if (!process.Start()) throw new ExternalToolException($"Falha ao iniciar: {executable}");

        Task copyTask = Task.CompletedTask;
        FileStream? output = null;
        if (stdoutFile is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(stdoutFile)!);
            output = new FileStream(stdoutFile, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);
            copyTask = process.StandardOutput.BaseStream.CopyToAsync(output, ct);
        }
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        await copyTask;
        if (output is not null) await output.DisposeAsync();
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new ExternalToolException($"{Path.GetFileName(executable)} terminou com código {process.ExitCode}: {stderr.Trim()}");
    }
}

public interface IVcdiffCodec
{
    Task ApplyAsync(string sourceFile, string patchFile, string outputFile, CancellationToken ct = default);
}

public sealed class Xdelta3VcdiffCodec(string executablePath) : IVcdiffCodec
{
    public Task ApplyAsync(string sourceFile, string patchFile, string outputFile, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        return ExternalToolRunner.RunAsync(executablePath,
            ["-D", "-f", "-d", "-s", sourceFile, patchFile, outputFile], null, ct);
    }
}

public interface IRawLzmaCodec
{
    Task DecompressAsync(string compressedRawFile, string rawOutputFile, UnityFsLzmaShape shape, CancellationToken ct = default);
    Task CompressAsync(string rawInputFile, string compressedRawOutputFile, UnityFsLzmaShape shape, CancellationToken ct = default);
}

public sealed class XzRawLzmaCodec(string executablePath) : IRawLzmaCodec
{
    private static string Filter(UnityFsLzmaShape s, bool encode)
    {
        var preset = encode && s.LzmaPreset.HasValue ? $"preset={s.LzmaPreset.Value}," : "";
        return $"--lzma1={preset}dict={s.LzmaDictionarySize},lc={s.LzmaLc},lp={s.LzmaLp},pb={s.LzmaPb}";
    }

    public Task DecompressAsync(string compressedRawFile, string rawOutputFile, UnityFsLzmaShape shape, CancellationToken ct = default) =>
        ExternalToolRunner.RunAsync(executablePath,
            ["--format=raw", "--decompress", "--stdout", Filter(shape, false), compressedRawFile], rawOutputFile, ct);

    public Task CompressAsync(string rawInputFile, string compressedRawOutputFile, UnityFsLzmaShape shape, CancellationToken ct = default) =>
        ExternalToolRunner.RunAsync(executablePath,
            ["--format=raw", "--stdout", Filter(shape, true), rawInputFile], compressedRawOutputFile, ct);
}
