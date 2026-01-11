namespace MatlabFileIO;

using System.Buffers.Binary;
using System.Text;

internal class Tag
{
    private static readonly Dictionary<uint, MatlabType> MatlabTypes = new() {
        [1] = new PrimitiveMatlabType<sbyte>(),
        [2] = new PrimitiveMatlabType<byte>(),
        [3] = new PrimitiveMatlabType<short>(),
        [4] = new PrimitiveMatlabType<ushort>(),
        [5] = new PrimitiveMatlabType<int>(),
        [6] = new PrimitiveMatlabType<uint>(),
        [7] = new PrimitiveMatlabType<float>(),
        [9] = new PrimitiveMatlabType<double>(),
        [12] = new PrimitiveMatlabType<long>(),
        [13] = new PrimitiveMatlabType<ulong>(),
        [14] = new MatrixMatlabType(),
        [15] = new CompressedMatlabType(),
        [16] = new EncodedCharacterMatlabType(Encoding.UTF8),
        [17] = new EncodedCharacterMatlabType(Encoding.Unicode),
        [18] = new EncodedCharacterMatlabType(Encoding.UTF32)
    };

    private const int tagSize = 8;

    public MatlabType MatlabType { get; }
    public uint Length { get; }

    public uint EmbededData { get; }

    public Tag(BinaryReader reader, Header header)
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
            typeId = typeId >> 16;
            this.Length = typeId & 0xFFFF;
        }
        else
        {
            //Regular format
            this.Length = BitConverter.ToUInt32(bytes, 4);
            if (!header.IsSameEndian)
                this.Length = BinaryPrimitives.ReverseEndianness(this.Length);
        }


        if (!MatlabTypes.TryGetValue(typeId, out var matlabType))
            matlabType = new InvalidMatlabType();
        this.MatlabType = matlabType;
    }
}
