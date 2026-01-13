namespace MatlabFileIO;

using System.Numerics;
using System.Text;

public abstract class Array
{
    public string Name { get; protected set; } = string.Empty;
    internal static Array Read(BinaryReader reader, ArrayTag tag, Header header)
        => tag.ArrayType.ReadArray(reader, tag, header);
    internal static Array Read(BinaryReader reader, Header header)
    {
        ArrayTag tag = new(reader, header);
        return Read(reader, tag, header);
    }

    internal static Array ReadArrayOfType<T>(BinaryReader reader, Header header) where T : ArrayType
    {
        ArrayTag tag = new(reader, header);
        return tag.ArrayType is not T
            ? throw new MatlabFileException($"Expected array of type {typeof(T).Name} but got {tag.ArrayType.GetType().Name}")
            : Read(reader, tag, header);
    }

    internal static Array ReadArrayOfType<T>(BinaryReader reader, Header header, int expectedLength) where T : ArrayType
    {
        ArrayTag tag = new(reader, header);
        if (tag.ArrayType is not T)
            throw new MatlabFileException($"Expected array of type {typeof(T).Name} but got {tag.ArrayType.GetType().Name}");
        if (tag.Length != expectedLength)
            throw new MatlabFileException($"Expected array of length {expectedLength} but got {tag.Length}");
        return Read(reader, tag, header);
    }

    internal static T[] ReadArrayConvert<T>(BinaryReader reader, Header header) where T : unmanaged, INumber<T>
    {
        return Read(reader, header) switch
        {
            Array<sbyte> sbyteArray => Convert(sbyteArray.Data),
            Array<byte> byteArray => Convert(byteArray.Data),
            Array<short> shortArray => Convert(shortArray.Data),
            Array<ushort> ushortArray => Convert(ushortArray.Data),
            Array<int> intArray => Convert(intArray.Data),
            Array<uint> uintArray => Convert(uintArray.Data),
            Array<long> longArray => Convert(longArray.Data),
            Array<ulong> ulongArray => Convert(ulongArray.Data),
            Array<float> floatArray => Convert(floatArray.Data),
            Array<double> doubleArray => Convert(doubleArray.Data),
            String stringArray => Convert(stringArray.Data.ToArray()),
            _ => throw new MatlabFileException("Unsupported array type for conversion"),
        };

        static T[] Convert<U>(U[] data) where U : unmanaged, INumber<U>
        {
            var result = new T[data.Length];
            for (var i = 0; i < data.Length; i++)
                result[i] = T.CreateChecked(data[i]);
            return result;
        }
    }
}

public class Array<T> : Array
{
    public T[] Data { get; set; }
    internal Array(T[] data) => this.Data = data;
}

public class String : Array
{
    public string Data { get; set; }
    internal String(string data) => this.Data = data;
}

internal abstract class ArrayType
{
    public virtual Array ReadArray(BinaryReader reader, ArrayTag tag, Header header) =>
        throw new NotImplementedException();
}

internal class InvalidArrayType : ArrayType
{
}

internal class PrimitiveArrayType<T> : ArrayType where T : unmanaged
{
    public override Array ReadArray(BinaryReader reader, ArrayTag tag, Header header)
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

            //ensure 8 byte alignment
            if ((reader.BaseStream.Position & 7) != 0)
                reader.BaseStream.Seek(8 - (reader.BaseStream.Position & 7), SeekOrigin.Current);

            return new Array<T>(data);
        }
    }
}

internal class MatrixType : ArrayType
{
    public override Array ReadArray(BinaryReader reader, ArrayTag tag, Header header)
    {
        var bytes = reader.ReadBytes((int)tag.Length);

        using MemoryStream matrixStream = new(bytes);
        using BinaryReader matrixReader = new(matrixStream);
        return Matrix.ReadMatrix(matrixReader, header);
    }
}

public abstract class Matrix : Array
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

    internal static Matrix ReadMatrix(BinaryReader reader, Header header)
    {
        // Read Array Flags
        var flagsData = Array.ReadArrayOfType<PrimitiveArrayType<uint>>(reader, header, 8) as Array<uint>
            ?? throw new MatlabFileException("Failed to read Array Flags in Matrix");

        var flags = (MatrixFlags)(flagsData.Data[0] >> 8);
        var matrixClass = (MatrixClass)(flagsData.Data[0] & 0xFF);
        var isComplex = flags.HasFlag(MatrixFlags.Complex);
        var nonZeroMax = flagsData.Data[1];
        return matrixClass switch
        {
            MatrixClass.Cell => new CellMatrixClass(reader, header),
            MatrixClass.Struct => throw new NotImplementedException(),
            MatrixClass.Object => throw new NotImplementedException(),
            MatrixClass.Char => new PrimitiveMatrixClass<char>(reader, header, isComplex),
            MatrixClass.Sparse => new SparseArrayClass(reader, header, isComplex, nonZeroMax),
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
            MatrixClass.Logical => new LogicalMatrixClass(reader, header),
            MatrixClass.LittleEndianPackedArray => throw new NotImplementedException(),
            _ => throw new MatlabFileException("Unknown matrix type"),
        };
    }

    public int[] Dimensions { get; }

    public int TotalElements => this.Dimensions.Aggregate(1, (a, b) => a * b);

    protected Matrix(BinaryReader reader, Header header, bool hasDimensions = true)
    {
        if (hasDimensions)
        {
            ArrayTag dimensionsTag = new(reader, header);
            if (dimensionsTag.ArrayType is not PrimitiveArrayType<int>)
                throw new MatlabFileException("Invalid Dimensions Array in Matrix");

            var dimensions = Array.Read(reader, dimensionsTag, header) as Array<int>
                ?? throw new MatlabFileException("Failed to read Dimensions Array in Matrix");

            this.Dimensions = dimensions.Data;
        }
        else
            this.Dimensions = [];

        ArrayTag nameTag = new(reader, header);

        if (nameTag.ArrayType is not PrimitiveArrayType<sbyte>)
            throw new MatlabFileException("Invalid Array Name in Matrix");

        var nameData = Array.Read(reader, nameTag, header) as Array<sbyte>
            ?? throw new MatlabFileException("Failed to read Array Name in Matrix");

        this.Name = Encoding.ASCII.GetString(System.Array.ConvertAll(nameData.Data, b => (byte)b));
    }
}

internal class LogicalMatrixClass : Matrix
{
    public string PrimaryKey { get; }
    public string SecondaryKey { get; }
    public Matrix Data { get; }
    public LogicalMatrixClass(BinaryReader reader, Header header) : base(reader, header, false)
    {
        ArrayTag primaryKeyTag = new(reader, header);
        if (primaryKeyTag.ArrayType is not PrimitiveArrayType<sbyte>)
            throw new MatlabFileException("Invalid Primary Key in Logical Matrix");
        var primaryKeyData = Array.Read(reader, primaryKeyTag, header) as Array<sbyte>
            ?? throw new MatlabFileException("Failed to read Primary Key in Logical Matrix");
        this.PrimaryKey = Encoding.ASCII.GetString(System.Array.ConvertAll(primaryKeyData.Data, b => (byte)b));

        ArrayTag secondaryKeyTag = new(reader, header);
        if (secondaryKeyTag.ArrayType is not PrimitiveArrayType<sbyte>)
            throw new MatlabFileException("Invalid Secondary Key in Logical Matrix");   
        var secondaryKeyData = Array.Read(reader, secondaryKeyTag, header) as Array<sbyte>
            ?? throw new MatlabFileException("Failed to read Secondary Key in Logical Matrix");
        this.SecondaryKey = Encoding.ASCII.GetString(System.Array.ConvertAll(secondaryKeyData.Data, b => (byte)b));

        this.Data = (Matrix)Array.Read(reader, header);
    }
}

internal class CellMatrixClass : Matrix
{
    public Array[] Cells { get; }
    public CellMatrixClass(BinaryReader reader, Header header) : base(reader, header)
    {
        this.Cells = new Array[this.TotalElements];
        for (var i = 0; i < this.TotalElements; i++)
            this.Cells[i] = Array.Read(reader, header);
    }
}

internal class SparseArrayClass : Matrix
{
    public Array<int> RowIndices { get; }
    public Array<int> ColumnPointers { get; }
    public Array RealData { get; }
    public Array? ImaginaryData { get; }
    public SparseArrayClass(BinaryReader reader, Header header, bool isComplex, uint nonZeroMax) : base(reader, header)
    {
        ArrayTag rowIndicesTag = new(reader, header);
        if (rowIndicesTag.ArrayType is not PrimitiveArrayType<int>)
            throw new MatlabFileException("Invalid Row Indices in Sparse Matrix");
        this.RowIndices = Array.Read(reader, rowIndicesTag, header) as Array<int>
            ?? throw new MatlabFileException("Failed to read Row Indices in Sparse Matrix");
        ArrayTag columnPointersTag = new(reader, header);
        if (columnPointersTag.ArrayType is not PrimitiveArrayType<int>)
            throw new MatlabFileException("Invalid Column Pointers in Sparse Matrix");
        this.ColumnPointers = Array.Read(reader, columnPointersTag, header) as Array<int>
            ?? throw new MatlabFileException("Failed to read Column Pointers in Sparse Matrix");
        ArrayTag realDataTag = new(reader, header);
        this.RealData = Array.Read(reader, realDataTag, header);
        if (!isComplex)
            return;
        ArrayTag imaginaryDataTag = new(reader, header);
        this.ImaginaryData = Array.Read(reader, imaginaryDataTag, header);
    }
}

internal class PrimitiveMatrixClass<T> : Matrix where T : unmanaged, INumber<T>
{
    public T[] RealData { get; }
    public T[]? ImaginaryData { get; }
    public PrimitiveMatrixClass(BinaryReader reader, Header header, bool isComplex) : base(reader, header)
    {
        this.RealData = Array.ReadArrayConvert<T>(reader, header);
        if (isComplex)
            this.ImaginaryData = Array.ReadArrayConvert<T>(reader, header);
    }
}
