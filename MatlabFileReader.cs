namespace MatlabFileIO;

using System.Text;

public class Matfile
{
    private readonly Dictionary<string, Variable> variables = [];
    public IReadOnlyDictionary<string, Variable> Variables => this.variables;
    public Header Header { get; }

    public Matfile(string fileName)
    {
        //create stream from filename
        FileStream fileStream = new(fileName, FileMode.Open, FileAccess.Read);
        var reader = new BinaryReader(fileStream);

        //Parse header (will throw if fail)
        this.Header = new(reader.ReadBytes(128));

        this.ReadVariables(reader);
    }

    private void ReadVariables(BinaryReader reader)
    {
        Variables = new Dictionary<string, Variable>();
        while(readStream.BaseStream.Position < readStream.BaseStream.Length)
        {
            Variable v = new Variable();

            Tag t = MatfileHelper.ReadTag(readStream);
            if(t.dataType == null)
                throw new Exception("Not an array, don't know what to do with this stuff");
            else if (t.dataType.Equals(typeof(Array))) //We use Array to indicate MiMatrix
            {
                ReadMatrix(ref v, t.length, readStream);
            } else if (t.dataType.Equals(typeof(ZlibStream)))
            {
                byte[] compressed = readStream.ReadBytes((int)t.length);
                byte[] decompressed = ZlibStream.UncompressBuffer(compressed);
                MemoryStream m = new MemoryStream(decompressed);
                BinaryReader br = new BinaryReader(m);
                Tag ct = MatfileHelper.ReadTag(br);
                ReadMatrix(ref v, ct.length, br);
            }
            else
                throw new Exception("Not an array, don't know what to do with this stuff");
            
            Variables.Add(v.name, v);
        }
    }

    private static void ReadMatrix(ref Variable vi, UInt32 length, BinaryReader matrixStream)
    {
        Tag t;
        long offset = matrixStream.BaseStream.Position;

        //Array flags
        //Will always be too large to be in small data format, so not checking t.data
        Flag Flag = matrixStream.ReadFlag();
        vi.dataType = Flag.dataClass;

        //Dimensions - There are always 2 dimensions, so this
        //tag will never be of small data format, i.e. not checking for t.data
        t = MatfileHelper.ReadTag(matrixStream);
        int[] arrayDimensions = new int[t.length / MatfileHelper.MatlabBytesPerType(t.dataType)];
        int elements = 1;
        for (int i = 0; i < arrayDimensions.Length; i++)
        {
            int dimension = (int)matrixStream.ReadUInt32();// depends on sizeof(t.dataType) => MatfileHelper.MatlabBytesPerType(t.dataType)
            arrayDimensions[arrayDimensions.Length - i - 1] = dimension;
            elements *= dimension;
        }
        //Don't keep single dimensions
        arrayDimensions = arrayDimensions.Where(x => x > 1).ToArray();
        //If by doing this, we end up without dimensions, it means we had a 1x...x1 array. 
        //We need at least 1 dimension to instantiate the final array, so...
        if (arrayDimensions.Length == 0)
            arrayDimensions = new int[1] { 1 };

        //Array name
        t = MatfileHelper.ReadTag(matrixStream);
        if (t.data != null)
        {
            sbyte[] varname = t.data as sbyte[];
            vi.name = Encoding.UTF8.GetString(Array.ConvertAll(varname, x => (byte)x));
        } else {
            byte[] varname = matrixStream.ReadBytes((int)t.length);
            vi.name = Encoding.UTF8.GetString(varname);
            MatfileHelper.AdvanceTo8ByteBoundary(matrixStream);
        }
        
        
        //Read and reshape data
        t = MatfileHelper.ReadTag(matrixStream);
        reshape(ref vi.data, ref vi.dataType, matrixStream, t, arrayDimensions, elements);

        // Read Imaginary data
        if (Flag.Complex)
        {
            t = matrixStream.ReadTag();
            reshape(ref vi.Image, ref vi.dataType, matrixStream, t, arrayDimensions, elements);
        }
        //Move on in case the data didn't end on a 64 byte boundary
        matrixStream.BaseStream.Seek(offset + length, SeekOrigin.Begin);
    }

    private static void reshape(ref object data, ref Type dataType, BinaryReader matrixStream, Tag t, int[] arrayDimensions, int elements)
    {
        if (t.length / MatfileHelper.MatlabBytesPerType(t.dataType) != elements)
            throw new IOException("Read dimensions didn't correspond to header dimensions");
                
        Array readBytes;
        if (t.data == null)
            readBytes = MatfileHelper.CastToMatlabType(t.dataType, matrixStream.ReadBytes((int)t.length));
        else
            readBytes = (Array)t.data;

        Array reshapedData = Array.CreateInstance(t.dataType, elements);
        if (t.dataType != dataType) //This happens when matlab choses to store the data in a smaller datatype when the values permit it
        {
            Array linearData = Array.CreateInstance(dataType, readBytes.Length);
            Array.Copy(readBytes, linearData, readBytes.Length);
            Buffer.BlockCopy(linearData, 0, reshapedData, 0, linearData.Length * MatfileHelper.MatlabBytesPerType(dataType));
            //dataType = t.dataType;
        }
        else //Readbytes is already in the correct type
            Buffer.BlockCopy(readBytes, 0, reshapedData, 0, readBytes.Length * MatfileHelper.MatlabBytesPerType(dataType));

        if(reshapedData.Length == 1)
            data = reshapedData.GetValue(0);
        else
            data = reshapedData;

        //Move on in case the data didn't end on a 64 byte boundary
        //matrixStream.BaseStream.Seek(offset + length, SeekOrigin.Begin);
    }

    public void Close()
    {
        readStream.Close();
    }
}
