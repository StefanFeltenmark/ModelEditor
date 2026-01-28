using Core;
using Core.Parsing;
using Core.Parsing.Tokenization;

/// <summary>
/// Orchestrates all tokenization strategies
/// </summary>
public class TokenizationOrchestrator
{
    private readonly List<ITokenizationStrategy> strategies;
        
    public TokenizationOrchestrator()
    {
        strategies = new List<ITokenizationStrategy>
        {
            new ItemExpressionTokenizer(),
            new TupleFieldAccessTokenizer(),
            new TwoDimensionalIndexTokenizer(),
            new SingleDimensionalIndexTokenizer(),
            new ParameterTokenizationStrategy()
        };
    }
        
    /// <summary>
    /// Applies all tokenization strategies in priority order
    /// </summary>
    public string TokenizeExpression(string expression, TokenManager tokenManager, ModelManager modelManager)
    {
        string result = expression;
            
        foreach (var strategy in strategies.OrderBy(s => s.Priority))
        {
            try
            {
                result = strategy.Tokenize(result, tokenManager, modelManager);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in {strategy.Name} tokenization: {ex.Message}", ex);
            }
        }
            
        return result;
    }
        
    /// <summary>
    /// Allows adding custom tokenization strategies
    /// </summary>
    public void AddStrategy(ITokenizationStrategy strategy)
    {
        strategies.Add(strategy);
    }
}