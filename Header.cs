namespace MatlabFileIO;

using System.Buffers.Binary;
using System.Text;

public class Header
{
    public string Text { get; }
    public MatfileVersion Version { get; }

    public ulong SubsystemOffset { get; }
    public bool IsSameEndian { get; }
    public Header(byte[] bytes)
    {
        if (bytes.Length != 128)
            throw new MatlabFileException("Matlab header should be 128 charachters");

        this.Text = Encoding.ASCII.GetString(bytes[..116]);

        this.SubsystemOffset = BitConverter.ToUInt64(bytes, 116);

        const ushort mi = 0x4d49; //'MI' in ASCII
        const ushort im = 0x494d; //'IM' in ASCII

        var endianIndicator = BitConverter.ToUInt16(bytes, 126);
        if (endianIndicator == mi)
            this.IsSameEndian = true;
        else if (endianIndicator == im) //'IM' in ASCII
            this.IsSameEndian = false;
        else
            throw new MatlabFileException("Invalid endian indicator in matlab file header");
        
        var version = BitConverter.ToUInt16(bytes, 124);
        if (!this.IsSameEndian)
            version = BinaryPrimitives.ReverseEndianness(version);
        if (version != 0x0100)
            throw new MatlabFileException("Unsupported version of matlab file");
        this.Version = MatfileVersion.Version5;
    }
}
