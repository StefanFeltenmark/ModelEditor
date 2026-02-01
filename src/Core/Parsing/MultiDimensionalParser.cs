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
        public bool TryParseIndexedParameter(string statement, out IndexedParameter? param, out string error)
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

            // Check if it's a tuple type (for typed parameters)
            ParameterType paramType = ParameterType.Float; // Default
            bool isTupleType = modelManager.TupleSchemas.ContainsKey(typeName);

            if (!isTupleType)
            {
                // It's a primitive type
                paramType = typeName.ToLower() switch
                {
                    "int" => ParameterType.Integer,
                    "float" => ParameterType.Float,
                    "string" => ParameterType.String,
                    "bool" => ParameterType.Boolean,
                    _ => ParameterType.Float
                };
            }

            param = new IndexedParameter(paramName, paramType);

            // Parse dimensions [var in Set][var2 in Set2]...
            if (!ParseDimensions(dimensionsStr, param.Dimensions, out error))
            {
                return false;
            }

            // Check if it's external
            if (valueStr == "...")
            {
                param.IsExternal = true;
            }
            else
            {
                // TODO: Parse the value expression
                // For now, just mark as external
                param.IsExternal = true;
            }

            return true;
        }

        /// <summary>
        /// Tries to parse an indexed set collection
        /// Example: {Arc} Jin[i in hydroNodeIndices] = {j | j in HydroArcs: j.toHydroNode == i};
        /// </summary>
        public bool TryParseIndexedSet(string statement, out IndexedSetCollection? setCollection, out string error)
        {
            setCollection = null;
            error = string.Empty;

            // Pattern: {Type} name[var in Set][var2 in Set2]... = expr;
            var pattern = @"^\{([a-zA-Z][a-zA-Z0-9_]*)\}\s+([a-zA-Z][a-zA-Z0-9_]*)\s*(\[.+?\](?:\[.+?\])*)\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not an indexed set declaration";
                return false;
            }

            string elementType = match.Groups[1].Value;
            string setName = match.Groups[2].Value;
            string dimensionsStr = match.Groups[3].Value;
            string valueStr = match.Groups[4].Value.Trim();

            setCollection = new IndexedSetCollection(setName, elementType);

            // Parse dimensions
            if (!ParseDimensions(dimensionsStr, setCollection.Dimensions, out error))
            {
                return false;
            }

            // Store value expression as external for now
            // TODO: Parse comprehension or value expression
            setCollection.ValueExpression = null;

            return true;
        }

        /// <summary>
        /// Parses dimension specifications like [i in Set][j in Set2]
        /// </summary>
        private bool ParseDimensions(string dimensionsStr, List<IndexDimension> dimensions, out string error)
        {
            error = string.Empty;

            // Pattern to match individual dimensions: [var in Set]
            var dimensionPattern = @"\[([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\]";
            var matches = Regex.Matches(dimensionsStr, dimensionPattern);

            if (matches.Count == 0)
            {
                error = "Invalid dimension specification";
                return false;
            }

            foreach (Match match in matches)
            {
                string iteratorVar = match.Groups[1].Value;
                string setName = match.Groups[2].Value;

                dimensions.Add(new IndexDimension(iteratorVar, setName));
            }

            return true;
        }

        /// <summary>
        /// Tries to parse a multi-dimensional external parameter
        /// Example: float productionCost[stations][T][priceSegment] = ...;
        /// </summary>
        public bool TryParseExternalMultiDimParameter(string statement, out IndexedParameter? param, out string error)
        {
            param = null;
            error = string.Empty;

            // Pattern: type name[Set1][Set2][Set3]... = ...;
            var pattern = @"^(int|float|string|bool)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*(\[.+\])\s*=\s*\.\.\.$";
            var match = Regex.Match(statement.Trim(), pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                error = "Not an external multi-dimensional parameter";
                return false;
            }

            string typeStr = match.Groups[1].Value.ToLower();
            string paramName = match.Groups[2].Value;
            string dimensionsStr = match.Groups[3].Value;

            ParameterType paramType = typeStr switch
            {
                "int" => ParameterType.Integer,
                "float" => ParameterType.Float,
                "string" => ParameterType.String,
                "bool" => ParameterType.Boolean,
                _ => ParameterType.Float
            };

            param = new IndexedParameter(paramName, paramType);
            param.IsExternal = true;

            // Parse dimensions [Set1][Set2][Set3]
            if (!ParseSimpleDimensions(dimensionsStr, param.Dimensions, out error))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses simple dimension specs like [Set1][Set2] (without "var in")
        /// </summary>
        private bool ParseSimpleDimensions(string dimensionsStr, List<IndexDimension> dimensions, out string error)
        {
            error = string.Empty;

            // Pattern to match: [SetName]
            var dimensionPattern = @"\[([a-zA-Z][a-zA-Z0-9_]*)\]";
            var matches = Regex.Matches(dimensionsStr, dimensionPattern);

            if (matches.Count == 0)
            {
                error = "Invalid dimension specification";
                return false;
            }

            int index = 0;
            foreach (Match match in matches)
            {
                string setName = match.Groups[1].Value;
                string generatedVar = $"_idx{index++}"; // Generate iterator variable

                dimensions.Add(new IndexDimension(generatedVar, setName));
            }

            return true;
        }
    }
}