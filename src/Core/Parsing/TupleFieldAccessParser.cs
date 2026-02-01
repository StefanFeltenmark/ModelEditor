using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Utility for parsing tuple field access patterns
    /// </summary>
    public static class TupleFieldAccessParser
    {
        /// <summary>
        /// Checks if an expression contains tuple field access
        /// Examples: n.prob, j.arcindex, s.id
        /// </summary>
        public static bool IsTupleFieldAccess(string expression)
        {
            // Pattern: identifier.identifier (but not numbers or complex expressions)
            var pattern = @"^([a-zA-Z][a-zA-Z0-9_]*)\.([a-zA-Z][a-zA-Z0-9_]*)$";
            return Regex.IsMatch(expression.Trim(), pattern);
        }
        
        /// <summary>
        /// Parses a tuple field access expression
        /// </summary>
        public static bool TryParse(string expression, out string variableName, out string fieldName)
        {
            variableName = string.Empty;
            fieldName = string.Empty;
            
            var pattern = @"^([a-zA-Z][a-zA-Z0-9_]*)\.([a-zA-Z][a-zA-Z0-9_]*)$";
            var match = Regex.Match(expression.Trim(), pattern);
            
            if (match.Success)
            {
                variableName = match.Groups[1].Value;
                fieldName = match.Groups[2].Value;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Replaces all tuple field accesses in an expression with tokens
        /// </summary>
        public static string TokenizeTupleFieldAccesses(
            string expression, 
            TokenManager tokenManager,
            ModelManager modelManager,
            Dictionary<string, object>? context = null)
        {
            // Pattern to match tuple field access: variable.field
            var pattern = @"\b([a-zA-Z][a-zA-Z0-9_]*)\.([a-zA-Z][a-zA-Z0-9_]*)\b";
            
            return Regex.Replace(expression, pattern, match =>
            {
                string varName = match.Groups[1].Value;
                string fieldName = match.Groups[2].Value;
                
                // Check if this looks like a tuple field access
                // (not a method call or other construct)
                if (IsLikelyTupleFieldAccess(varName, fieldName, modelManager, context))
                {
                    var tupleAccessExpr = new DynamicTupleFieldAccessExpression(varName, fieldName);
                    return tokenManager.CreateToken(tupleAccessExpr, "TUPLE");
                }
                
                return match.Value;
            });
        }
        
        private static bool IsLikelyTupleFieldAccess(
            string varName, 
            string fieldName, 
            ModelManager modelManager,
            Dictionary<string, object>? context)
        {
            // Check if variable is in context (iterator variable)
            if (context != null && context.ContainsKey(varName))
            {
                return context[varName] is TupleInstance;
            }
            
            // Check if it's a known tuple set
            if (modelManager.TupleSets.ContainsKey(varName))
            {
                return false; // This would be set.field, not instance.field
            }
            
            // Check if it's a parameter holding a tuple
            if (modelManager.Parameters.TryGetValue(varName, out var param))
            {
                return param.Value is TupleInstance;
            }
            
            // Default: assume it's tuple field access
            return true;
        }
        
        /// <summary>
        /// Extracts all tuple field accesses from an expression
        /// </summary>
        public static List<(string variable, string field)> ExtractTupleFieldAccesses(string expression)
        {
            var results = new List<(string, string)>();
            var pattern = @"\b([a-zA-Z][a-zA-Z0-9_]*)\.([a-zA-Z][a-zA-Z0-9_]*)\b";
            var matches = Regex.Matches(expression, pattern);
            
            foreach (Match match in matches)
            {
                string varName = match.Groups[1].Value;
                string fieldName = match.Groups[2].Value;
                results.Add((varName, fieldName));
            }
            
            return results;
        }
    }
}