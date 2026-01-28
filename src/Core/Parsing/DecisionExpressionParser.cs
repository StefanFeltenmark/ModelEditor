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
        /// Examples:
        ///   dexpr float totalCost = sum(i in Products) cost[i] * x[i];
        ///   dexpr int used[i in Machines] = sum(j in Jobs) assign[i][j];
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
                return ParseIndexedDexpr(indexedMatch, out dexpr, out error);
            }
            
            // Pattern for scalar dexpr: dexpr type name = expression
            string scalarPattern = @"^dexpr\s+(int|float|bool|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
            var scalarMatch = Regex.Match(statement, scalarPattern, RegexOptions.IgnoreCase);
            
            if (scalarMatch.Success)
            {
                return ParseScalarDexpr(scalarMatch, out dexpr, out error);
            }
            
            error = "Invalid decision expression syntax. Expected: dexpr type name = expression";
            return false;
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
            
            // Parse expression (simplified - would need full expression parser)
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
            if (!modelManager.IndexSets.ContainsKey(indexSetName))
            {
                error = $"Index set '{indexSetName}' not found";
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
        
        // Update ParseExpression to use the EquationParser's ParseExpression

        private Expression ParseExpression(string exprStr, out string error)
        {
            error = string.Empty;
            
            // Use a helper from EquationParser or create an expression builder
            // For now, simplified version:
            
            try
            {
                // Use the summation expander and expression parser
                var summationExpander = new SummationExpander(modelManager);
                exprStr = summationExpander.ExpandSummations(exprStr, out error);
                
                if (!string.IsNullOrEmpty(error))
                    return new ConstantExpression(0);
                
                // Now parse the expression
                // This is simplified - you'd want to reuse the full expression parsing logic
                var expressionParser = new ExpressionParser(modelManager);
                
                if (expressionParser.TryParseExpression(exprStr, out var coeffs, out var constant, out error))
                {
                    // If it's a pure constant
                    if (coeffs.Count == 0)
                    {
                        return constant;
                    }
                    
                    // If it's a single variable
                    if (coeffs.Count == 1 && constant is ConstantExpression c && Math.Abs(c.Value) < 1e-10)
                    {
                        var kvp = coeffs.First();
                        if (kvp.Value is ConstantExpression coef && Math.Abs(coef.Value - 1.0) < 1e-10)
                        {
                            return new VariableExpression(kvp.Key);
                        }
                    }
                    
                    // For complex expressions, build a composite expression
                    // This is a simplified placeholder
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