namespace Core.Models
{
    public enum ParameterType
    {
        Integer,
        Float,
        String,
        Boolean
    }

    public class Parameter
    {
        public string Name { get; set; }
        public ParameterType Type { get; set; }
        public object? Value { get; set; }
        
        // For indexed parameters
        public bool IsIndexed { get; set; }
        public string? IndexSetName { get; set; }
        public string? SecondIndexSetName { get; set; }  // ADD THIS - for 2D parameters
        public bool IsExternal { get; set; }
        
        private Dictionary<int, object>? indexedValues;
        private Dictionary<string, object>? twoDimensionalValues;  // ADD THIS - for 2D storage

        // ADD THESE COMPUTED PROPERTIES:
        
        /// <summary>
        /// Returns true if this is a scalar (non-indexed) parameter
        /// </summary>
        public bool IsScalar => !IsIndexed;

        /// <summary>
        /// Returns true if this parameter is indexed over one dimension
        /// </summary>
        public bool IsOneDimensional => IsIndexed && string.IsNullOrEmpty(SecondIndexSetName);

        /// <summary>
        /// Returns true if this parameter is indexed over two dimensions
        /// </summary>
        public bool IsTwoDimensional => IsIndexed && !string.IsNullOrEmpty(SecondIndexSetName);

        /// <summary>
        /// Returns true if the parameter has a value assigned
        /// </summary>
        public bool HasValue
        {
            get
            {
                if (IsScalar)
                {
                    return Value != null;
                }
                else if (IsTwoDimensional)
                {
                    return twoDimensionalValues != null && twoDimensionalValues.Count > 0;
                }
                else // OneDimensional
                {
                    return indexedValues != null && indexedValues.Count > 0;
                }
            }
        }

        // Regular parameter constructor
        public Parameter(string name, ParameterType type, object value)
        {
            Name = name;
            Type = type;
            Value = value;
            IsIndexed = false;
        }

        // Indexed parameter constructor (1D)
        public Parameter(string name, ParameterType type, string indexSetName, bool isExternal)
        {
            Name = name;
            Type = type;
            IsIndexed = true;
            IndexSetName = indexSetName;
            IsExternal = isExternal;
            indexedValues = new Dictionary<int, object>();
        }

        // ADD THIS - Two-dimensional parameter constructor
        public Parameter(string name, ParameterType type, string indexSetName, string secondIndexSetName, bool isExternal)
        {
            Name = name;
            Type = type;
            IsIndexed = true;
            IndexSetName = indexSetName;
            SecondIndexSetName = secondIndexSetName;
            IsExternal = isExternal;
            twoDimensionalValues = new Dictionary<string, object>();
        }

        // 1D indexed parameter methods
        public void SetIndexedValue(int index, object value)
        {
            if (!IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is not indexed");

            if (IsTwoDimensional)
                throw new InvalidOperationException($"Parameter '{Name}' is two-dimensional, use SetIndexedValue(int, int, object)");

            indexedValues ??= new Dictionary<int, object>();
            indexedValues[index] = value;
        }

        public object? GetIndexedValue(int index)
        {
            if (!IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is not indexed");

            if (IsTwoDimensional)
                throw new InvalidOperationException($"Parameter '{Name}' is two-dimensional, use GetIndexedValue(int, int)");

            return indexedValues?.TryGetValue(index, out var value) == true ? value : null;
        }

        // ADD THESE - 2D indexed parameter methods
        public void SetIndexedValue(int index1, int index2, object value)
        {
            if (!IsTwoDimensional)
                throw new InvalidOperationException($"Parameter '{Name}' is not two-dimensional");

            twoDimensionalValues ??= new Dictionary<string, object>();
            string key = BuildKey(index1, index2);
            twoDimensionalValues[key] = value;
        }

        public object? GetIndexedValue(int index1, int index2)
        {
            if (!IsTwoDimensional)
                throw new InvalidOperationException($"Parameter '{Name}' is not two-dimensional");

            string key = BuildKey(index1, index2);
            return twoDimensionalValues?.TryGetValue(key, out var value) == true ? value : null;
        }

        private string BuildKey(int index1, int index2)
        {
            return $"{index1}_{index2}";
        }

        // ADD THIS - Get all indices that have values
        public IEnumerable<int> GetDefinedIndices()
        {
            if (IsScalar)
                return Enumerable.Empty<int>();

            if (IsTwoDimensional)
                throw new InvalidOperationException($"Parameter '{Name}' is two-dimensional, use GetDefinedIndexPairs()");

            return indexedValues?.Keys ?? Enumerable.Empty<int>();
        }

        // ADD THIS - Get all index pairs that have values (for 2D parameters)
        public IEnumerable<(int, int)> GetDefinedIndexPairs()
        {
            if (!IsTwoDimensional)
                throw new InvalidOperationException($"Parameter '{Name}' is not two-dimensional");

            if (twoDimensionalValues == null)
                return Enumerable.Empty<(int, int)>();

            return twoDimensionalValues.Keys.Select(key =>
            {
                var parts = key.Split('_');
                return (int.Parse(parts[0]), int.Parse(parts[1]));
            });
        }

        public override string ToString()
        {
            if (IsScalar)
            {
                return $"{Type} {Name} = {Value}";
            }
            else if (IsTwoDimensional)
            {
                return $"{Type} {Name}[{IndexSetName}][{SecondIndexSetName}] = {(IsExternal ? "..." : "computed")}";
            }
            else
            {
                return $"{Type} {Name}[{IndexSetName}] = {(IsExternal ? "..." : "computed")}";
            }
        }
    }
}