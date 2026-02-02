namespace Core.Models
{
    public enum ParameterType
    {
        Integer,
        Float,
        String,
        Boolean
    }

    /// <summary>
    /// Represents a parameter in the optimization model
    /// Supports scalar, 1D, 2D, and N-dimensional parameters
    /// </summary>
    public class Parameter
    {
        public string Name { get; set; }
        public ParameterType Type { get; set; }
        public object? Value { get; set; }  // For scalar parameters
        
        // Indexing information
        public bool IsIndexed { get; set; }
        public List<string> IndexSetNames { get; set; }  // List of index set names for multi-dimensional
        public bool IsExternal { get; set; }
        
        // Storage for indexed values
        private Dictionary<string, object>? indexedValues;  // Generic storage for any dimensionality

        // Computed properties
        public bool IsScalar => !IsIndexed;
        public bool IsOneDimensional => IsIndexed && IndexSetNames.Count == 1;
        public bool IsTwoDimensional => IsIndexed && IndexSetNames.Count == 2;
        public int Dimensionality => IsIndexed ? IndexSetNames.Count : 0;

        // Legacy accessors for backward compatibility
        public string? IndexSetName => IndexSetNames.Count > 0 ? IndexSetNames[0] : null;
        public string? SecondIndexSetName => IndexSetNames.Count > 1 ? IndexSetNames[1] : null;

        public bool HasValue
        {
            get
            {
                if (IsScalar)
                    return Value != null;
                else
                    return indexedValues != null && indexedValues.Count > 0;
            }
        }

        // Constructors

        /// <summary>
        /// Creates a scalar (non-indexed) parameter
        /// </summary>
        public Parameter(string name, ParameterType type, object value)
        {
            Name = name;
            Type = type;
            Value = value;
            IsIndexed = false;
            IndexSetNames = new List<string>();
        }

        /// <summary>
        /// Creates an indexed parameter (1D, 2D, or N-dimensional)
        /// </summary>
        public Parameter(string name, ParameterType type, IEnumerable<string> indexSetNames, bool isExternal)
        {
            Name = name;
            Type = type;
            IsIndexed = true;
            IndexSetNames = new List<string>(indexSetNames);
            IsExternal = isExternal;
            indexedValues = new Dictionary<string, object>();
        }

        /// <summary>
        /// Creates a 1D indexed parameter (convenience constructor)
        /// </summary>
        public Parameter(string name, ParameterType type, string indexSetName, bool isExternal)
            : this(name, type, new[] { indexSetName }, isExternal)
        {
        }

        /// <summary>
        /// Creates a 2D indexed parameter (convenience constructor)
        /// </summary>
        public Parameter(string name, ParameterType type, string indexSetName, string secondIndexSetName, bool isExternal)
            : this(name, type, new[] { indexSetName, secondIndexSetName }, isExternal)
        {
        }

        // Value accessors

        /// <summary>
        /// Sets a value for a 1D indexed parameter
        /// </summary>
        public void SetIndexedValue(int index, object value)
        {
            if (!IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is not indexed");

            if (Dimensionality != 1)
                throw new InvalidOperationException($"Parameter '{Name}' has {Dimensionality} dimensions, use SetIndexedValue with {Dimensionality} indices");

            indexedValues ??= new Dictionary<string, object>();
            indexedValues[index.ToString()] = value;
        }

        /// <summary>
        /// Gets a value for a 1D indexed parameter
        /// </summary>
        public object? GetIndexedValue(int index)
        {
            if (!IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is not indexed");

            if (Dimensionality != 1)
                throw new InvalidOperationException($"Parameter '{Name}' has {Dimensionality} dimensions, use GetIndexedValue with {Dimensionality} indices");

            return indexedValues?.TryGetValue(index.ToString(), out var value) == true ? value : null;
        }

        /// <summary>
        /// Sets a value for a 2D indexed parameter
        /// </summary>
        public void SetIndexedValue(int index1, int index2, object value)
        {
            if (!IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is not indexed");

            if (Dimensionality != 2)
                throw new InvalidOperationException($"Parameter '{Name}' has {Dimensionality} dimensions, use SetIndexedValue with {Dimensionality} indices");

            indexedValues ??= new Dictionary<string, object>();
            string key = BuildKey(index1, index2);
            indexedValues[key] = value;
        }

        /// <summary>
        /// Gets a value for a 2D indexed parameter
        /// </summary>
        public object? GetIndexedValue(int index1, int index2)
        {
            if (!IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is not indexed");

            if (Dimensionality != 2)
                throw new InvalidOperationException($"Parameter '{Name}' has {Dimensionality} dimensions, use GetIndexedValue with {Dimensionality} indices");

            string key = BuildKey(index1, index2);
            return indexedValues?.TryGetValue(key, out var value) == true ? value : null;
        }

        /// <summary>
        /// Sets a value for an N-dimensional indexed parameter
        /// </summary>
        public void SetIndexedValue(object value, params int[] indices)
        {
            if (!IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is not indexed");

            if (indices.Length != Dimensionality)
                throw new InvalidOperationException($"Parameter '{Name}' requires {Dimensionality} indices, but {indices.Length} were provided");

            indexedValues ??= new Dictionary<string, object>();
            string key = BuildKey(indices);
            indexedValues[key] = value;
        }

        /// <summary>
        /// Gets a value for an N-dimensional indexed parameter
        /// </summary>
        public object? GetIndexedValue(params int[] indices)
        {
            if (!IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is not indexed");

            if (indices.Length != Dimensionality)
                throw new InvalidOperationException($"Parameter '{Name}' requires {Dimensionality} indices, but {indices.Length} were provided");

            string key = BuildKey(indices);
            return indexedValues?.TryGetValue(key, out var value) == true ? value : null;
        }

        // Helper methods

        private string BuildKey(params int[] indices)
        {
            return string.Join("_", indices);
        }

        public IEnumerable<int> GetDefinedIndices()
        {
            if (IsScalar)
                return Enumerable.Empty<int>();

            if (Dimensionality != 1)
                throw new InvalidOperationException($"Parameter '{Name}' has {Dimensionality} dimensions, use GetDefinedKeys()");

            return indexedValues?.Keys.Select(int.Parse) ?? Enumerable.Empty<int>();
        }

        public IEnumerable<(int, int)> GetDefinedIndexPairs()
        {
            if (Dimensionality != 2)
                throw new InvalidOperationException($"Parameter '{Name}' has {Dimensionality} dimensions, not 2");

            if (indexedValues == null)
                return Enumerable.Empty<(int, int)>();

            return indexedValues.Keys.Select(key =>
            {
                var parts = key.Split('_');
                return (int.Parse(parts[0]), int.Parse(parts[1]));
            });
        }

        public IEnumerable<string> GetDefinedKeys()
        {
            return indexedValues?.Keys ?? Enumerable.Empty<string>();
        }

        public override string ToString()
        {
            if (IsScalar)
            {
                return $"{Type} {Name} = {Value}";
            }
            else
            {
                string indexSpec = string.Join("][", IndexSetNames.Select(s => s));
                return $"{Type} {Name}[{indexSpec}] = {(IsExternal ? "..." : "computed")}";
            }
        }
    }
}