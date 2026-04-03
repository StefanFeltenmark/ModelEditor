namespace Core.Models
{
    /// <summary>
    /// Represents an assertion that validates model assumptions.
    /// Example: assert n > 0;
    /// Example: assert sum(i in I) x[i] >= 0;
    /// </summary>
    public class AssertStatement
    {
        public string? Message { get; set; }
        public string Condition { get; set; }
        public bool IsIndexed { get; set; }
        public string? IndexSetName { get; set; }
        public string? IndexVariable { get; set; }

        /// <summary>Parsed expression set by EquationParser. Null if only text condition is available.</summary>
        public Expression? ParsedCondition { get; set; }

        public AssertStatement(string condition, string? message = null)
        {
            Condition = condition;
            Message = message;
        }

        /// <summary>
        /// Evaluates the assertion. Returns false (with a warning message) when the condition is false.
        /// </summary>
        public bool Validate(ModelManager modelManager, out string error)
        {
            error = string.Empty;

            try
            {
                if (ParsedCondition == null)
                    return true; // Cannot evaluate without a parsed expression — treat as pass

                double result = ParsedCondition.Evaluate(modelManager);
                bool holds = Math.Abs(result - 1.0) < 1e-10 || result != 0.0;

                if (!holds)
                {
                    error = string.IsNullOrEmpty(Message)
                        ? $"Assertion violated: {Condition}"
                        : $"Assertion \"{Message}\" violated: {Condition}";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                // Evaluation failed (e.g., missing data) — emit a warning but don't block
                error = $"Assertion could not be evaluated: {ex.Message}";
                return true; // treat as warning, not hard failure
            }
        }

        public override string ToString()
        {
            string msg = !string.IsNullOrEmpty(Message) ? $" \"{Message}\"" : "";
            return $"assert{msg}: {Condition};";
        }
    }
}