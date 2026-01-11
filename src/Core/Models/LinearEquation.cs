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
        /// Optional index for the equation (e.g., constraint[1], constraint[2])
        /// </summary>
        public int? Index { get; set; }

        /// <summary>
        /// Base name for indexed equations (e.g., "constraint" in constraint[1])
        /// </summary>
        public string? BaseName { get; set; }

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
            var result = new System.Text.StringBuilder();
            
            // Add label or indexed identifier if present
            string identifier = GetFullIdentifier();
            if (identifier != "unlabeled")
            {
                result.Append($"[{identifier}] ");
            }

            if (Coefficients.Count == 0)
            {
                result.Append($"0 {GetOperatorSymbol()} {Constant}");
            }
            else
            {
                var terms = new List<string>();
                foreach (var variable in GetVariables())
                {
                    double coeff = Coefficients[variable];
                    if (coeff == 0)
                        continue;

                    string term;
                    if (coeff == 1)
                        term = variable;
                    else if (coeff == -1)
                        term = $"-{variable}";
                    else
                        term = $"{coeff}*{variable}";

                    if (terms.Count > 0 && coeff > 0)
                        term = $"+ {term}";
                    else if (terms.Count > 0)
                        term = $"- {(Math.Abs(coeff) == 1 ? variable : $"{Math.Abs(coeff)}*{variable}")}";

                    terms.Add(term);
                }

                result.Append($"{string.Join(" ", terms)} {GetOperatorSymbol()} {Constant}");
            }

            return result.ToString();
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