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