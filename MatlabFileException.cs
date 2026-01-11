namespace MatlabFileIO;

[Serializable]
public class MatlabFileException : Exception
{
    public MatlabFileException() { }
    public MatlabFileException(string message) : base(message) { }
    public MatlabFileException(string message, Exception inner) : base(message, inner) { }
}
