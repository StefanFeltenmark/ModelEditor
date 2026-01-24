namespace Core.Models
{
    /// <summary>
    /// Represents a single instance (row) of a tuple with typed fields
    /// </summary>
    public class TupleInstance
    {
        public string SchemaName { get; }
        public Dictionary<string, object> Fields { get; } = new Dictionary<string, object>();
        
        public TupleInstance(string schemaName)
        {
            SchemaName = schemaName ?? throw new ArgumentNullException(nameof(schemaName));
        }
        
        /// <summary>
        /// Sets the value for a specific field
        /// </summary>
        public void SetValue(string fieldName, object value)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("Field name cannot be empty", nameof(fieldName));
            
            Fields[fieldName] = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        /// <summary>
        /// Gets the value for a specific field
        /// </summary>
        public object? GetValue(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("Field name cannot be empty", nameof(fieldName));
            
            return Fields.TryGetValue(fieldName, out var value) ? value : null;
        }
        
        /// <summary>
        /// Gets a typed value for a specific field
        /// </summary>
        public T? GetValue<T>(string fieldName)
        {
            var value = GetValue(fieldName);
            if (value == null)
                return default;
            
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }
        
        /// <summary>
        /// Checks if a field exists in this instance
        /// </summary>
        public bool HasField(string fieldName)
        {
            return Fields.ContainsKey(fieldName);
        }
        
        /// <summary>
        /// Gets all field names in this instance
        /// </summary>
        public IEnumerable<string> GetFieldNames()
        {
            return Fields.Keys;
        }
        
        public override string ToString()
        {
            var fieldValues = Fields.Select(kvp =>
            {
                var value = kvp.Value;
                string valueStr = value switch
                {
                    string s => $"\"{s}\"",
                    double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _ => value?.ToString() ?? "null"
                };
                return $"{kvp.Key}={valueStr}";
            });
            
            return $"{SchemaName}<{string.Join(", ", fieldValues)}>";
        }
    }
}