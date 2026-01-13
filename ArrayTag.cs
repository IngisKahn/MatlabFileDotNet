namespace MatlabFileIO;

using System.Buffers.Binary;
using System.Text;

internal class ArrayTag
{
    private static readonly Dictionary<uint, ArrayType> MatlabTypes = new() {
        [1] = new PrimitiveArrayType<sbyte>(),
        [2] = new PrimitiveArrayType<byte>(),
        [3] = new PrimitiveArrayType<short>(),
        [4] = new PrimitiveArrayType<ushort>(),
        [5] = new PrimitiveArrayType<int>(),
        [6] = new PrimitiveArrayType<uint>(),
        [7] = new PrimitiveArrayType<float>(),
        [9] = new PrimitiveArrayType<double>(),
        [12] = new PrimitiveArrayType<long>(),
        [13] = new PrimitiveArrayType<ulong>(),
        [14] = new MatrixType(),
        [15] = new CompressedType(),
        [16] = new EncodedCharacterArrayType(Encoding.UTF8),
        [17] = new EncodedCharacterArrayType(Encoding.Unicode),
        [18] = new EncodedCharacterArrayType(Encoding.UTF32)
    };

    private const int tagSize = 8;

    public ArrayType ArrayType { get; }
    public uint TypeId { get; }
    public uint Length { get; }

    public uint EmbededData { get; }

    public ArrayTag(BinaryReader reader, Header header)
    {
        var bytes = reader.ReadBytes(tagSize);
        var typeId = BitConverter.ToUInt32(bytes, 0);
        if (!header.IsSameEndian)
            typeId = BinaryPrimitives.ReverseEndianness(typeId);

        if (typeId > 255)
        {
            //Small data element format
            this.EmbededData = BitConverter.ToUInt32(bytes, 4);
            if (!header.IsSameEndian)
                this.EmbededData = BinaryPrimitives.ReverseEndianness(this.EmbededData);
            this.Length = (typeId >> 16) & 0xFFFF;
            typeId = typeId & 0xFFFF;
        }
        else
        {
            //Regular format
            this.Length = BitConverter.ToUInt32(bytes, 4);
            if (!header.IsSameEndian)
                this.Length = BinaryPrimitives.ReverseEndianness(this.Length);
        }

        this.TypeId = typeId;

        if (!MatlabTypes.TryGetValue(typeId, out var arrayType))
            arrayType = new InvalidArrayType();
        this.ArrayType = arrayType;
    }
}
