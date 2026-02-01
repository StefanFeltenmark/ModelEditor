// Add warning system to parser
public class ParseSessionResult
{
    public List<string> Warnings { get; } = new List<string>();
    
    public void AddWarning(string message, int lineNumber)
    {
        Warnings.Add($"Line {lineNumber}: WARNING - {message}");
    }
}

