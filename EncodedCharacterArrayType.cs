namespace MatlabFileIO;

using System.Text;

internal class EncodedCharacterArrayType(Encoding encoding) : ArrayType
{
    public override Array ReadArray(BinaryReader reader, ArrayTag tag, Header header)
    {
        var bytes = tag.Length <= 4
            ? BitConverter.GetBytes(tag.EmbededData)
            : reader.ReadBytes((int)tag.Length);
        var str = encoding.GetString(bytes);
        return new String(str);
    }
}
