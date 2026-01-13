namespace MatlabFileIO;

using System.IO.Compression;

internal class CompressedType : ArrayType
{
    public override Array ReadArray(BinaryReader reader, ArrayTag tag, Header header)
    {
        using MemoryStream compressedStream = new(reader.ReadBytes((int)tag.Length));
        using ZLibStream zlibStream = new(compressedStream, CompressionMode.Decompress);
        using MemoryStream uncompressedStream = new();
        zlibStream.CopyTo(uncompressedStream);
        uncompressedStream.Seek(0, SeekOrigin.Begin);
        using BinaryReader br = new(uncompressedStream);
        return Array.Read(br, header);
    }
}
