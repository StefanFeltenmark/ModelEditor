namespace Core.Models
{
    /// <summary>
    /// Represents a named decision expression (dexpr) that can be reused in the model
    /// Example: dexpr float totalCost = sum(i in I) cost[i] * x[i];
    /// </summary>
    public class DecisionExpression
    {
        public string Name { get; set; }
        public VariableType Type { get; set; }
        public Dictionary<string, Expression> Coefficients { get; set; }
        public Expression Constant { get; set; }
        public bool IsIndexed { get; set; }
        public string? IndexSetName { get; set; }

        public DecisionExpression(
            string name,
            VariableType type,
            Dictionary<string, Expression> coefficients,
            Expression constant,
            string? indexSetName = null)
        {
            Name = name;
            Type = type;
            Coefficients = coefficients;
            Constant = constant;
            IndexSetName = indexSetName;
            IsIndexed = !string.IsNullOrEmpty(indexSetName);
        }

        /// <summary>
        /// Evaluates the decision expression to get numeric value
        /// </summary>
        public double Evaluate(ModelManager modelManager, Dictionary<string, double>? variableValues = null)
        {
            double result = Constant.Evaluate(modelManager);

            if (variableValues != null)
            {
                foreach (var kvp in Coefficients)
                {
                    if (variableValues.TryGetValue(kvp.Key, out double varValue))
                    {
                        double coeff = kvp.Value.Evaluate(modelManager);
                        result += coeff * varValue;
                    }
                }
            }

            return result;
        }

        public override string ToString()
        {
            string typeStr = Type switch
            {
                VariableType.Float => "float",
                VariableType.Integer => "int",
                VariableType.Boolean => "bool",
                _ => "float"
            };

            var terms = Coefficients.OrderBy(k => k.Key)
                .Select(kvp => $"{kvp.Value}*{kvp.Key}");
            
            string expr = string.Join(" + ", terms).Replace("+ -", "- ");
            
            if (Constant is ConstantExpression constExpr && Math.Abs(constExpr.Value) > 1e-10)
            {
                expr += $" + {Constant}";
            }

            string indexStr = IsIndexed ? $"[{IndexSetName}]" : "";
            return $"dexpr {typeStr} {Name}{indexStr} = {expr};";
        }
    }
}