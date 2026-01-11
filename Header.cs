namespace MatlabFileIO;

using System.Text;

public class Header
{
    public string Text { get; }
    public MatfileVersion Version { get; }
    public Header(byte[] bytes)
    {
        if (bytes.Length != 128)
            throw new MatlabFileException("Matlab header should be 128 charachters");

        this.Text = Encoding.ASCII.GetString(bytes[..116]);

        var version = (ushort)(bytes[125] << 8 + bytes[124]);
        if (version != 0x0100)
            throw new MatlabFileException("Unsupported version of matlab file");
        this.Version = MatfileVersion.Version5;
    }
}
