namespace Core.Models
{
    /// <summary>
    /// Parameter that stores tuple instances (not primitive values)
    /// Example: ScenarioTreeNode root = item(nodes, 0);
    /// </summary>
    public class TupleParameter
    {
        public string Name { get; set; }
        public string TupleTypeName { get; set; }  // Schema name
        public bool IsIndexed { get; set; }
        public List<string> IndexSetNames { get; set; }
        public bool IsExternal { get; set; }

        // Storage
        private TupleInstance? scalarValue;  // For scalar tuple parameters
        private Dictionary<string, TupleInstance>? indexedValues;  // For indexed

        public TupleParameter(string name, string tupleTypeName)
        {
            Name = name;
            TupleTypeName = tupleTypeName;
            IsIndexed = false;
            IndexSetNames = new List<string>();
        }

        public TupleParameter(string name, string tupleTypeName, List<string> indexSetNames, bool isExternal)
        {
            Name = name;
            TupleTypeName = tupleTypeName;
            IsIndexed = true;
            IndexSetNames = indexSetNames;
            IsExternal = isExternal;
            indexedValues = new Dictionary<string, TupleInstance>();
        }

        public void SetValue(TupleInstance value)
        {
            if (IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is indexed");
            scalarValue = value;
        }

        public TupleInstance? GetValue()
        {
            if (IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is indexed");
            return scalarValue;
        }

        public void SetIndexedValue(TupleInstance value, params int[] indices)
        {
            if (!IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is not indexed");

            indexedValues ??= new Dictionary<string, TupleInstance>();
            string key = string.Join("_", indices);
            indexedValues[key] = value;
        }

        public TupleInstance? GetIndexedValue(params int[] indices)
        {
            if (!IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is not indexed");

            string key = string.Join("_", indices);
            return indexedValues?.TryGetValue(key, out var value) == true ? value : null;
        }
    }
}