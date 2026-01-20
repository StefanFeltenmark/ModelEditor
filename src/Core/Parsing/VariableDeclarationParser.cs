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

            // OPL-style: dvar float+ x[I] in 0..100;
            // Your style: var float x[I] in 0..100;
            
            // Pattern for 2D variables with OPL syntax
            string twoDimPattern = @"^\s*(dvar|var)\s+(?:(float|int|bool)([+]?)\s+)?([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\](?:\s+in\s+(.+))?$";
            var twoDimMatch = Regex.Match(statement.Trim(), twoDimPattern);

            if (twoDimMatch.Success)
            {
                string keyword = twoDimMatch.Groups[1].Value; // "dvar" or "var"
                string typeStr = twoDimMatch.Groups[2].Value;
                string domainQualifier = twoDimMatch.Groups[3].Value; // "+" for non-negative
                string varName = twoDimMatch.Groups[4].Value;
                string indexSetName1 = twoDimMatch.Groups[5].Value;
                string indexSetName2 = twoDimMatch.Groups[6].Value;
                string boundsStr = twoDimMatch.Groups[7].Value;

                VariableType varType = VariableType.Float;
                if (!string.IsNullOrEmpty(typeStr))
                {
                    varType = typeStr.ToLower() switch
                    {
                        "float" => VariableType.Float,
                        "int" => VariableType.Integer,
                        "bool" => VariableType.Boolean,
                        _ => VariableType.Float
                    };
                }

                // Validate index sets
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
                
                // Handle domain qualifier (e.g., float+ means >= 0)
                if (domainQualifier == "+")
                {
                    lower = 0;
                }

                // Parse explicit bounds if provided
                if (!string.IsNullOrEmpty(boundsStr))
                {
                    if (!TryParseBounds(boundsStr, out var explicitLower, out var explicitUpper, out error))
                    {
                        return false;
                    }
                    
                    // Explicit bounds override domain qualifier
                    lower = explicitLower;
                    upper = explicitUpper;
                }

                variable = new IndexedVariable(varName, indexSetName1, varType, indexSetName2, lower, upper);
                return true;
            }

            // Single-dimensional pattern with OPL support
            string indexedPattern = @"^\s*(dvar|var)\s+(?:(float|int|bool)([+]?)\s+)?([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\](?:\s+in\s+(.+))?$";
            var indexedMatch = Regex.Match(statement.Trim(), indexedPattern);

            if (indexedMatch.Success)
            {
                string keyword = indexedMatch.Groups[1].Value;
                string typeStr = indexedMatch.Groups[2].Value;
                string domainQualifier = indexedMatch.Groups[3].Value;
                string varName = indexedMatch.Groups[4].Value;
                string indexSetName = indexedMatch.Groups[5].Value;
                string boundsStr = indexedMatch.Groups[6].Value;

                VariableType varType = VariableType.Float;
                if (!string.IsNullOrEmpty(typeStr))
                {
                    varType = typeStr.ToLower() switch
                    {
                        "float" => VariableType.Float,
                        "int" => VariableType.Integer,
                        "bool" => VariableType.Boolean,
                        _ => VariableType.Float
                    };
                }

                if (!modelManager.IndexSets.ContainsKey(indexSetName))
                {
                    error = $"Index set '{indexSetName}' is not declared";
                    return false;
                }

                double? lower = null, upper = null;
                
                if (domainQualifier == "+")
                {
                    lower = 0;
                }

                if (!string.IsNullOrEmpty(boundsStr))
                {
                    if (!TryParseBounds(boundsStr, out var explicitLower, out var explicitUpper, out error))
                    {
                        return false;
                    }
                    lower = explicitLower;
                    upper = explicitUpper;
                }

                variable = new IndexedVariable(varName, indexSetName, varType, null, lower, upper);
                return true;
            }

            // Scalar variable with OPL support
            string scalarPattern = @"^\s*(dvar|var)\s+(?:(float|int|bool)([+]?)\s+)?([a-zA-Z][a-zA-Z0-9_]*)(?:\s+in\s+(.+))?$";
            var scalarMatch = Regex.Match(statement.Trim(), scalarPattern);

            if (scalarMatch.Success)
            {
                string keyword = scalarMatch.Groups[1].Value;
                string typeStr = scalarMatch.Groups[2].Value;
                string domainQualifier = scalarMatch.Groups[3].Value;
                string varName = scalarMatch.Groups[4].Value;
                string boundsStr = scalarMatch.Groups[5].Value;

                VariableType varType = VariableType.Float;
                if (!string.IsNullOrEmpty(typeStr))
                {
                    varType = typeStr.ToLower() switch
                    {
                        "float" => VariableType.Float,
                        "int" => VariableType.Integer,
                        "bool" => VariableType.Boolean,
                        _ => VariableType.Float
                    };
                }

                double? lower = null, upper = null;
                
                if (domainQualifier == "+")
                {
                    lower = 0;
                }

                if (!string.IsNullOrEmpty(boundsStr))
                {
                    if (!TryParseBounds(boundsStr, out var explicitLower, out var explicitUpper, out error))
                    {
                        return false;
                    }
                    lower = explicitLower;
                    upper = explicitUpper;
                }

                variable = new IndexedVariable(varName, string.Empty, varType, null, lower, upper);
                return true;
            }

            error = "Not a variable declaration";
            return false;
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