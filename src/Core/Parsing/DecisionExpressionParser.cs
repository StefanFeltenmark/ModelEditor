using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses OPL decision expression (dexpr) declarations
    /// </summary>
    public class DecisionExpressionParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;
        
        public DecisionExpressionParser(ModelManager manager, ExpressionEvaluator eval)
        {
            modelManager = manager;
            evaluator = eval;
        }
        
        /// <summary>
        /// Tries to parse a decision expression declaration
        /// </summary>
        public bool TryParse(string statement, out DecisionExpression? dexpr, out string error)
        {
            dexpr = null;
            error = string.Empty;
            
            statement = statement.Trim();
            
            // Check if it starts with "dexpr"
            if (!statement.StartsWith("dexpr", StringComparison.OrdinalIgnoreCase))
            {
                error = "Not a decision expression declaration";
                return false;
            }
            
            // Pattern for indexed dexpr: dexpr type name[indexVar in IndexSet] = expression
            string indexedPattern = @"^dexpr\s+(int|float|bool|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\]\s*=\s*(.+)$";
            var indexedMatch = Regex.Match(statement, indexedPattern, RegexOptions.IgnoreCase);
            
            if (indexedMatch.Success)
            {
                bool success = ParseIndexedDexpr(indexedMatch, out dexpr, out error);
                if (success && dexpr != null)
                {
                    // Validate the dexpr
                    if (!ValidateDexpr(dexpr, out string validationError))
                    {
                        error = validationError;
                        dexpr = null;
                        return false;
                    }
                }
                return success;
            }
            
            // Pattern for scalar dexpr: dexpr type name = expression
            string scalarPattern = @"^dexpr\s+(int|float|bool|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
            var scalarMatch = Regex.Match(statement, scalarPattern, RegexOptions.IgnoreCase);
            
            if (scalarMatch.Success)
            {
                bool success = ParseScalarDexpr(scalarMatch, out dexpr, out error);
                if (success && dexpr != null)
                {
                    // Validate the dexpr
                    if (!ValidateDexpr(dexpr, out string validationError))
                    {
                        error = validationError;
                        dexpr = null;
                        return false;
                    }
                }
                return success;
            }
            
            error = "Invalid decision expression syntax. Expected: dexpr type name = expression";
            return false;
        }

        private bool ValidateDexpr(DecisionExpression dexpr, out string error)
        {
            error = string.Empty;
            
            // Basic validation - could be expanded
            if (string.IsNullOrEmpty(dexpr.Name))
            {
                error = "Decision expression must have a name";
                return false;
            }
            return true;
        }

        private bool ParseScalarDexpr(Match match, out DecisionExpression? dexpr, out string error)
        {
            dexpr = null;
            error = string.Empty;
            
            string typeStr = match.Groups[1].Value.ToLower();
            string name = match.Groups[2].Value;
            string exprStr = match.Groups[3].Value.Trim();
            
            // Parse type
            VariableType type = typeStr switch
            {
                "int" => VariableType.Integer,
                "float" => VariableType.Float,
                "bool" => VariableType.Boolean,
                "string" => VariableType.String,
                _ => VariableType.Float
            };
            
            // Parse expression
            Expression expr;
            try
            {
                expr = ParseExpression(exprStr, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"Error parsing expression: {ex.Message}";
                return false;
            }
            
            dexpr = new DecisionExpression(name, type, expr);
            return true;
        }
        
        private bool ParseIndexedDexpr(Match match, out DecisionExpression? dexpr, out string error)
        {
            dexpr = null;
            error = string.Empty;
            
            string typeStr = match.Groups[1].Value.ToLower();
            string name = match.Groups[2].Value;
            string indexVar = match.Groups[3].Value;
            string indexSetName = match.Groups[4].Value;
            string exprStr = match.Groups[5].Value.Trim();
            
            // Validate index set exists
            if (!modelManager.IndexSets.ContainsKey(indexSetName) && 
                !modelManager.Ranges.ContainsKey(indexSetName))
            {
                error = $"Index set or range '{indexSetName}' not found";
                return false;
            }
            
            // Parse type
            VariableType type = typeStr switch
            {
                "int" => VariableType.Integer,
                "float" => VariableType.Float,
                "bool" => VariableType.Boolean,
                "string" => VariableType.String,
                _ => VariableType.Float
            };
            
            // Parse expression
            Expression expr;
            try
            {
                expr = ParseExpression(exprStr, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"Error parsing expression: {ex.Message}";
                return false;
            }
            
            dexpr = new DecisionExpression(name, type, indexVar, expr);
            return true;
        }
        
        /// <summary>
        /// Parses an expression string into an Expression object
        /// CRITICAL: This handles summations over tuple sets as SummationExpression objects
        /// </summary>
        private Expression ParseExpression(string exprStr, out string error)
        {
            error = string.Empty;
            
            try
            {
                exprStr = exprStr.Trim();
                
                // CRITICAL: Check if this is a summation expression FIRST
                // Pattern: sum(indexVar in setName) body
                var sumMatch = Regex.Match(exprStr, 
                    @"^\s*sum\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\)\s*(.+)$",
                    RegexOptions.IgnoreCase);
                
                if (sumMatch.Success)
                {
                    string indexVar = sumMatch.Groups[1].Value;
                    string setName = sumMatch.Groups[2].Value;
                    string bodyStr = sumMatch.Groups[3].Value.Trim();
                    
                    // Remove surrounding parentheses if present
                    if (bodyStr.StartsWith("(") && bodyStr.EndsWith(")"))
                    {
                        bodyStr = bodyStr.Substring(1, bodyStr.Length - 2).Trim();
                    }
                    
                    // Validate that the set exists
                    if (!modelManager.TupleSets.ContainsKey(setName) &&
                        !modelManager.ComputedSets.ContainsKey(setName) &&
                        !modelManager.IndexSets.ContainsKey(setName) &&
                        !modelManager.Ranges.ContainsKey(setName) &&
                        !modelManager.Sets.ContainsKey(setName))
                    {
                        error = $"Set '{setName}' not found for summation";
                        return new ConstantExpression(0);
                    }
                    
                    // Recursively parse the body
                    Expression bodyExpr = ParseExpression(bodyStr, out error);
                    if (!string.IsNullOrEmpty(error))
                    {
                        return new ConstantExpression(0);
                    }
                    
                    // Create a SummationExpression - this will be evaluated at runtime
                    return new SummationExpression(indexVar, setName, bodyExpr);
                }
                
                // Check for tuple field access
                if (TupleFieldAccessParser.IsTupleFieldAccess(exprStr))
                {
                    if (TupleFieldAccessParser.TryParse(exprStr, out string varName, out string fieldName))
                    {
                        return new DynamicTupleFieldAccessExpression(varName, fieldName);
                    }
                }
                
                // Check for binary expressions (with tuple field access support)
                if (TryParseBinaryExpression(exprStr, out var binaryExpr, out error))
                {
                    return binaryExpr;
                }
                
                // Try constant
                if (double.TryParse(exprStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double constValue))
                {
                    return new ConstantExpression(constValue);
                }
                
                // Try parameter
                if (modelManager.Parameters.ContainsKey(exprStr))
                {
                    return new ParameterExpression(exprStr);
                }
                
                // Try decision expression
                if (modelManager.DecisionExpressions.ContainsKey(exprStr))
                {
                    return new DecisionExpressionExpression(exprStr);
                }
                
                // For non-tuple summations, try to expand them
                var summationExpander = new SummationExpander(modelManager);
                string expanded = summationExpander.ExpandSummations(exprStr, out error);
                
                if (!string.IsNullOrEmpty(error))
                    return new ConstantExpression(0);
                
                // If expansion happened, try parsing again
                if (expanded != exprStr)
                {
                    exprStr = expanded;
                }
                
                // Use expression parser for complex expressions
                var expressionParser = new ExpressionParser(modelManager);
                
                if (expressionParser.TryParseExpression(exprStr, out var coeffs, out var constant, out error))
                {
                    // If it's a pure constant
                    if (coeffs.Count == 0)
                    {
                        return constant;
                    }
                    
                    // If it's a single variable with coefficient 1
                    if (coeffs.Count == 1 && constant is ConstantExpression c && Math.Abs(c.Value) < 1e-10)
                    {
                        var kvp = coeffs.First();
                        if (kvp.Value is ConstantExpression coef && Math.Abs(coef.Value - 1.0) < 1e-10)
                        {
                            return new VariableExpression(kvp.Key);
                        }
                    }
                    
                    // For complex expressions, build a composite expression
                    return BuildCompositeExpression(coeffs, constant);
                }
                
                error = "Could not parse decision expression body";
                return new ConstantExpression(0);
            }
            catch (Exception ex)
            {
                error = $"Error parsing expression: {ex.Message}";
                return new ConstantExpression(0);
            }
        }

        /// <summary>
        /// Tries to parse a binary expression (with support for tuple field access)
        /// </summary>
        private bool TryParseBinaryExpression(string exprStr, out Expression? result, out string error)
        {
            result = null;
            error = string.Empty;

            // Simple binary operations: +, -, *, /
            var operators = new[] { "+", "-", "*", "/" };

            foreach (var op in operators)
            {
                int opIndex = FindOperatorIndex(exprStr, op);
                if (opIndex > 0 && opIndex < exprStr.Length - 1)
                {
                    string leftPart = exprStr.Substring(0, opIndex).Trim();
                    string rightPart = exprStr.Substring(opIndex + 1).Trim();

                    var left = ParseExpression(leftPart, out error);
                    if (!string.IsNullOrEmpty(error))
                    {
                        return false;
                    }
                    
                    var right = ParseExpression(rightPart, out error);
                    if (!string.IsNullOrEmpty(error))
                    {
                        return false;
                    }

                    var binaryOp = op switch
                    {
                        "+" => BinaryOperator.Add,
                        "-" => BinaryOperator.Subtract,
                        "*" => BinaryOperator.Multiply,
                        "/" => BinaryOperator.Divide,
                        _ => throw new InvalidOperationException($"Unknown operator: {op}")
                    };

                    result = new BinaryExpression(left, binaryOp, right);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the index of an operator at depth 0 (not inside parens or brackets)
        /// </summary>
        private int FindOperatorIndex(string expr, string op)
        {
            int depth = 0;
            int bracketDepth = 0;
            int lastIndex = -1;

            for (int i = 0; i < expr.Length; i++)
            {
                if (expr[i] == '(' || expr[i] == '[')
                    depth++;
                else if (expr[i] == ')' || expr[i] == ']')
                    depth--;
                else if (depth == 0 && i + op.Length <= expr.Length)
                {
                    if (expr.Substring(i, op.Length) == op)
                    {
                        // For - and +, we want the rightmost occurrence at depth 0 (lowest precedence)
                        if (op == "+" || op == "-")
                        {
                            // Make sure it's not a unary operator
                            if (i > 0)
                            {
                                lastIndex = i;
                            }
                        }
                        else // For * and /, we want the leftmost occurrence at depth 0
                        {
                            return i;
                        }
                    }
                }
            }

            return lastIndex;
        }

        private Expression BuildCompositeExpression(
            Dictionary<string, Expression> coefficients,
            Expression constant)
        {
            // Build a binary expression tree from coefficients
            Expression? result = null;
            
            foreach (var kvp in coefficients)
            {
                var term = new BinaryExpression(
                    kvp.Value,
                    BinaryOperator.Multiply,
                    new VariableExpression(kvp.Key)
                );
                
                if (result == null)
                {
                    result = term;
                }
                else
                {
                    result = new BinaryExpression(result, BinaryOperator.Add, term);
                }
            }
            
            // Add constant if non-zero
            if (constant is ConstantExpression c && Math.Abs(c.Value) > 1e-10)
            {
                if (result == null)
                {
                    result = constant;
                }
                else
                {
                    result = new BinaryExpression(result, BinaryOperator.Add, constant);
                }
            }
            
            return result ?? new ConstantExpression(0);
        }
    }
}