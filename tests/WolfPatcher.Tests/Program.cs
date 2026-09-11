using System.Security.Cryptography;
using WolfPatcher.Core;

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception("TEST FAIL: " + message);
}

static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
static async Task WriteAsync(string path, byte[] data)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await File.WriteAllBytesAsync(path, data);
}

var temp = Path.Combine(Path.GetTempPath(), "wolfpatcher-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var originalA = "alpha-original"u8.ToArray();
    var finalA = "alpha-final"u8.ToArray();
    var originalB = "beta-original"u8.ToArray();
    var finalB = "beta-final"u8.ToArray();

    var profile = new GameProfile
    {
        Id = "test",
        DataDirectory = "Test_Data",
        TargetFiles =
        [
            new FileHashRecord { Path = "a.bin", Size = finalA.Length, Sha256 = Hash(finalA) },
            new FileHashRecord { Path = "sub/b.bin", Size = finalB.Length, Sha256 = Hash(finalB) }
        ]
    };
    var baseline = new BaselineDefinition
    {
        Id = "TEST_BASE",
        GameProfileId = "test",
        Files =
        [
            new FileHashRecord { Path = "a.bin", Size = originalA.Length, Sha256 = Hash(originalA) },
            new FileHashRecord { Path = "sub/b.bin", Size = originalB.Length, Sha256 = Hash(originalB) }
        ]
    };
    var inspector = new StateInspector(profile, baseline);
    var dataRoot = Path.Combine(temp, "Test_Data");

    await WriteAsync(Path.Combine(dataRoot, "a.bin"), originalA);
    await WriteAsync(Path.Combine(dataRoot, "sub", "b.bin"), originalB);
    Assert((await inspector.InspectAsync(temp)).State == InstallationState.Original, "estado ORIGINAL");

    await WriteAsync(Path.Combine(dataRoot, "a.bin"), finalA);
    await WriteAsync(Path.Combine(dataRoot, "sub", "b.bin"), finalB);
    Assert((await inspector.InspectAsync(temp)).State == InstallationState.Installed, "estado INSTALLED");

    await WriteAsync(Path.Combine(dataRoot, "a.bin"), originalA);
    Assert((await inspector.InspectAsync(temp)).State == InstallationState.Mixed, "estado MIXED");

    await WriteAsync(Path.Combine(dataRoot, "a.bin"), "unexpected"u8.ToArray());
    Assert((await inspector.InspectAsync(temp)).State == InstallationState.Unknown, "estado UNKNOWN");

    File.Delete(Path.Combine(dataRoot, "a.bin"));
    Assert((await inspector.InspectAsync(temp)).State == InstallationState.Missing, "estado MISSING");

    var traversalRejected = false;
    try { _ = CoreUtil.SafeCombine(temp, "../escape.bin"); }
    catch (InvalidDataException) { traversalRejected = true; }
    Assert(traversalRejected, "path traversal recusado");

    Console.WriteLine("WolfPatcher.Tests: 6/6 PASS");
}
finally
{
    try { Directory.Delete(temp, true); } catch { }
}
