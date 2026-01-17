namespace Core.Models
{
    /// <summary>
    /// Represents a typed parameter (constant) that can be scalar or indexed
    /// </summary>
    public class Parameter
    {
        public string Name { get; }
        public ParameterType Type { get; }
        public object? Value { get; set; } // For scalar parameters
        public Dictionary<string, object>? IndexedValues { get; set; } // For indexed parameters
        public bool IsExternal { get; set; }
        
        // Index set information (similar to IndexedVariable)
        public string IndexSetName { get; } = string.Empty;
        public string? SecondIndexSetName { get; }
        
        // Computed properties
        public bool IsScalar => string.IsNullOrEmpty(IndexSetName);
        public bool IsIndexed => !IsScalar;
        public bool IsTwoDimensional => !string.IsNullOrEmpty(SecondIndexSetName);

        /// <summary>
        /// Constructor for scalar parameters
        /// </summary>
        public Parameter(string name, ParameterType type, object? value, bool isExternal = false)
        {
            Name = name;
            Type = type;
            Value = value;
            IsExternal = isExternal;
            IndexSetName = string.Empty;
            SecondIndexSetName = null;
            IndexedValues = null;
        }

        /// <summary>
        /// Constructor for indexed parameters (1D)
        /// </summary>
        public Parameter(string name, ParameterType type, string indexSetName, bool isExternal = false)
        {
            Name = name;
            Type = type;
            IndexSetName = indexSetName;
            SecondIndexSetName = null;
            IsExternal = isExternal;
            Value = null;
            IndexedValues = new Dictionary<string, object>();
        }

        /// <summary>
        /// Constructor for indexed parameters (2D)
        /// </summary>
        public Parameter(string name, ParameterType type, string indexSetName1, string indexSetName2, bool isExternal = false)
        {
            Name = name;
            Type = type;
            IndexSetName = indexSetName1;
            SecondIndexSetName = indexSetName2;
            IsExternal = isExternal;
            Value = null;
            IndexedValues = new Dictionary<string, object>();
        }

        /// <summary>
        /// Sets a value for an indexed parameter
        /// </summary>
        public void SetIndexedValue(int index, object value)
        {
            if (IsScalar)
                throw new InvalidOperationException($"Parameter '{Name}' is scalar, not indexed");
            
            if (IsTwoDimensional)
                throw new InvalidOperationException($"Parameter '{Name}' is two-dimensional. Use SetIndexedValue(int, int, object)");

            IndexedValues![index.ToString()] = value;
        }

        /// <summary>
        /// Sets a value for a two-dimensional indexed parameter
        /// </summary>
        public void SetIndexedValue(int index1, int index2, object value)
        {
            if (!IsTwoDimensional)
                throw new InvalidOperationException($"Parameter '{Name}' is not two-dimensional");

            string key = $"{index1}_{index2}";
            IndexedValues![key] = value;
        }

        /// <summary>
        /// Gets a value from an indexed parameter
        /// </summary>
        public object? GetIndexedValue(int index)
        {
            if (IsScalar)
                throw new InvalidOperationException($"Parameter '{Name}' is scalar, not indexed");
            
            if (IsTwoDimensional)
                throw new InvalidOperationException($"Parameter '{Name}' is two-dimensional. Use GetIndexedValue(int, int)");

            return IndexedValues!.TryGetValue(index.ToString(), out var value) ? value : null;
        }

        /// <summary>
        /// Gets a value from a two-dimensional indexed parameter
        /// </summary>
        public object? GetIndexedValue(int index1, int index2)
        {
            if (!IsTwoDimensional)
                throw new InvalidOperationException($"Parameter '{Name}' is not two-dimensional");

            string key = $"{index1}_{index2}";
            return IndexedValues!.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Gets the value as an integer (for scalar parameters)
        /// </summary>
        public int GetIntValue()
        {
            if (IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is indexed. Use GetIndexedValue() instead");

            if (Value == null)
                throw new InvalidOperationException($"Parameter '{Name}' has no value assigned (external data required)");

            return Type switch
            {
                ParameterType.Integer => Convert.ToInt32(Value),
                _ => throw new InvalidOperationException($"Parameter '{Name}' is of type {Type}, not Integer")
            };
        }

        /// <summary>
        /// Gets the value as a double (for scalar parameters)
        /// </summary>
        public double GetFloatValue()
        {
            if (IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is indexed. Use GetIndexedValue() instead");

            if (Value == null)
                throw new InvalidOperationException($"Parameter '{Name}' has no value assigned (external data required)");

            return Type switch
            {
                ParameterType.Float => Convert.ToDouble(Value),
                ParameterType.Integer => Convert.ToDouble(Value),
                _ => throw new InvalidOperationException($"Parameter '{Name}' is of type {Type}, not numeric")
            };
        }

        /// <summary>
        /// Gets the value as a string (for scalar parameters)
        /// </summary>
        public string GetStringValue()
        {
            if (IsIndexed)
                throw new InvalidOperationException($"Parameter '{Name}' is indexed. Use GetIndexedValue() instead");

            if (Value == null)
                throw new InvalidOperationException($"Parameter '{Name}' has no value assigned (external data required)");

            return Type switch
            {
                ParameterType.String => Value.ToString() ?? string.Empty,
                _ => throw new InvalidOperationException($"Parameter '{Name}' is of type {Type}, not String")
            };
        }

        /// <summary>
        /// Checks if the parameter has a value assigned
        /// </summary>
        public bool HasValue 
        { 
            get
            {
                if (IsScalar)
                    return Value != null;
                else
                    return IndexedValues != null && IndexedValues.Count > 0;
            }
        }

        /// <summary>
        /// Gets the number of indexed values assigned (0 for scalar parameters)
        /// </summary>
        public int IndexedValueCount => IsIndexed ? (IndexedValues?.Count ?? 0) : 0;

        public override string ToString()
        {
            string typeStr = Type switch
            {
                ParameterType.Integer => "int",
                ParameterType.Float => "float",
                ParameterType.String => "string",
                _ => "unknown"
            };

            if (IsTwoDimensional)
            {
                if (IsExternal && IndexedValueCount == 0)
                    return $"{typeStr} {Name}[{IndexSetName},{SecondIndexSetName}] = ... (external data required)";
                else
                    return $"{typeStr} {Name}[{IndexSetName},{SecondIndexSetName}] ({IndexedValueCount} values)";
            }
            else if (IsIndexed)
            {
                if (IsExternal && IndexedValueCount == 0)
                    return $"{typeStr} {Name}[{IndexSetName}] = ... (external data required)";
                else
                    return $"{typeStr} {Name}[{IndexSetName}] ({IndexedValueCount} values)";
            }
            else
            {
                if (IsExternal && Value == null)
                    return $"{typeStr} {Name} = ... (external data required)";
                else
                    return $"{typeStr} {Name} = {Value}";
            }
        }
    }

    /// <summary>
    /// Represents the type of a parameter
    /// </summary>
    public enum ParameterType
    {
        Integer,
        Float,
        String
    }
}