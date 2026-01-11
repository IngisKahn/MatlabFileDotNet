namespace MatlabFileIO;

using System.ComponentModel.DataAnnotations;
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



internal class Flag
{
    public Tag Tag;
    public bool Complex = false;
    public bool Global = false;
    public bool Logical = false;
    public Type dataClass;
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
                Buffer.MemoryCopy(pBytes, pData, bytes.Length, bytes.Length);
            
            return new Variable<T[]>(data);
        }
    }
}

internal class MatrixMatlabType : MatlabType
{

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
}

internal static class MatfileHelper
{
    public const int SZ_TAG = 8; //Tag size in bytes

  


    public static Flag ReadFlag(this BinaryReader reader)
    {
        Flag f = new Flag() { Complex = false, Global = false, Logical = false };
        //f.Tag = reader.ReadTag();
        UInt32 flagsClass = reader.ReadUInt32();
        byte flags = (byte)(flagsClass >> 8);
        if ((flags & 0x08) == 0x08)
            f.Complex = true;
        if ((flags & 0x04) == 0x04)
            f.Global = true;
        if ((flags & 0x02) == 0x02)
            f.Logical = true;
        f.dataClass = MatfileHelper.parseArrayType((byte)flagsClass);
        reader.ReadUInt32();//unused flags
        //Flag f = matrixStream.ReadFlag();

        return f;
    }

    public static void AdvanceTo8ByteBoundary(this BinaryReader r)
    {
        long offset = (8 - (r.BaseStream.Position % 8)) % 8;
        r.BaseStream.Seek(offset, SeekOrigin.Current);
    }

    public static int AdvanceTo8ByteBoundary(this BinaryWriter w, byte stuffing = 0x00)
    {
        long offset = (8 - (w.BaseStream.Position % 8)) % 8;
        for(int i =0; i < offset; i ++)
            w.Write(stuffing);
        return (int)offset;
    }

    internal static Type[] ArrayTypes = new Type[] {
        null,               //0
        null,               //1
        null,               //2
        null,               //3
        typeof(Char),       //4
        null,               //5
        typeof(Double),     //6
        typeof(Single),     //7
        typeof(SByte),      //8
        typeof(Byte),       //9
        typeof(Int16),      //10
        typeof(UInt16),     //11
        typeof(Int32),      //12
        typeof(UInt32),     //13
        typeof(Int64),      //14
        typeof(UInt64)      //15
    };

    public static Type parseArrayType(byte contentTypeInt)
    {
        Type t = ArrayTypes[contentTypeInt];
        if (t != null) return t;
        throw new Exception("Content of array not supported");
    }

    public static int MatlabDataTypeNumber<T>()
    {
        var t = typeof(T);
        int i = 0;// Array.IndexOf(DataType, t);
        if (i > 0) return i;
        throw new NotImplementedException("Arrays of " + t.ToString() + " to .mat file not implemented");
    }

    public static int MatlabArrayTypeNumber<T>()
    {
        var t = typeof(T);
        int i = Array.IndexOf(ArrayTypes, t);
        if (i > 0) return i;
        throw new NotImplementedException("Arrays of " + t.ToString() + " to .mat file not implemented");
    }


    public static Array CastToMatlabType<T>(byte[] data, int offset = 0, int length = -1)
    {
        if (length < 0)
            length = data.Length - offset;
        var result = new T[length / 1];//
        Buffer.BlockCopy(data, offset, result, 0, length);
        return result;
    }

    public static Array SliceRow(this Array array, int row)
    {
        Array output = Array.CreateInstance(array.GetValue(0,0).GetType(), array.GetLength(1));
        for (var i = 0; i < array.GetLength(1); i++)
        {
            output.SetValue(array.GetValue(new int[] { row, i }), i);
        }
        return output;
    }
}
