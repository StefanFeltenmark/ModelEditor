namespace Core
{
    /// <summary>
    /// Result of parsing model and data files
    /// </summary>
    public class ParseResult
    {
        public bool Success { get; set; }
        public int TotalSuccess { get; set; }
        public int TotalErrors { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string SummaryMessage { get; set; } = string.Empty;
        
        public bool HasErrors => TotalErrors > 0;
        public bool HasWarnings => Warnings.Count > 0;
    }
}