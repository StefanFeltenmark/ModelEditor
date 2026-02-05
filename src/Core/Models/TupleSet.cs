namespace Core.Models
{
    /// <summary>
    /// Represents a set of structured tuples compatible with OPL syntax
    /// </summary>
    public class TupleSet
    {
        public string Name { get; }
        public string SchemaName { get; }
        public bool IsExternal { get; }
        public List<TupleInstance> Instances { get; }
        
        public string? IndexSetName { get; set; }  // Optional: which index set indexes this
        public bool IsIndexed => !string.IsNullOrEmpty(IndexSetName);

        /// <summary>
        /// Creates a tuple set based on a schema
        /// </summary>
        /// <param name="name">Name of the tuple set</param>
        /// <param name="schemaName">Name of the tuple schema this set uses</param>
        /// <param name="isExternal">Whether data is loaded from external file</param>
        public TupleSet(string name, string schemaName, bool isExternal = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            SchemaName = schemaName ?? throw new ArgumentNullException(nameof(schemaName));
            IsExternal = isExternal;
            Instances = new List<TupleInstance>();
        }
        
        public TupleSet(string name, string schemaName, string indexSetName, bool isExternal)
            : this(name, schemaName, isExternal)
        {
            IndexSetName = indexSetName;
        }
        /// <summary>
        /// Number of tuple instances in this set
        /// </summary>
        public int Count => Instances.Count;
        
        /// <summary>
        /// Adds a tuple instance to the set
        /// </summary>
        public void AddInstance(TupleInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            
            if (instance.SchemaName != SchemaName)
                throw new InvalidOperationException(
                    $"Cannot add instance of schema '{instance.SchemaName}' to tuple set expecting schema '{SchemaName}'");
            
            Instances.Add(instance);
        }
        
        /// <summary>
        /// Gets a tuple instance by index (0-based)
        /// </summary>
        public TupleInstance this[int index]
        {
            get
            {
                if (index < 0 || index >= Instances.Count)
                    throw new IndexOutOfRangeException($"Index {index} is out of range for tuple set '{Name}' (count: {Instances.Count})");
                return Instances[index];
            }
        }
        
        /// <summary>
        /// Checks if a tuple instance with matching field values exists
        /// </summary>
        public bool Contains(params object[] fieldValues)
        {
            foreach (var instance in Instances)
            {
                bool allMatch = true;
                int fieldIndex = 0;
                
                foreach (var field in instance.Fields)
                {
                    if (fieldIndex >= fieldValues.Length)
                    {
                        allMatch = false;
                        break;
                    }
                    
                    var instanceValue = field.Value;
                    var searchValue = fieldValues[fieldIndex++];
                    
                    if (!Equals(instanceValue, searchValue))
                    {
                        allMatch = false;
                        break;
                    }
                }
                
                if (allMatch && fieldIndex == fieldValues.Length)
                    return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Clears all instances from the set
        /// </summary>
        public void Clear()
        {
            Instances.Clear();
        }
        
        public override string ToString()
        {
            if (IsExternal && Count == 0)
            {
                return $"{{{SchemaName}}} {Name} = ... (external data - not loaded)";
            }
            
            if (Count == 0)
            {
                return $"{{{SchemaName}}} {Name} = {{}}";
            }
            
            var tupleStrings = Instances.Select(instance => 
            {
                var values = instance.Fields.Select(v => 
                {
                    if (v.Value is string s)
                        return $"\"{s}\"";
                    else if (v.Value is double d)
                        return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    else
                        return v.Value?.ToString() ?? "null";
                });
                return $"<{string.Join(", ", values)}>";
            });
            
            return $"{{{SchemaName}}} {Name} = {{{string.Join(", ", tupleStrings)}}}";
        }

        /// <summary>
        /// Finds a tuple instance by its key values (requires the schema to be provided)
        /// </summary>
        /// <param name="schema">The tuple schema defining the keys</param>
        /// <param name="keyValues">Values for the key fields in order</param>
        /// <returns>The matching tuple instance, or null if not found</returns>
        public TupleInstance? FindByKey(TupleSchema schema, params object[] keyValues)
        {
            if (schema == null)
                throw new ArgumentNullException(nameof(schema));
            
            if (keyValues.Length != schema.KeyFields.Count)
                throw new ArgumentException(
                    $"Expected {schema.KeyFields.Count} key values but got {keyValues.Length}");
            
            // Find matching instance
            foreach (var instance in Instances)
            {
                bool allMatch = true;
                for (int i = 0; i < schema.KeyFields.Count; i++)
                {
                    var keyField = schema.KeyFields[i];
                    var instanceValue = instance.GetValue(keyField);
                    var searchValue = keyValues[i];
                    
                    if (!Equals(instanceValue, searchValue))
                    {
                        allMatch = false;
                        break;
                    }
                }
                
                if (allMatch)
                    return instance;
            }
            
            return null;
        }
    }
}