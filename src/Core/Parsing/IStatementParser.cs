namespace Core.Parsing
{
    /// <summary>
    /// Common interface for all statement parsers
    /// </summary>
    public interface IStatementParser
    {
        /// <summary>
        /// Attempts to parse a statement
        /// </summary>
        /// <returns>True if successfully parsed (even if result is null), false if not recognized</returns>
        bool TryParse(string statement, out string error);
        
        /// <summary>
        /// Indicates the order priority for trying this parser (lower = try first)
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// Name of this parser for error messages
        /// </summary>
        string ParserName { get; }
    }
}