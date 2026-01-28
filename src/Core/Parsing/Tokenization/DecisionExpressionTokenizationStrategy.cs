using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing.Tokenization
{
    /// <summary>
    /// Tokenizes decision expression references (e.g., "totalCost", "profit[i]")
    /// </summary>
    public class DecisionExpressionTokenizationStrategy : ITokenizationStrategy
    {
        private readonly ModelManager modelManager;
        
        public DecisionExpressionTokenizationStrategy()
        {
            
        }
        
        public string Tokenize(string expression, TokenManager tokenManager, ModelManager modelManager)
        {
            // Pattern 1: Indexed dexpr - dexprName[index]
            string indexedPattern = @"\b([a-zA-Z][a-zA-Z0-9_]*)\[([^\]]+)\]";
            
            expression = Regex.Replace(expression, indexedPattern, match =>
            {
                string dexprName = match.Groups[1].Value;
                string indexExpr = match.Groups[2].Value;
                
                // Check if it's a decision expression
                if (modelManager.DecisionExpressions.TryGetValue(dexprName, out var dexpr))
                {
                    if (dexpr.IsIndexed)
                    {
                        // Try to parse the index
                        if (int.TryParse(indexExpr, out int index))
                        {
                            var dexprExpr = new DecisionExpressionExpression(dexprName, index);
                            return tokenManager.CreateToken(dexprExpr, "DEXPR");
                        }
                        else
                        {
                            // Index is an expression (like 'i'), handle later
                            // For now, don't tokenize - let the parser handle it
                            return match.Value;
                        }
                    }
                }
                
                return match.Value;
            });
            
            // Pattern 2: Scalar dexpr - just the name
            string scalarPattern = @"\b([a-zA-Z][a-zA-Z0-9_]*)\b(?!\s*[\[\(])";
            
            expression = Regex.Replace(expression, scalarPattern, match =>
            {
                string dexprName = match.Groups[1].Value;
                
                // Check if it's a scalar decision expression
                if (modelManager.DecisionExpressions.TryGetValue(dexprName, out var dexpr))
                {
                    if (!dexpr.IsIndexed)
                    {
                        var dexprExpr = new DecisionExpressionExpression(dexprName);
                        return tokenManager.CreateToken(dexprExpr, "DEXPR");
                    }
                }
                
                return match.Value;
            });
            
            return expression;
        }

        public int Priority { get; }
        public string Name { get; }
    }
}