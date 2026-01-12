namespace MatlabFileIO;

using System.IO.Compression;
using System.Text;

public abstract class Variable
{
    public string Name { get; protected set; } = string.Empty;
    internal static Variable Read(BinaryReader reader, Tag tag, Header header) 
        => tag.MatlabType.ReadVariable(reader, tag, header);
}

public class Variable<T> : Variable
{
    public T Data { get; set; }
    internal Variable(T data) => this.Data = data;
}

internal abstract class MatlabType
{
    public virtual Variable ReadVariable(BinaryReader reader, Tag tag, Header header) =>
        throw new NotImplementedException();
}

internal class InvalidMatlabType : MatlabType
{ 
}

internal class PrimitiveMatlabType<T> : MatlabType where T : unmanaged
{
    public override Variable ReadVariable(BinaryReader reader, Tag tag, Header header)
    {
        var bytes = tag.Length <= 4 
            ? BitConverter.GetBytes(tag.EmbededData) 
            : reader.ReadBytes((int)tag.Length);
        unsafe
        {
            var count = bytes.Length / sizeof(T);
            var data = new T[count];
            fixed (byte* pBytes = bytes)
            fixed (T* pData = data)
            {
                Buffer.MemoryCopy(pBytes, pData, bytes.Length, bytes.Length);

                if (header.IsSameEndian == false && sizeof(T) > 1)
                    for (var i = 0; i < count; i++)
                        new Span<byte>((byte*)pData + i * sizeof(T), sizeof(T)).Reverse();
            }
            
            return new Variable<T[]>(data);
        }
    }
}

internal class MatrixMatlabType : MatlabType
{
    public override Variable ReadVariable(BinaryReader reader, Tag tag, Header header)
    {
        var bytes = reader.ReadBytes((int)tag.Length);

        using MemoryStream matrixStream = new(bytes);
        using BinaryReader matrixReader = new(matrixStream);
        return MatrixVariable.ReadMatrix(matrixReader, tag, header);
    }
}

public abstract class MatrixVariable : Variable
{
    public enum MatrixClass : byte
    {
        Cell = 1,
        Struct = 2,
        Object = 3,
        Char = 4,
        Sparse = 5,
        Double = 6,
        Single = 7,
        Int8 = 8,
        UInt8 = 9,
        Int16 = 10,
        UInt16 = 11,
        Int32 = 12,
        UInt32 = 13,
        Int64 = 14,
        UInt64 = 15,
        FunctionHandle = 16,
        Logical = 17,
        LittleEndianPackedArray = 18,
        MxUnknown = 0xFF
    }

    [Flags]
    public enum MatrixFlags : byte
    {
        Complex = 0x08,
        Global = 0x04,
        Logical = 0x02
    }

    internal static MatrixVariable ReadMatrix(BinaryReader reader, Tag tag, Header header)
    {
        // Read Array Flags
        Tag arrayFlagsTag = new(reader, header);
        if (arrayFlagsTag.MatlabType is not PrimitiveMatlabType<uint> || arrayFlagsTag.Length != 8)
            throw new MatlabFileException("Invalid Array Flags in Matrix");
        var flagsData = Read(reader, arrayFlagsTag, header) as Variable<uint[]>
            ?? throw new MatlabFileException("Failed to read Array Flags in Matrix");
        var flags = (MatrixFlags)(flagsData.Data[0] >> 8);
        var matrixClass = (MatrixClass)(flagsData.Data[0] & 0xFF);
        var isComplex = flags.HasFlag(MatrixFlags.Complex);
        return matrixClass switch
        {
            MatrixClass.Cell => throw new NotImplementedException(),
            MatrixClass.Struct => throw new NotImplementedException(),
            MatrixClass.Object => throw new NotImplementedException(),
            MatrixClass.Char => new PrimitiveMatrixClass<char>(reader, header, isComplex),
            MatrixClass.Sparse => throw new NotImplementedException(),
            MatrixClass.Double => new PrimitiveMatrixClass<double>(reader, header, isComplex),
            MatrixClass.Single => new PrimitiveMatrixClass<float>(reader, header, isComplex),
            MatrixClass.Int8 => new PrimitiveMatrixClass<sbyte>(reader, header, isComplex),
            MatrixClass.UInt8 => new PrimitiveMatrixClass<byte>(reader, header, isComplex),
            MatrixClass.Int16 => new PrimitiveMatrixClass<short>(reader, header, isComplex),
            MatrixClass.UInt16 => new PrimitiveMatrixClass<ushort>(reader, header, isComplex),
            MatrixClass.Int32 => new PrimitiveMatrixClass<int>(reader, header, isComplex),
            MatrixClass.UInt32 => new PrimitiveMatrixClass<uint>(reader, header, isComplex),
            MatrixClass.Int64 => new PrimitiveMatrixClass<long>(reader, header, isComplex),
            MatrixClass.UInt64 => new PrimitiveMatrixClass<ulong>(reader, header, isComplex),
            MatrixClass.FunctionHandle => throw new NotImplementedException(),
            MatrixClass.Logical => throw new NotImplementedException(),
            MatrixClass.LittleEndianPackedArray => throw new NotImplementedException(),
            _ => throw new MatlabFileException("Unknown matrix type"),
        };
    }

    protected MatrixVariable(BinaryReader reader, Header header)
    {
        Tag dimensionsTag = new(reader, header);
        if (dimensionsTag.MatlabType is not PrimitiveMatlabType<int>)
            throw new MatlabFileException("Invalid Dimensions Array in Matrix");

        var dimensions = Read(reader, dimensionsTag, header) as Variable<int[]>
            ?? throw new MatlabFileException("Failed to read Dimensions Array in Matrix");

        Tag nameTag = new(reader, header);

        if (nameTag.MatlabType is not PrimitiveMatlabType<sbyte>)
            throw new MatlabFileException("Invalid Array Name in Matrix");

        var nameData = Read(reader, nameTag, header) as Variable<sbyte[]>
            ?? throw new MatlabFileException("Failed to read Array Name in Matrix");

        this.Name = Encoding.ASCII.GetString(Array.ConvertAll(nameData.Data, b => (byte)b));
    }
}

internal class PrimitiveMatrixClass<T> : MatrixVariable where T : unmanaged
{
    public Variable<T[]> RealData { get; }
    public Variable<T[]>? ImaginaryData { get; }
    public PrimitiveMatrixClass(BinaryReader reader, Header header, bool isComplex) : base(reader, header)
    {
        Tag realDataTag = new(reader, header);
        if (realDataTag.MatlabType is not PrimitiveMatlabType<T>)
            throw new MatlabFileException("Invalid Real Data in Matrix");
        this.RealData = Read(reader, realDataTag, header) as Variable<T[]> 
            ?? throw new MatlabFileException("Failed to read Real Data in Matrix");
        if (!isComplex)
            return;
        Tag imaginaryDataTag = new(reader, header);
        if (imaginaryDataTag.MatlabType is not PrimitiveMatlabType<T>)
            throw new MatlabFileException("Invalid Imaginary Data in Matrix");
        this.ImaginaryData = Read(reader, imaginaryDataTag, header) as Variable<T[]>
            ?? throw new MatlabFileException("Failed to read Imaginary Data in Matrix");
    }
}

internal class CompressedMatlabType : MatlabType
{
    public override Variable ReadVariable(BinaryReader reader, Tag tag, Header header)
    {
        using MemoryStream compressedStream = new(reader.ReadBytes((int)tag.Length));
        using ZLibStream zlibStream = new(compressedStream, CompressionMode.Decompress);
        using MemoryStream uncompressedStream = new();
        zlibStream.CopyTo(uncompressedStream);
        uncompressedStream.Seek(0, SeekOrigin.Begin);
        using BinaryReader br = new(uncompressedStream);
        Tag ct = new(br, header);
        return ct.MatlabType.ReadVariable(br, ct, header);
    }
}

internal class EncodedCharacterMatlabType(Encoding encoding) : MatlabType
{
    public override Variable ReadVariable(BinaryReader reader, Tag tag, Header header)
    {
        var bytes = tag.Length <= 4
            ? BitConverter.GetBytes(tag.EmbededData)
            : reader.ReadBytes((int)tag.Length);
        var str = encoding.GetString(bytes);
        return new Variable<string>(str);
    }
}
