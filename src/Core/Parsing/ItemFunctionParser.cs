using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses item() function calls
    /// Syntax: item(setName, keyExpression)
    /// Examples:
    ///   item(nodes, 0)
    ///   item(HydroArcs, <"A1">)
    ///   item(HydroArcTs, <s.id, t>)
    /// </summary>
    public static class ItemFunctionParser
    {
        public static bool TryParse(string expression, ModelManager manager, out Expression? result, out string error)
        {
            result = null;
            error = string.Empty;

            expression = expression.Trim();

            // Pattern: item(setName, keyExpression)
            if (!expression.StartsWith("item(", StringComparison.OrdinalIgnoreCase))
            {
                error = "Not an item() function";
                return false;
            }

            if (!expression.EndsWith(")"))
            {
                error = "item() function must end with ')'";
                return false;
            }

            // Extract content between parentheses
            string content = expression.Substring(5, expression.Length - 6).Trim();

            // Split by comma at top level (not inside <>, (), [])
            var parts = SplitTopLevel(content, ',');

            if (parts.Count != 2)
            {
                error = $"item() expects 2 arguments (setName, key), got {parts.Count}";
                return false;
            }

            string setName = parts[0].Trim();
            string keyExpr = parts[1].Trim();

            // Validate set name
            if (!Regex.IsMatch(setName, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
            {
                error = $"Invalid set name: '{setName}'";
                return false;
            }

            // Parse key expression
            Expression keyExpression;

            if (keyExpr.StartsWith("<") && keyExpr.EndsWith(">"))
            {
                // Composite key: <expr1, expr2, ...>
                keyExpression = ParseCompositeKey(keyExpr, manager, out error);
                if (keyExpression == null)
                    return false;
            }
            else
            {
                // Simple key expression
                keyExpression = ParseKeyExpression(keyExpr, manager, out error);
                if (keyExpression == null)
                    return false;
            }

            result = new ItemFunctionExpression(setName, keyExpression);
            return true;
        }

        private static Expression? ParseCompositeKey(string keyExpr, ModelManager manager, out string error)
        {
            error = string.Empty;

            // Remove angle brackets
            string content = keyExpr.Substring(1, keyExpr.Length - 2).Trim();

            // Split by comma at top level
            var parts = SplitTopLevel(content, ',');

            if (parts.Count == 0)
            {
                error = "Empty composite key";
                return null;
            }

            var keyParts = new List<Expression>();

            foreach (var part in parts)
            {
                var expr = ParseKeyExpression(part.Trim(), manager, out error);
                if (expr == null)
                    return null;

                keyParts.Add(expr);
            }

            return new CompositeKeyExpression(keyParts);
        }

        private static Expression? ParseKeyExpression(string exprStr, ModelManager manager, out string error)
        {
            error = string.Empty;
            exprStr = exprStr.Trim();

            // Check for nested item()
            if (exprStr.StartsWith("item(", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParse(exprStr, manager, out var nestedItem, out error))
                    return nestedItem;
                return null;
            }

            // Check for item().field
            if (exprStr.Contains("item(") && exprStr.Contains(")."))
            {
                var match = Regex.Match(exprStr, @"^(item\([^)]+\))\.([a-zA-Z][a-zA-Z0-9_]*)$");
                if (match.Success)
                {
                    string itemPart = match.Groups[1].Value;
                    string fieldName = match.Groups[2].Value;

                    if (TryParse(itemPart, manager, out var itemExpr, out error))
                    {
                        return new ItemFieldAccessExpression((ItemFunctionExpression)itemExpr!, fieldName);
                    }
                    return null;
                }
            }

            // Check for tuple field access: variable.field
            if (Regex.IsMatch(exprStr, @"^([a-zA-Z][a-zA-Z0-9_]*)\.([a-zA-Z][a-zA-Z0-9_]*)$"))
            {
                var match = Regex.Match(exprStr, @"^([a-zA-Z][a-zA-Z0-9_]*)\.([a-zA-Z][a-zA-Z0-9_]*)$");
                string varName = match.Groups[1].Value;
                string fieldName = match.Groups[2].Value;

                return new DynamicTupleFieldAccessExpression(varName, fieldName);
            }

            // Check for string literal
            if ((exprStr.StartsWith("\"") && exprStr.EndsWith("\"")) ||
                (exprStr.StartsWith("'") && exprStr.EndsWith("'")))
            {
                return new StringConstantExpression(exprStr.Trim('"', '\''));
            }

            // Check for numeric literal
            if (double.TryParse(exprStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double numValue))
            {
                return new ConstantExpression(numValue);
            }

            // Check for parameter
            if (manager.Parameters.ContainsKey(exprStr))
            {
                return new ParameterExpression(exprStr);
            }

            // Assume it's an iterator variable
            return new ParameterExpression(exprStr);
        }

        private static List<string> SplitTopLevel(string input, char delimiter)
        {
            var parts = new List<string>();
            var current = new System.Text.StringBuilder();
            int depth = 0;
            int angleDepth = 0;
            int bracketDepth = 0;
            bool inQuotes = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '"' && (i == 0 || input[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                }
                else if (!inQuotes)
                {
                    if (c == '(') depth++;
                    else if (c == ')') depth--;
                    else if (c == '<') angleDepth++;
                    else if (c == '>') angleDepth--;
                    else if (c == '[') bracketDepth++;
                    else if (c == ']') bracketDepth--;
                    else if (c == delimiter && depth == 0 && angleDepth == 0 && bracketDepth == 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                        continue;
                    }

                    current.Append(c);
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                parts.Add(current.ToString());
            }

            return parts;
        }
    }
}