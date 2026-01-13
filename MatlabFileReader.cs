namespace MatlabFileIO;

public class Matfile
{
    private readonly Dictionary<string, Array> arrays = [];
    public IReadOnlyDictionary<string, Array> Arrays => this.arrays;
    public Header Header { get; }

    public Matfile(string fileName)
    {
        //create stream from filename
        FileStream fileStream = new(fileName, FileMode.Open, FileAccess.Read);
        var reader = new BinaryReader(fileStream);
        //Parse header (will throw if fail)
        this.Header = new(reader.ReadBytes(128));

        this.ReadArrays(reader);
    }

    private void ReadArrays(BinaryReader reader)
    {
        while(reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var array = Array.Read(reader, this.Header);
            if (array != null)
                this.arrays[!string.IsNullOrEmpty(array.Name) ? array.Name : ("Unnamed " + this.arrays.Count)] = array;
        }

        var data = ((PrimitiveMatrixClass<byte>)arrays["\0\0\0\0"]).RealData;
    }
}
