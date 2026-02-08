using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// Context for substituting index variables during expression expansion
    /// Used when expanding summations and indexed equations
    /// </summary>
    public class IndexSubstitutionContext
    {
        private Dictionary<string, int> indexValues;

        public IndexSubstitutionContext()
        {
            indexValues = new Dictionary<string, int>();
        }

        public IndexSubstitutionContext(Dictionary<string, int> values)
        {
            indexValues = new Dictionary<string, int>(values);
        }

        /// <summary>
        /// Tries to get the integer value for an index variable
        /// </summary>
        public bool TryGetIndex(string name, out int value)
        {
            return indexValues.TryGetValue(name, out value);
        }

        /// <summary>
        /// Sets an index variable value
        /// </summary>
        public void SetIndex(string name, int value)
        {
            indexValues[name] = value;
        }

        /// <summary>
        /// Checks if an index variable is defined
        /// </summary>
        public bool HasIndex(string name)
        {
            return indexValues.ContainsKey(name);
        }

        /// <summary>
        /// Gets all index bindings
        /// </summary>
        public Dictionary<string, int> GetAllIndices()
        {
            return new Dictionary<string, int>(indexValues);
        }

        /// <summary>
        /// Removes an index variable
        /// </summary>
        public void RemoveIndex(string name)
        {
            indexValues.Remove(name);
        }

        /// <summary>
        /// Clears all index bindings
        /// </summary>
        public void Clear()
        {
            indexValues.Clear();
        }

        /// <summary>
        /// Creates a copy of this context
        /// </summary>
        public IndexSubstitutionContext Clone()
        {
            return new IndexSubstitutionContext(indexValues);
        }
    }
}