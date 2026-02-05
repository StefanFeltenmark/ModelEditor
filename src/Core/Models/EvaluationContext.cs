namespace Core.Models
{
    /// <summary>
    /// Tracks iterator variable values during expression evaluation
    /// </summary>
    public class EvaluationContext
    {
        private Dictionary<string, int> iteratorValues = new Dictionary<string, int>();

        public void SetIterator(string name, int value)
        {
            iteratorValues[name] = value;
        }

        public int GetIterator(string name)
        {
            if (!iteratorValues.TryGetValue(name, out int value))
            {
                throw new InvalidOperationException($"Iterator variable '{name}' not found in context");
            }
            return value;
        }

        public bool HasIterator(string name)
        {
            return iteratorValues.ContainsKey(name);
        }

        public EvaluationContext Clone()
        {
            var clone = new EvaluationContext();
            foreach (var kvp in iteratorValues)
            {
                clone.SetIterator(kvp.Key, kvp.Value);
            }
            return clone;
        }
    }
}