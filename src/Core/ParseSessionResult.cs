using System.Collections.Generic;
using System.Linq;

namespace Core
{
    /// <summary>
    /// Result of a parsing session containing errors and success count
    /// </summary>
    public class ParseSessionResult
    {
        public List<(string Message, int LineNumber, string? FilePath)> Errors { get; private set; } = new List<(string, int, string?)>();
        public int SuccessCount { get; private set; } = 0;

        public void AddError(string error, int lineNumber, string? filePath = null)
        {
            Errors.Add((error, lineNumber, filePath));
        }

        public void IncrementSuccess()
        {
            SuccessCount++;
        }

        public bool HasErrors => Errors.Count > 0;
        public bool HasSuccess => SuccessCount > 0;

        public IEnumerable<string> GetErrorMessages()
        {
            return Errors.Select(e => e.Message);
        }

        public IEnumerable<string> GetErrorsForLine(int lineNumber)
        {
            return Errors.Where(e => e.LineNumber == lineNumber).Select(e => e.Message);
        }
    }
}