using System.Buffers.Binary;

namespace WolfPatcher.Core;

public sealed record TransformRequest(
    string SourceFile,
    string PatchFile,
    string? MetadataFile,
    string OutputFile,
    PatchFileDefinition Definition,
    string WorkingDirectory);

public interface IFileTransformer
{
    string Id { get; }
    Task ApplyAsync(TransformRequest request, CancellationToken ct = default);
}

public sealed class VcdiffDirectTransformer(IVcdiffCodec vcdiff) : IFileTransformer
{
    public string Id => "vcdiff-direct-v1";
    public Task ApplyAsync(TransformRequest request, CancellationToken ct = default) =>
        vcdiff.ApplyAsync(request.SourceFile, request.PatchFile, request.OutputFile, ct);
}

public sealed class UnityFsSingleLzmaTransformer(IVcdiffCodec vcdiff, IRawLzmaCodec lzma) : IFileTransformer
{
    public string Id => "unityfs-single-lzma-v1";

    public async Task ApplyAsync(TransformRequest request, CancellationToken ct = default)
    {
        var source = request.Definition.Options.Source
            ?? throw new InvalidDataException("Transformador UnityFS sem opções source.");
        var target = request.Definition.Options.Target
            ?? throw new InvalidDataException("Transformador UnityFS sem opções target.");
        if (request.Definition.Options.MetadataFormat != "target-prefix-plus-5-byte-lzma-header-v1")
            throw new InvalidDataException("Formato de metadata UnityFS não suportado.");
        if (request.MetadataFile is null) throw new InvalidDataException("Metadata UnityFS ausente.");
        if (source.LzmaHeaderBytes != 5 || target.LzmaHeaderBytes != 5)
            throw new InvalidDataException("Somente cabeçalho LZMA de 5 bytes é suportado por este transformador.");

        Directory.CreateDirectory(request.WorkingDirectory);
        var sourceCompressed = Path.Combine(request.WorkingDirectory, "source.lzma.raw");
        var sourceRaw = Path.Combine(request.WorkingDirectory, "source.raw");
        var targetRaw = Path.Combine(request.WorkingDirectory, "target.raw");
        var targetCompressed = Path.Combine(request.WorkingDirectory, "target.lzma.raw");

        await ExtractSourceCompressedPayloadAsync(request.SourceFile, source, sourceCompressed, ct);
        await lzma.DecompressAsync(sourceCompressed, sourceRaw, source, ct);
        if (new FileInfo(sourceRaw).Length != source.UncompressedSize)
            throw new InvalidDataException("UnityFS source descompactado com tamanho inesperado.");

        await vcdiff.ApplyAsync(sourceRaw, request.PatchFile, targetRaw, ct);
        if (new FileInfo(targetRaw).Length != target.UncompressedSize)
            throw new InvalidDataException("VCDIFF produziu conteúdo UnityFS com tamanho inesperado.");

        await lzma.CompressAsync(targetRaw, targetCompressed, target, ct);
        if (new FileInfo(targetCompressed).Length + target.LzmaHeaderBytes != target.CompressedSize)
            throw new InvalidDataException("Recompressão LZMA não corresponde ao tamanho comprimido esperado.");

        var envelope = await File.ReadAllBytesAsync(request.MetadataFile, ct);
        if (envelope.Length != target.DataOffset + target.LzmaHeaderBytes)
            throw new InvalidDataException("Metadata UnityFS possui tamanho inesperado.");

        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputFile)!);
        await using var output = new FileStream(request.OutputFile, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);
        await output.WriteAsync(envelope, ct);
        await using var compressed = new FileStream(targetCompressed, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        await compressed.CopyToAsync(output, 1024 * 1024, ct);
    }

    private static async Task ExtractSourceCompressedPayloadAsync(string sourceFile, UnityFsLzmaShape shape,
        string destination, CancellationToken ct)
    {
        await using var input = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        if (input.Length != shape.BundleSize) throw new InvalidDataException("Bundle UnityFS source com tamanho inesperado.");
        input.Position = shape.DataOffset;
        var header = new byte[shape.LzmaHeaderBytes];
        await input.ReadExactlyAsync(header, ct);
        if (header[0] != shape.LzmaProperty || BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1, 4)) != shape.LzmaDictionarySize)
            throw new InvalidDataException("Propriedades LZMA source não correspondem ao perfil.");

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);
        await input.CopyToAsync(output, 1024 * 1024, ct);
        if (output.Length + shape.LzmaHeaderBytes != shape.CompressedSize)
            throw new InvalidDataException("Payload LZMA source com tamanho inesperado.");
    }
}

public sealed class TransformerRegistry(IEnumerable<IFileTransformer> transformers)
{
    private readonly Dictionary<string, IFileTransformer> _items = transformers.ToDictionary(x => x.Id, StringComparer.Ordinal);
    public IFileTransformer Get(string id) => _items.TryGetValue(id, out var transformer)
        ? transformer
        : throw new InvalidDataException($"Transformador não registrado: {id}");
}
