namespace Core.Models
{
    /// <summary>
    /// Schema definition for structured tuples with key support
    /// </summary>
    public class TupleSchema
    {
        public string Name { get; }
        public Dictionary<string, VariableType> Fields { get; } = new Dictionary<string, VariableType>();
        public List<string> KeyFields { get; } = new List<string>(); // Ordered key fields
        
        public TupleSchema(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
        
        /// <summary>
        /// Adds a field to the tuple schema
        /// </summary>
        /// <param name="name">Field name</param>
        /// <param name="type">Field type</param>
        /// <param name="isKey">Whether this field is part of the key</param>
        public void AddField(string name, VariableType type, bool isKey = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Field name cannot be empty", nameof(name));
            
            if (Fields.ContainsKey(name))
                throw new InvalidOperationException($"Field '{name}' already exists in schema '{Name}'");
            
            Fields[name] = type;
            
            if (isKey)
            {
                KeyFields.Add(name);
            }
        }
        
        /// <summary>
        /// Checks if this schema has keys defined
        /// </summary>
        public bool HasKeys => KeyFields.Count > 0;
        
        /// <summary>
        /// Gets the types of the key fields in order
        /// </summary>
        public IEnumerable<VariableType> GetKeyTypes()
        {
            return KeyFields.Select(kf => Fields[kf]);
        }
        
        public override string ToString()
        {
            var fieldStrings = Fields.Select(f =>
            {
                var keyPrefix = KeyFields.Contains(f.Key) ? "key " : "";
                return $"{keyPrefix}{f.Value} {f.Key}";
            });
            return $"tuple {Name} {{ {string.Join("; ", fieldStrings)} }}";
        }
    }
}