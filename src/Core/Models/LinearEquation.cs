namespace Core.Models
{
    /// <summary>
    /// Represents a linear equation or inequality with symbolic coefficients
    /// Format: a1*v1 + a2*v2 + ... + an*vn {operator} c
    /// where a1, a2, ..., an, c can be expressions
    /// </summary>
    public class LinearEquation
    {
        /// <summary>
        /// Dictionary of variable names to their coefficient expressions
        /// </summary>
        public Dictionary<string, Expression> Coefficients { get; set; }

        /// <summary>
        /// The constant expression on the right side of the equation/inequality
        /// </summary>
        public Expression Constant { get; set; }

        /// <summary>
        /// The relational operator (=, <, >, <=, >=)
        /// </summary>
        public RelationalOperator Operator { get; set; }

        /// <summary>
        /// Optional label for the equation
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Base name for indexed equations (e.g., "constraint" in constraint[1])
        /// </summary>
        public string? BaseName { get; set; }

        /// <summary>
        /// Optional index for the equation
        /// </summary>
        public int? Index { get; set; }

        /// <summary>
        /// Optional second index for two-dimensional constraints
        /// </summary>
        public int? SecondIndex { get; set; }

        public LinearEquation()
        {
            Coefficients = new Dictionary<string, Expression>();
            Constant = new ConstantExpression(0);
            Operator = RelationalOperator.Equal;
        }

        public LinearEquation(
            Dictionary<string, Expression> coefficients,
            Expression constant,
            RelationalOperator op,
            string? label = null)
        {
            Coefficients = coefficients;
            Constant = constant;
            Operator = op;
            Label = label;
        }

        /// <summary>
        /// Checks if this is an inequality (not an equality)
        /// </summary>
        public bool IsInequality()
        {
            return Operator != RelationalOperator.Equal;
        }

        /// <summary>
        /// Gets the operator symbol as a string
        /// </summary>
        public string GetOperatorSymbol() => Operator switch
        {
            RelationalOperator.Equal => "==",
            RelationalOperator.LessThan => "<",
            RelationalOperator.GreaterThan => ">",
            RelationalOperator.LessThanOrEqual => "<=",
            RelationalOperator.GreaterThanOrEqual => ">=",
            _ => "?"
        };

        /// <summary>
        /// Gets all variable names in this equation
        /// </summary>
        public IEnumerable<string> GetVariables()
        {
            return Coefficients.Keys.OrderBy(k => k);
        }

        /// <summary>
        /// Gets the coefficient expression for a specific variable
        /// Returns a zero constant expression if the variable is not present
        /// </summary>
        public Expression GetCoefficientExpression(string variableName)
        {
            return Coefficients.TryGetValue(variableName, out var expr) 
                ? expr 
                : new ConstantExpression(0);
        }

        /// <summary>
        /// Gets the evaluated numeric coefficient for a specific variable
        /// Returns 0 if the variable is not present
        /// </summary>
        public double GetCoefficient(string variableName)
        {
            if (!Coefficients.TryGetValue(variableName, out var expr))
                return 0.0;
            
            // For now, if it's a constant expression, return the value
            if (expr is ConstantExpression constExpr)
                return constExpr.Value;
            
            // For non-constant expressions, we need a ModelManager to evaluate
            throw new InvalidOperationException(
                $"Cannot get numeric coefficient for variable '{variableName}' - coefficient is a non-constant expression. Use GetCoefficientExpression() or Evaluate() instead.");
        }

        /// <summary>
        /// Tries to get the numeric coefficient if it's a constant
        /// </summary>
        public bool TryGetConstantCoefficient(string variableName, out double value)
        {
            value = 0.0;
            
            if (!Coefficients.TryGetValue(variableName, out var expr))
                return true; // Variable not present = coefficient is 0
            
            if (expr is ConstantExpression constExpr)
            {
                value = constExpr.Value;
                return true;
            }
            
            return false; // Non-constant expression
        }

        /// <summary>
        /// Evaluates all expressions to get numeric coefficients and constant
        /// </summary>
        public (Dictionary<string, double> coefficients, double constant) Evaluate(ModelManager modelManager)
        {
            var numericCoefficients = new Dictionary<string, double>();
            
            foreach (var kvp in Coefficients)
            {
                numericCoefficients[kvp.Key] = kvp.Value.Evaluate(modelManager);
            }
            
            double numericConstant = Constant.Evaluate(modelManager);
            
            return (numericCoefficients, numericConstant);
        }

        /// <summary>
        /// Gets the number of variables in this equation
        /// </summary>
        public int VariableCount => Coefficients.Count;

        /// <summary>
        /// Checks if a specific variable appears in this equation
        /// </summary>
        public bool ContainsVariable(string variableName)
        {
            return Coefficients.ContainsKey(variableName);
        }

        /// <summary>
        /// Gets a human-readable description of the equation
        /// </summary>
        public string GetDescription()
        {
            string labelPart = !string.IsNullOrEmpty(Label) ? $"{Label}: " : "";
            string indexPart = "";
            
            if (Index.HasValue)
            {
                if (SecondIndex.HasValue)
                {
                    indexPart = $"[{Index},{SecondIndex}]";
                }
                else
                {
                    indexPart = $"[{Index}]";
                }
            }
            
            string basePart = !string.IsNullOrEmpty(BaseName) ? $"{BaseName}{indexPart}" : "";
            
            return $"{labelPart}{basePart}".TrimEnd();
        }

        public override string ToString()
        {
            if (Coefficients.Count == 0)
                return $"0 {GetOperatorSymbol()} {Constant}";

            var terms = new List<string>();
            foreach (var kvp in Coefficients.OrderBy(k => k.Key))
            {
                string coeffStr = kvp.Value.ToString();
                string var = kvp.Key;
                
                // Handle simple constant coefficients nicely
                if (kvp.Value is ConstantExpression constExpr)
                {
                    if (Math.Abs(constExpr.Value - 1.0) < 1e-10)
                        terms.Add(var);
                    else if (Math.Abs(constExpr.Value - (-1.0)) < 1e-10)
                        terms.Add($"-{var}");
                    else
                        terms.Add($"{constExpr.Value}*{var}");
                }
                else
                {
                    // For complex expressions, show in parentheses
                    terms.Add($"({coeffStr})*{var}");
                }
            }

            string lhs = string.Join(" + ", terms).Replace("+ -", "- ");
            string label = !string.IsNullOrEmpty(Label) ? $"{Label}: " : "";
            
            return $"{label}{lhs} {GetOperatorSymbol()} {Constant}";
        }
    }

    public enum RelationalOperator
    {
        Equal,
        LessThan,
        GreaterThan,
        LessThanOrEqual,
        GreaterThanOrEqual
    }
}