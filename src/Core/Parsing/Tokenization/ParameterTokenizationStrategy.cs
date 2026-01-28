using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing.Tokenization
{
    /// <summary>
    /// Tokenizes scalar parameter references (e.g., "a", "multiplier")
    /// </summary>
    public class ParameterTokenizationStrategy : ITokenizationStrategy
    {
        private readonly ModelManager modelManager;
        
        public ParameterTokenizationStrategy()
        {
            
        }
        
    

        public string Tokenize(string expression, TokenManager tokenManager, ModelManager modelManager)
        {
            // Pattern: word boundaries around identifiers
            // Match identifiers that are parameters (not followed by [ or ( )
            string pattern = @"\b([a-zA-Z][a-zA-Z0-9_]*)\b(?!\s*[\[\(])";
            
            return Regex.Replace(expression, pattern, match =>
            {
                string identifier = match.Groups[1].Value;
                
                // Check if it's a parameter
                if (modelManager.Parameters.TryGetValue(identifier, out var param))
                {
                    // Only tokenize scalar parameters
                    if (param.IsScalar)
                    {
                        var paramExpr = new ParameterExpression(identifier);
                        string token = tokenManager.CreateToken(paramExpr);
                        return token;
                    }
                }
                
                // Not a scalar parameter, return as-is
                return match.Value;
            });
        }

        public int Priority { get; }
        public string Name { get; }
    }
}