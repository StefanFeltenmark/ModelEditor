using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses multi-dimensional indexed parameters and sets
    /// </summary>
    public class MultiDimensionalParser
    {
        private readonly ModelManager modelManager;

        public MultiDimensionalParser(ModelManager manager)
        {
            modelManager = manager;
        }

        /// <summary>
        /// Tries to parse an indexed parameter declaration
        /// Example: ArcTData arcT[s in HydroArcs][t in T] = item(HydroArcTs, <s.id,t>);
        /// </summary>
        public bool TryParseIndexedParameter(string statement, out Parameter? param, out string error)
        {
            param = null;
            error = string.Empty;

            // Pattern: Type name[var in Set][var2 in Set2]... = expr;
            var pattern = @"^([a-zA-Z][a-zA-Z0-9_]*)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*(\[.+\])\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not an indexed parameter declaration";
                return false;
            }

            string typeName = match.Groups[1].Value;
            string paramName = match.Groups[2].Value;
            string dimensionsStr = match.Groups[3].Value;
            string valueStr = match.Groups[4].Value.Trim();

            // Determine parameter type
            ParameterType paramType = typeName.ToLower() switch
            {
                "int" => ParameterType.Integer,
                "float" => ParameterType.Float,
                "string" => ParameterType.String,
                "bool" => ParameterType.Boolean,
                _ => ParameterType.Float
            };

            // Parse dimensions [var in Set][var2 in Set2]...
            var indexSetNames = new List<string>();
            if (!ParseDimensions(dimensionsStr, indexSetNames, out error))
            {
                return false;
            }

            bool isExternal = valueStr == "...";

            // Create unified Parameter with multiple dimensions
            param = new Parameter(paramName, paramType, indexSetNames, isExternal);

            return true;
        }

       

        /// <summary>
        /// Parses dimension specifications like [i in Set][j in Set2] or [Set1][Set2]
        /// </summary>
        private bool ParseDimensions(string dimensionsStr, List<string> indexSetNames, out string error)
        {
            error = string.Empty;

            // Extract all bracket contents
            var bracketPattern = @"\[([^\]]+)\]";
            var bracketMatches = Regex.Matches(dimensionsStr, bracketPattern);

            if (bracketMatches.Count == 0)
            {
                error = "Invalid dimension specification";
                return false;
            }

            foreach (Match match in bracketMatches)
            {
                string content = match.Groups[1].Value.Trim();

                // Check for iterator syntax: "var in Set" or "var in 1..5"
                var iterMatch = Regex.Match(content, @"^[a-zA-Z][a-zA-Z0-9_]*\s+in\s+(.+)$");
                if (iterMatch.Success)
                {
                    indexSetNames.Add(iterMatch.Groups[1].Value.Trim());
                }
                else if (Regex.IsMatch(content, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
                {
                    // Simple set/range name
                    indexSetNames.Add(content);
                }
                else
                {
                    error = $"Invalid dimension: '{content}'";
                    return false;
                }
            }

            return true;
        }

        

       
    }
}