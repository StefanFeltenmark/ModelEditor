using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Manages tokenization of complex expressions (parameters, tuples, items)
    /// </summary>
    public class TokenManager
    {
        private readonly Dictionary<string, Expression> tokenMap = new Dictionary<string, Expression>();
        private int tokenCounter = 0;
        
        /// <summary>
        /// Creates a token for an expression
        /// </summary>
        public string CreateToken(Expression expression, string prefix = "TOKEN")
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));
            
            string token = $"__{prefix}{tokenCounter++}__";
            tokenMap[token] = expression;
            return token;
        }
        
        /// <summary>
        /// Tries to get the expression for a token
        /// </summary>
        public bool TryGetExpression(string token, out Expression? expression)
        {
            return tokenMap.TryGetValue(token, out expression);
        }
        
        /// <summary>
        /// Gets the expression for a token or returns null
        /// </summary>
        public Expression? GetExpression(string token)
        {
            return tokenMap.TryGetValue(token, out var expr) ? expr : null;
        }
        
        /// <summary>
        /// Gets all tokens as a read-only dictionary
        /// </summary>
        public IReadOnlyDictionary<string, Expression> GetAllTokens() => tokenMap;
        
        /// <summary>
        /// Checks if a token exists
        /// </summary>
        public bool HasToken(string token) => tokenMap.ContainsKey(token);
        
        /// <summary>
        /// Gets the number of tokens created
        /// </summary>
        public int TokenCount => tokenMap.Count;
        
        /// <summary>
        /// Clears all tokens and resets counter
        /// </summary>
        public void Clear()
        {
            tokenMap.Clear();
            tokenCounter = 0;
        }
        
        /// <summary>
        /// Checks if a string contains any tokens
        /// </summary>
        public bool ContainsTokens(string expression)
        {
            return tokenMap.Keys.Any(token => expression.Contains(token));
        }
    }
}