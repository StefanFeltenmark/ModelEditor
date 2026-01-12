namespace Core.Models
{
    /// <summary>
    /// Represents a linear equation or inequality with any number of variables
    /// Format: a1*v1 + a2*v2 + ... + an*vn {operator} c
    /// </summary>
    public class LinearEquation
    {
        /// <summary>
        /// Dictionary of variable names to their coefficients
        /// </summary>
        public Dictionary<string, double> Coefficients { get; set; }

        /// <summary>
        /// The constant on the right side of the equation/inequality
        /// </summary>
        public double Constant { get; set; }

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
        /// Optional index for the equation (e.g., constraint[1], constraint[2])
        /// </summary>
        public int? Index { get; set; }

        /// <summary>
        /// Optional second index for two-dimensional constraints
        /// </summary>
        public int? SecondIndex { get; set; }

        public LinearEquation()
        {
            Coefficients = new Dictionary<string, double>();
            Constant = 0;
            Operator = RelationalOperator.Equal;
        }

        public LinearEquation(Dictionary<string, double> coefficients, double constant, RelationalOperator op = RelationalOperator.Equal, string? label = null)
        {
            Coefficients = coefficients ?? new Dictionary<string, double>();
            Constant = constant;
            Operator = op;
            Label = label;
        }

        /// <summary>
        /// Checks if the equation is two-dimensional (i.e., has a second index)
        /// </summary>
        public bool IsTwoDimensional => SecondIndex.HasValue;

        /// <summary>
        /// Gets all variable names in sorted order
        /// </summary>
        public IEnumerable<string> GetVariables()
        {
            return Coefficients.Keys.OrderBy(k => k);
        }

        /// <summary>
        /// Gets the coefficient for a specific variable (returns 0 if not found)
        /// </summary>
        public double GetCoefficient(string variable)
        {
            return Coefficients.TryGetValue(variable, out double value) ? value : 0;
        }

        /// <summary>
        /// Returns true if this is an inequality (not an equality)
        /// </summary>
        public bool IsInequality()
        {
            return Operator != RelationalOperator.Equal;
        }

        /// <summary>
        /// Gets the operator symbol as a string
        /// </summary>
        public string GetOperatorSymbol()
        {
            return Operator switch
            {
                RelationalOperator.Equal => "=",
                RelationalOperator.LessThan => "<",
                RelationalOperator.GreaterThan => ">",
                RelationalOperator.LessThanOrEqual => "≤",
                RelationalOperator.GreaterThanOrEqual => "≥",
                _ => "="
            };
        }

        /// <summary>
        /// Gets the full identifier including index if present
        /// </summary>
        public string GetFullIdentifier()
        {
            if (!string.IsNullOrEmpty(Label))
            {
                if (Index.HasValue)
                {
                    return $"{Label}[{Index.Value}]";
                }
                return Label;
            }
            
            if (!string.IsNullOrEmpty(BaseName) && Index.HasValue)
            {
                return $"{BaseName}[{Index.Value}]";
            }

            return "unlabeled";
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            
            if (!string.IsNullOrEmpty(Label))
            {
                sb.Append($"{Label}: ");
            }
            else if (!string.IsNullOrEmpty(BaseName))
            {
                if (IsTwoDimensional)
                    sb.Append($"{BaseName}[{Index},{SecondIndex}]: ");
                else if (Index.HasValue)
                    sb.Append($"{BaseName}[{Index}]: ");
            }

            bool first = true;
            foreach (var kvp in Coefficients.OrderBy(k => k.Key))
            {
                double coeff = kvp.Value;
                string variable = kvp.Key;

                if (!first && coeff >= 0)
                    sb.Append(" + ");
                else if (coeff < 0)
                    sb.Append(" - ");

                if (Math.Abs(coeff) != 1 || first)
                {
                    sb.Append($"{Math.Abs(coeff):G}");
                }

                sb.Append(variable);
                first = false;
            }

            sb.Append($" {OperatorToString(Operator)} {Constant:G}");

            return sb.ToString();
        }

        private string OperatorToString(RelationalOperator op)
        {
            return op switch
            {
                RelationalOperator.Equal => "==",
                RelationalOperator.LessThanOrEqual => "≤",
                RelationalOperator.GreaterThanOrEqual => "≥",
                RelationalOperator.LessThan => "<",
                RelationalOperator.GreaterThan => ">",
                _ => "=="
            };
        }
    }

    /// <summary>
    /// Represents the type of relational operator in an equation or inequality
    /// </summary>
    public enum RelationalOperator
    {
        Equal,
        LessThan,
        GreaterThan,
        LessThanOrEqual,
        GreaterThanOrEqual
    }
}