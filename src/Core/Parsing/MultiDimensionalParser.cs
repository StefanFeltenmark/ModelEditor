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
        /// Parses dimension specifications like [i in Set][j in Set2]
        /// </summary>
        private bool ParseDimensions(string dimensionsStr, List<string> indexSetNames, out string error)
        {
            error = string.Empty;

            // Pattern: [var in Set]
            var dimensionPattern = @"\[([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\]";
            var matches = Regex.Matches(dimensionsStr, dimensionPattern);

            if (matches.Count == 0)
            {
                error = "Invalid dimension specification";
                return false;
            }

            foreach (Match match in matches)
            {
                string setName = match.Groups[2].Value;
                indexSetNames.Add(setName);
            }

            return true;
        }

        

       
    }
}