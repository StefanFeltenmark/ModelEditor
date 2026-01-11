using Core.Models;

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
            try
            {
                expression = expression.Trim();

                if (int.TryParse(expression, out int directValue))
                {
                    return EvaluationResult<int>.Success(directValue);
                }

                var param = modelManager.GetParameter(expression);
                if (param != null)
                {
                    if (param.Type == ParameterType.Integer)
                    {
                        return EvaluationResult<int>.Success(param.GetIntValue());
                    }
                    return EvaluationResult<int>.Failure($"Parameter '{expression}' is not an integer");
                }

                return EvaluationResult<int>.Failure($"Cannot evaluate '{expression}' as integer");
            }
            catch (Exception ex)
            {
                return EvaluationResult<int>.Failure(ex.Message);
            }
        }

        public EvaluationResult<double> EvaluateFloatExpression(string expression)
        {
            try
            {
                expression = expression.Trim();

                if (double.TryParse(expression, System.Globalization.NumberStyles.Float, 
                    System.Globalization.CultureInfo.InvariantCulture, out double directValue))
                {
                    return EvaluationResult<double>.Success(directValue);
                }

                var param = modelManager.GetParameter(expression);
                if (param != null)
                {
                    if (param.Type == ParameterType.Float)
                    {
                        return EvaluationResult<double>.Success(param.GetFloatValue());
                    }
                    else if (param.Type == ParameterType.Integer)
                    {
                        return EvaluationResult<double>.Success(param.GetIntValue());
                    }
                    return EvaluationResult<double>.Failure($"Parameter '{expression}' is not numeric");
                }

                return EvaluationResult<double>.Failure($"Cannot evaluate '{expression}' as float");
            }
            catch (Exception ex)
            {
                return EvaluationResult<double>.Failure(ex.Message);
            }
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