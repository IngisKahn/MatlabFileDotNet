namespace MatlabFileIO;

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
        while(reader.BaseStream.Position < reader.BaseStream.Length)
        {
            Tag tag = new(reader, this.Header);
            var variable = tag.MatlabType.ReadVariable(reader, tag, this.Header);
            if (variable != null)
                this.variables[!string.IsNullOrEmpty(variable.Name) ? variable.Name : ("Unnamed " + this.variables.Count)] = variable;
        }
    }
}
