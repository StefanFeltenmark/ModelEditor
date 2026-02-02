using System.Text;
using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    public class ItemFunctionParser
    {
        /// <summary>
        /// Parses item(setName, key) expressions
        /// Supports nested item() and composite keys
        /// </summary>
        public static bool TryParse(string expression, ModelManager manager, 
            out Expression? result, out string error)
        {
            result = null;
            error = string.Empty;

            // Pattern: item(setName, keyExpression)
            var pattern = @"^item\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*(.+)\s*\)$";
            var match = Regex.Match(expression.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not an item() function call";
                return false;
            }

            string setName = match.Groups[1].Value;
            string keyPart = match.Groups[2].Value.Trim();

            // Parse the key expression (may be <...> composite key)
            Expression keyExpr;
            if (keyPart.StartsWith("<") && keyPart.EndsWith(">"))
            {
                // Composite key
                if (!ParseCompositeKey(keyPart, manager, out keyExpr, out error))
                    return false;
            }
            else
            {
                // Single key expression
                if (!ParseSingleKeyExpression(keyPart, manager, out keyExpr, out error))
                    return false;
            }

            result = new ItemFunctionExpression(setName, keyExpr);
            return true;
        }

        private static bool ParseCompositeKey(string keyExpression, ModelManager manager,
            out Expression? result, out string error)
        {
            result = null;
            error = string.Empty;

            // Remove angle brackets
            string inner = keyExpression.Substring(1, keyExpression.Length - 2).Trim();

            // Split by commas (respecting nested structures)
            var parts = SplitByComma(inner);
            var keyExpressions = new List<Expression>();

            foreach (var part in parts)
            {
                if (!ParseSingleKeyExpression(part.Trim(), manager, out var expr, out error))
                    return false;

                keyExpressions.Add(expr);
            }

            result = new CompositeKeyExpression(keyExpressions);
            return true;
        }

        private static bool ParseSingleKeyExpression(string expr, ModelManager manager,
            out Expression? result, out string error)
        {
            result = null;
            error = string.Empty;

            expr = expr.Trim();

            // Check for nested item() function
            if (expr.StartsWith("item("))
            {
                return TryParse(expr, manager, out result, out error);
            }

            // Check for field access: var.field OR array[i].field OR array[i][j].field
            if (expr.Contains('.'))
            {
                // Find the LAST dot (to handle cases like item(...).field)
                int lastDotIndex = expr.LastIndexOf('.');
                if (lastDotIndex > 0 && lastDotIndex < expr.Length - 1)
                {
                    string variablePart = expr.Substring(0, lastDotIndex);
                    string fieldName = expr.Substring(lastDotIndex + 1);

                    // Parse the variable part - it might have indices
                    Expression baseExpression;
                    
                    // Check if variable part has indexing: array[i] or array[i][j]
                    if (variablePart.Contains('['))
                    {
                        // Parse indexed access
                        if (!ParseIndexedExpression(variablePart, manager, out baseExpression, out error))
                            return false;
                    }
                    else
                    {
                        // Simple variable reference
                        if (manager.Parameters.ContainsKey(variablePart))
                        {
                            baseExpression = new ParameterExpression(variablePart);
                        }
                        else if (manager.TupleParameters != null && 
                                 manager.TupleParameters.ContainsKey(variablePart))
                        {
                            // It's a tuple parameter (iterator variable or tuple param)
                            baseExpression = new TupleVariableExpression(variablePart);
                        }
                        else
                        {
                            // Assume it's an iterator variable from forall context
                            baseExpression = new TupleVariableExpression(variablePart);
                        }
                    }

                    // Create field access on the base expression
                    result = new TupleFieldAccessExpression(baseExpression, fieldName);
                    return true;
                }
            }

            // Check for numeric constant
            if (double.TryParse(expr, out double value))
            {
                result = new ConstantExpression(value);
                return true;
            }

            // Check for parameter
            if (manager.Parameters.ContainsKey(expr))
            {
                result = new ParameterExpression(expr);
                return true;
            }

            // Assume it's a variable/iterator reference
            result = new ParameterExpression(expr);
            return true;
        }

        /// <summary>
        /// Parses indexed expressions like array[i], array[i][j], param[n.stage]
        /// </summary>
        private static bool ParseIndexedExpression(string expr, ModelManager manager,
            out Expression? result, out string error)
        {
            result = null;
            error = string.Empty;

            // Pattern: baseName[index1][index2]...
            var match = Regex.Match(expr, @"^([a-zA-Z][a-zA-Z0-9_]*)((?:\[[^\]]+\])+)$");
            
            if (!match.Success)
            {
                error = $"Invalid indexed expression: {expr}";
                return false;
            }

            string baseName = match.Groups[1].Value;
            string indicesPart = match.Groups[2].Value;

            // Extract individual indices
            var indexMatches = Regex.Matches(indicesPart, @"\[([^\]]+)\]");
            var indices = new List<Expression>();

            foreach (Match indexMatch in indexMatches)
            {
                string indexExpr = indexMatch.Groups[1].Value.Trim();
                
                // Parse each index expression recursively
                if (!ParseSingleKeyExpression(indexExpr, manager, out var indexExpression, out error))
                    return false;

                indices.Add(indexExpression);
            }

            // Create appropriate indexed expression based on what we're indexing
            if (manager.Parameters.ContainsKey(baseName))
            {
                var param = manager.Parameters[baseName];
                
                if (indices.Count == 1)
                {
                    result = new IndexedParameterExpression(baseName, indices[0]);
                }
                else if (indices.Count == 2)
                {
                    result = new IndexedParameterExpression(baseName, indices[0], indices[1]);
                }
                else
                {
                    // N-dimensional
                    result = new IndexedParameterExpression(baseName, indices);
                }
            }
            else if (manager.TupleParameters != null && 
                     manager.TupleParameters.ContainsKey(baseName))
            {
                // Indexed tuple parameter
                result = new IndexedTupleParameterExpression(baseName, indices);
            }
            else
            {
                // Assume it's an indexed variable or tuple set access
                result = new IndexedTupleParameterExpression(baseName, indices);
            }

            return true;
        }

        private static List<string> SplitByComma(string input)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            int depth = 0;
            int angleBracketDepth = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == '<') angleBracketDepth++;
                else if (c == '>') angleBracketDepth--;
                else if (c == ',' && depth == 0 && angleBracketDepth == 0)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
                result.Add(current.ToString().Trim());

            return result;
        }
    }
}