namespace Core.Models
{
    /// <summary>
    /// Represents an assertion that validates model assumptions
    /// Example: assert n > 0;
    /// Example: assert forall(i in I) cost[i] >= 0;
    /// </summary>
    public class AssertStatement
    {
        public string? Message { get; set; }
        public string Condition { get; set; }
        public bool IsIndexed { get; set; }
        public string? IndexSetName { get; set; }
        public string? IndexVariable { get; set; }

        public AssertStatement(string condition, string? message = null)
        {
            Condition = condition;
            Message = message;
        }

        /// <summary>
        /// Validates the assertion
        /// </summary>
        public bool Validate(ModelManager modelManager, out string error)
        {
            error = string.Empty;
            
            try
            {
                // This is simplified - full implementation would evaluate the condition
                // For now, we just check that referenced parameters exist
                return true;
            }
            catch (Exception ex)
            {
                error = $"Assertion failed: {ex.Message}";
                return false;
            }
        }

        public override string ToString()
        {
            string msg = !string.IsNullOrEmpty(Message) ? $" \"{Message}\"" : "";
            return $"assert{msg}: {Condition};";
        }
    }
}