using Core.Models;
using System.Data;
using System.Globalization;

namespace Core
{
    /// <summary>
    /// Evaluates expressions and parameter references
    /// </summary>
    public class ExpressionEvaluator
    {
        private readonly ModelManager modelManager;

        public ExpressionEvaluator(ModelManager manager)
        {
            modelManager = manager;
        }

        public EvaluationResult<int> EvaluateIntExpression(string expression)
        {
            expression = expression.Trim();
            
            // Try direct parse first
            if (int.TryParse(expression, out int directValue))
            {
                return new EvaluationResult<int> { IsSuccess = true, Value = directValue };
            }
            
            // Try to evaluate as arithmetic expression
            try
            {
                var dataTable = new System.Data.DataTable();
                var result = dataTable.Compute(expression, "");
                
                if (result != null && int.TryParse(result.ToString(), out int computedValue))
                {
                    return new EvaluationResult<int> { IsSuccess = true, Value = computedValue };
                }
            }
            catch
            {
                // Fall through to parameter substitution
            }
            
            // Check if it's a parameter reference
            if (modelManager.Parameters.TryGetValue(expression, out var param))
            {
                if (param.Type == ParameterType.Integer)
                {
                    return new EvaluationResult<int> 
                    { 
                        IsSuccess = true, 
                        Value = Convert.ToInt32(param.Value) 
                    };
                }
            }
            
            return new EvaluationResult<int> 
            { 
                IsSuccess = false, 
                ErrorMessage = $"Cannot evaluate int expression: {expression}" 
            };
        }

        public EvaluationResult<double> EvaluateFloatExpression(string expression)
        {
            expression = expression.Trim();
            
            // Try direct parse first
            if (double.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, out double directValue))
            {
                return new EvaluationResult<double> { IsSuccess = true, Value = directValue };
            }
            
            // Try to evaluate as arithmetic expression
            try
            {
                // Use NCalc or System.Data.DataTable.Compute for expression evaluation
                var dataTable = new DataTable();
                var result = dataTable.Compute(expression, "");
                
                if (result != null && double.TryParse(result.ToString(), out double computedValue))
                {
                    return new EvaluationResult<double> { IsSuccess = true, Value = computedValue };
                }
            }
            catch
            {
                // Fall through to parameter substitution
            }
            
            // Check if it's a parameter reference
            if (modelManager.Parameters.TryGetValue(expression, out var param))
            {
                if (param.Type == ParameterType.Float || param.Type == ParameterType.Integer)
                {
                    return new EvaluationResult<double> 
                    { 
                        IsSuccess = true, 
                        Value = Convert.ToDouble(param.Value) 
                    };
                }
            }
            
            return new EvaluationResult<double> 
            { 
                IsSuccess = false, 
                ErrorMessage = $"Cannot evaluate float expression: {expression}" 
            };
        }

        /// <summary>
        /// Evaluates a boolean expression with comparison operators
        /// Examples: "5 < 10", "x == y", "a != b", "10 >= 5"
        /// </summary>
        public bool EvaluateBooleanExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            expression = expression.Trim();

            // Check for comparison operators in order of precedence (longest first to avoid conflicts)
            if (expression.Contains("=="))
            {
                var parts = expression.Split(new[] { "==" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var left = EvaluateFloatExpression(parts[0].Trim());
                    var right = EvaluateFloatExpression(parts[1].Trim());
                    
                    if (left.IsSuccess && right.IsSuccess)
                    {
                        return Math.Abs(left.Value - right.Value) < 1e-10;
                    }
                }
            }
            else if (expression.Contains("!="))
            {
                var parts = expression.Split(new[] { "!=" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var left = EvaluateFloatExpression(parts[0].Trim());
                    var right = EvaluateFloatExpression(parts[1].Trim());
                    
                    if (left.IsSuccess && right.IsSuccess)
                    {
                        return Math.Abs(left.Value - right.Value) >= 1e-10;
                    }
                }
            }
            else if (expression.Contains("<="))
            {
                var parts = expression.Split(new[] { "<=" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var left = EvaluateFloatExpression(parts[0].Trim());
                    var right = EvaluateFloatExpression(parts[1].Trim());
                    
                    if (left.IsSuccess && right.IsSuccess)
                    {
                        return left.Value <= right.Value + 1e-10; // Add epsilon for floating point comparison
                    }
                }
            }
            else if (expression.Contains(">="))
            {
                var parts = expression.Split(new[] { ">=" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var left = EvaluateFloatExpression(parts[0].Trim());
                    var right = EvaluateFloatExpression(parts[1].Trim());
                    
                    if (left.IsSuccess && right.IsSuccess)
                    {
                        return left.Value >= right.Value - 1e-10; // Subtract epsilon for floating point comparison
                    }
                }
            }
            else if (expression.Contains("<"))
            {
                var parts = expression.Split('<');
                if (parts.Length == 2)
                {
                    var left = EvaluateFloatExpression(parts[0].Trim());
                    var right = EvaluateFloatExpression(parts[1].Trim());
                    
                    if (left.IsSuccess && right.IsSuccess)
                    {
                        return left.Value < right.Value;
                    }
                }
            }
            else if (expression.Contains(">"))
            {
                var parts = expression.Split('>');
                if (parts.Length == 2)
                {
                    var left = EvaluateFloatExpression(parts[0].Trim());
                    var right = EvaluateFloatExpression(parts[1].Trim());
                    
                    if (left.IsSuccess && right.IsSuccess)
                    {
                        return left.Value > right.Value;
                    }
                }
            }
            else
            {
                // No comparison operator - try to evaluate as a numeric expression
                // Non-zero is true, zero is false
                var result = EvaluateFloatExpression(expression);
                if (result.IsSuccess)
                {
                    return Math.Abs(result.Value) >= 1e-10;
                }
            }

            return false;
        }
    }

    public class EvaluationResult<T>
    {
        public bool IsSuccess { get; set; }
        public T Value { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public static EvaluationResult<T> Success(T value) 
            => new EvaluationResult<T> { IsSuccess = true, Value = value };
        public static EvaluationResult<T> Failure(string errorMessage) 
            => new EvaluationResult<T> { IsSuccess = false, ErrorMessage = errorMessage };
    }
}