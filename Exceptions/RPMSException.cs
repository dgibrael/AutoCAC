namespace AutoCAC.Exceptions;

public class RPMSException : Exception
{
    public RPMSException(string message = "Unknown menu location")
        : base(message) { }
}
