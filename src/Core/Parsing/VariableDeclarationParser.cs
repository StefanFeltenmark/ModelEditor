using System.Globalization;
using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    public class VariableDeclarationParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;

        public VariableDeclarationParser(ModelManager manager, ExpressionEvaluator eval)
        {
            modelManager = manager;
            evaluator = eval;
        }

        public bool TryParse(string statement, out IndexedVariable? variable, out string error)
        {
            variable = null;
            error = string.Empty;

            // Try two-dimensional indexed variable
            if (TryParseTwoDimensionalVariable(statement, out variable, out error))
            {
                return variable != null || !error.Equals("Not a variable declaration");
            }

            // Try single-dimensional indexed variable
            if (TryParseSingleDimensionalVariable(statement, out variable, out error))
            {
                return variable != null || !error.Equals("Not a variable declaration");
            }

            // Try scalar variable
            return TryParseScalarVariable(statement, out variable, out error);
        }

        private bool TryParseTwoDimensionalVariable(string statement, out IndexedVariable? variable, out string error)
        {
            variable = null;
            error = string.Empty;

            string pattern = @"^\s*var\s+(?:(float|int|bool)\s+)?([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\](?:\s+in\s+(.+))?$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not a variable declaration";
                return false;
            }

            string typeStr = match.Groups[1].Value;
            string varName = match.Groups[2].Value;
            string indexSetName1 = match.Groups[3].Value;
            string indexSetName2 = match.Groups[4].Value;
            string boundsStr = match.Groups[5].Value;

            VariableType varType = ParseVariableType(typeStr);

            if (!modelManager.IndexSets.ContainsKey(indexSetName1))
            {
                error = $"First index set '{indexSetName1}' is not declared";
                return false;
            }

            if (!modelManager.IndexSets.ContainsKey(indexSetName2))
            {
                error = $"Second index set '{indexSetName2}' is not declared";
                return false;
            }

            double? lower = null, upper = null;
            if (!string.IsNullOrEmpty(boundsStr))
            {
                if (!TryParseBounds(boundsStr, out lower, out upper, out error))
                {
                    return false;
                }
            }

            variable = new IndexedVariable(varName, indexSetName1, varType, indexSetName2, lower, upper);
            return true;
        }

        private bool TryParseSingleDimensionalVariable(string statement, out IndexedVariable? variable, out string error)
        {
            variable = null;
            error = string.Empty;

            string pattern = @"^\s*var\s+(?:(float|int|bool)\s+)?([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\](?:\s+in\s+(.+))?$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not a variable declaration";
                return false;
            }

            string typeStr = match.Groups[1].Value;
            string varName = match.Groups[2].Value;
            string indexSetName = match.Groups[3].Value;
            string boundsStr = match.Groups[4].Value;

            VariableType varType = ParseVariableType(typeStr);

            if (!modelManager.IndexSets.ContainsKey(indexSetName))
            {
                error = $"Index set '{indexSetName}' is not declared";
                return true; // ← Changed from false to true - it IS a variable declaration, just invalid
            }

            double? lower = null, upper = null;
            if (!string.IsNullOrEmpty(boundsStr))
            {
                if (!TryParseBounds(boundsStr, out lower, out upper, out error))
                {
                    return true; // ← Also return true here - it's a variable declaration with invalid bounds
                }
            }

            variable = new IndexedVariable(varName, indexSetName, varType, null, lower, upper);
            return true;
        }

        private bool TryParseScalarVariable(string statement, out IndexedVariable? variable, out string error)
        {
            variable = null;
            error = string.Empty;

            string pattern = @"^\s*var\s+(?:(float|int|bool)\s+)?([a-zA-Z][a-zA-Z0-9_]*)(?:\s+in\s+(.+))?$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not a variable declaration";
                return false;
            }

            string typeStr = match.Groups[1].Value;
            string varName = match.Groups[2].Value;
            string boundsStr = match.Groups[3].Value;

            VariableType varType = ParseVariableType(typeStr);

            double? lower = null, upper = null;
            if (!string.IsNullOrEmpty(boundsStr))
            {
                if (!TryParseBounds(boundsStr, out lower, out upper, out error))
                {
                    return false;
                }
            }

            // Scalar variables use empty string for IndexSetName
            variable = new IndexedVariable(varName, string.Empty, varType, null, lower, upper);
            return true;
        }

        private VariableType ParseVariableType(string typeStr)
        {
            if (string.IsNullOrEmpty(typeStr))
                return VariableType.Float;

            return typeStr.ToLower() switch
            {
                "float" => VariableType.Float,
                "int" => VariableType.Integer,
                "bool" => VariableType.Boolean,
                _ => VariableType.Float
            };
        }

        private bool TryParseBounds(string boundsStr, out double? lower, out double? upper, out string error)
        {
            lower = null;
            upper = null;
            error = string.Empty;

            // Pattern to match bounds: lower..upper
            var boundsMatch = Regex.Match(boundsStr,
                @"^\s*(-?\d+(?:\.\d+)?|[a-zA-Z][a-zA-Z0-9_]*)\s*\.\.\s*(-?\d+(?:\.\d+)?|[a-zA-Z][a-zA-Z0-9_]*)\s*$");

            if (!boundsMatch.Success)
            {
                error = "Invalid bounds format. Expected: in lower..upper";
                return false;
            }

            string lowerStr = boundsMatch.Groups[1].Value;
            string upperStr = boundsMatch.Groups[2].Value;

            // Parse lower bound
            if (!TryParseBoundValue(lowerStr, out lower, out error))
            {
                error = $"Lower bound '{lowerStr}': {error}";
                return false;
            }

            // Parse upper bound
            if (!TryParseBoundValue(upperStr, out upper, out error))
            {
                error = $"Upper bound '{upperStr}': {error}";
                return false;
            }

            if (lower > upper)
            {
                error = $"Lower bound ({lower}) cannot be greater than upper bound ({upper})";
                return false;
            }

            return true;
        }

        private bool TryParseBoundValue(string valueStr, out double? value, out string error)
        {
            value = null;
            error = string.Empty;

            // Try to parse as a number
            if (double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double numValue))
            {
                value = numValue;
                return true;
            }

            // Try to parse as a parameter reference
            if (modelManager.Parameters.TryGetValue(valueStr, out var param))
            {
                if (param.Type == ParameterType.Integer || param.Type == ParameterType.Float)
                {
                    value = Convert.ToDouble(param.Value);
                    return true;
                }
                else
                {
                    error = $"Parameter '{valueStr}' must be numeric for bounds";
                    return false;
                }
            }

            error = $"'{valueStr}' is not a valid number or declared parameter";
            return false;
        }
    }
}