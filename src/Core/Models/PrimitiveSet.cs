    namespace Core.Models
{
    /// <summary>
    /// Represents a set of primitive values (int, string, float) compatible with OPL syntax
    /// Examples: {int} nodes = {1, 2, 3}; or {string} cities = ...;
    /// </summary>
    public class PrimitiveSet
    {
        public string Name { get; }
        public PrimitiveSetType Type { get; }
        public bool IsExternal { get; }
        
        private readonly HashSet<int> intValues = new HashSet<int>();
        private readonly HashSet<string> stringValues = new HashSet<string>();
        private readonly HashSet<double> floatValues = new HashSet<double>();
        
        /// <summary>
        /// Creates a primitive set
        /// </summary>
        public PrimitiveSet(string name, PrimitiveSetType type, bool isExternal = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            IsExternal = isExternal;
        }
        
        /// <summary>
        /// Number of elements in the set
        /// </summary>
        public int Count => Type switch
        {
            PrimitiveSetType.Int => intValues.Count,
            PrimitiveSetType.String => stringValues.Count,
            PrimitiveSetType.Float => floatValues.Count,
            _ => 0
        };
        
        /// <summary>
        /// Adds a value to the set
        /// </summary>
        public void Add(object value)
        {
            switch (Type)
            {
                case PrimitiveSetType.Int:
                    intValues.Add(Convert.ToInt32(value));
                    break;
                case PrimitiveSetType.String:
                    stringValues.Add(value.ToString()!);
                    break;
                case PrimitiveSetType.Float:
                    floatValues.Add(Convert.ToDouble(value));
                    break;
            }
        }
        
        /// <summary>
        /// Checks if a value exists in the set
        /// </summary>
        public bool Contains(object value)
        {
            return Type switch
            {
                PrimitiveSetType.Int when value is int intVal => intValues.Contains(intVal),
                PrimitiveSetType.String when value is string strVal => stringValues.Contains(strVal),
                PrimitiveSetType.Float when value is double floatVal => floatValues.Contains(floatVal),
                _ => false
            };
        }
        
        /// <summary>
        /// Gets all integer values (only valid for Int type)
        /// </summary>
        public IEnumerable<int> GetIntValues()
        {
            if (Type != PrimitiveSetType.Int)
                throw new InvalidOperationException($"Set '{Name}' is not an integer set");
            return intValues;
        }
        
        /// <summary>
        /// Gets all string values (only valid for String type)
        /// </summary>
        public IEnumerable<string> GetStringValues()
        {
            if (Type != PrimitiveSetType.String)
                throw new InvalidOperationException($"Set '{Name}' is not a string set");
            return stringValues;
        }
        
        /// <summary>
        /// Gets all float values (only valid for Float type)
        /// </summary>
        public IEnumerable<double> GetFloatValues()
        {
            if (Type != PrimitiveSetType.Float)
                throw new InvalidOperationException($"Set '{Name}' is not a float set");
            return floatValues;
        }
        
        /// <summary>
        /// Gets all values in the set as an enumerable of objects
        /// </summary>
        public IEnumerable<object> GetAllValues()
        {
            return Type switch
            {
                PrimitiveSetType.Int => intValues.Cast<object>(),
                PrimitiveSetType.String => stringValues.Cast<object>(),
                PrimitiveSetType.Float => floatValues.Cast<object>(),
                _ => Enumerable.Empty<object>()
            };
        }
        
        /// <summary>
        /// Gets an element at a specific index (1-based, OPL style)
        /// </summary>
        public object? GetAt(int index)
        {
            if (index < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index must be 1 or greater (1-based indexing)");
            }
            
            int zeroBasedIndex = index - 1;
            
            return Type switch
            {
                PrimitiveSetType.Int when zeroBasedIndex < intValues.Count => intValues.ElementAt(zeroBasedIndex),
                PrimitiveSetType.String when zeroBasedIndex < stringValues.Count => stringValues.ElementAt(zeroBasedIndex),
                PrimitiveSetType.Float when zeroBasedIndex < floatValues.Count => floatValues.ElementAt(zeroBasedIndex),
                _ => null
            };
        }
        
        /// <summary>
        /// Clears all values from the set
        /// </summary>
        public void Clear()
        {
            intValues.Clear();
            stringValues.Clear();
            floatValues.Clear();
        }
        
        public override string ToString()
        {
            if (IsExternal && Count == 0)
            {
                return $"{{{Type.ToString().ToLower()}}} {Name} = ... (external data - not loaded)";
            }
            
            if (Count == 0)
            {
                return $"{{{Type.ToString().ToLower()}}} {Name} = {{}}";
            }
            
            string typeStr = Type.ToString().ToLower();
            string values = Type switch
            {
                PrimitiveSetType.Int => string.Join(", ", intValues.OrderBy(v => v)),
                PrimitiveSetType.String => string.Join(", ", stringValues.Select(s => $"\"{s}\"")),
                PrimitiveSetType.Float => string.Join(", ", floatValues.OrderBy(v => v)
                    .Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                _ => ""
            };
            
            return $"{{{typeStr}}} {Name} = {{{values}}}";
        }
    }
    
    /// <summary>
    /// Type of values stored in a primitive set
    /// </summary>
    public enum PrimitiveSetType
    {
        Int,
        String,
        Float
    }
}