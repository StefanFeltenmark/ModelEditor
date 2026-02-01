using System;
using System.Collections.Generic;
using System.Text;


using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses OPL range declarations
    /// </summary>
    public class RangeParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;
        
        public RangeParser(ModelManager manager, ExpressionEvaluator eval)
        {
            modelManager = manager;
            evaluator = eval;
        }
        
        /// <summary>
        /// Tries to parse a range declaration
        /// Examples:
        ///   range Products = 1..100;
        ///   range TimeHorizons = 0..n;
        ///   range R = start..end;
        /// </summary>
        public bool TryParse(string statement, out OplRange? range, out string error)
        {
            range = null;
            error = string.Empty;
            
            statement = statement.Trim();
            
            // Check if it starts with "range"
            if (!statement.StartsWith("range", StringComparison.OrdinalIgnoreCase))
            {
                error = "Not a range declaration";
                return false;
            }
            
            // Pattern: range name = start..end
            string pattern = @"^range\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+?)\s*\.\.\s*(.+)$";
            var match = Regex.Match(statement, pattern, RegexOptions.IgnoreCase);
            
            if (!match.Success)
            {
                error = "Invalid range syntax. Expected: range Name = start..end";
                return false;
            }
            
            string rangeName = match.Groups[1].Value;
            string startStr = match.Groups[2].Value.Trim();
            string endStr = match.Groups[3].Value.Trim();
            
            // Parse start expression
            Expression startExpr;
            if (!TryParseRangeExpression(startStr, out startExpr, out string startError))
            {
                error = $"Invalid range start expression '{startStr}': {startError}";
                return false;
            }
            
            // Parse end expression
            Expression endExpr;
            if (!TryParseRangeExpression(endStr, out endExpr, out string endError))
            {
                error = $"Invalid range end expression '{endStr}': {endError}";
                return false;
            }
            
            // Create the range
            range = new OplRange(rangeName, startExpr, endExpr);
            
            // Validate the range (try to evaluate it)
            try
            {
                int start = range.GetStart(modelManager);
                int end = range.GetEnd(modelManager);
                
                if (start > end)
                {
                    // This is a warning, but we'll allow it (empty range)
                    System.Diagnostics.Debug.WriteLine($"Warning: Range '{rangeName}' has start ({start}) greater than end ({end})");
                }
            }
            catch (Exception ex)
            {
                error = $"Error evaluating range bounds: {ex.Message}";
                return false;
            }
            
            return true;
        }
        
        private bool TryParseRangeExpression(string exprStr, out Expression expr, out string error)
        {
            expr = new ConstantExpression(0);
            error = string.Empty;
            
            exprStr = exprStr.Trim();
            
            // Try to parse as integer constant
            if (int.TryParse(exprStr, out int intValue))
            {
                expr = new ConstantExpression(intValue);
                return true;
            }
            
            // Try to parse as parameter reference
            if (IsValidIdentifier(exprStr) && modelManager.Parameters.ContainsKey(exprStr))
            {
                expr = new ParameterExpression(exprStr);
                return true;
            }
            
            // Try to parse as arithmetic expression
            if (TryParseArithmeticExpression(exprStr, out expr))
            {
                return true;
            }
            
            // Try to evaluate as expression
            var evalResult = evaluator.EvaluateFloatExpression(exprStr);
            if (evalResult.IsSuccess)
            {
                expr = new ConstantExpression((int)Math.Round(evalResult.Value));
                return true;
            }
            
            error = $"Cannot parse range expression '{exprStr}'. Expected a number, parameter name, or simple arithmetic expression.";
            return false;
        }
        
        private bool IsValidIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name) || !char.IsLetter(name[0]))
                return false;
            
            return name.All(c => char.IsLetterOrDigit(c) || c == '_');
        }
        
        private bool TryParseArithmeticExpression(string exprStr, out Expression expr)
        {
            expr = new ConstantExpression(0);
            
            // Try to match simple patterns: param+num, param-num, num*param, param*num
            var patterns = new[]
            {
                (@"^([a-zA-Z][a-zA-Z0-9_]*)\s*\+\s*(\d+)$", BinaryOperator.Add, true),
                (@"^([a-zA-Z][a-zA-Z0-9_]*)\s*-\s*(\d+)$", BinaryOperator.Subtract, true),
                (@"^(\d+)\s*\+\s*([a-zA-Z][a-zA-Z0-9_]*)$", BinaryOperator.Add, false),
                (@"^(\d+)\s*\*\s*([a-zA-Z][a-zA-Z0-9_]*)$", BinaryOperator.Multiply, false),
                (@"^([a-zA-Z][a-zA-Z0-9_]*)\s*\*\s*(\d+)$", BinaryOperator.Multiply, true)
            };
            
            foreach (var (pattern, op, paramFirst) in patterns)
            {
                var match = Regex.Match(exprStr, pattern);
                if (match.Success)
                {
                    string part1 = match.Groups[1].Value;
                    string part2 = match.Groups[2].Value;
                    
                    Expression left, right;
                    
                    if (paramFirst)
                    {
                        // part1 is parameter, part2 is number
                        left = modelManager.Parameters.ContainsKey(part1)
                            ? new ParameterExpression(part1)
                            : new ConstantExpression(0);
                        right = int.TryParse(part2, out int num2)
                            ? new ConstantExpression(num2)
                            : new ConstantExpression(0);
                    }
                    else
                    {
                        // part1 is number, part2 is parameter
                        left = int.TryParse(part1, out int num1)
                            ? new ConstantExpression(num1)
                            : new ConstantExpression(0);
                        right = modelManager.Parameters.ContainsKey(part2)
                            ? new ParameterExpression(part2)
                            : new ConstantExpression(0);
                    }
                    
                    expr = new BinaryExpression(left, op, right);
                    return true;
                }
            }
            
            return false;
        }
    }
}
