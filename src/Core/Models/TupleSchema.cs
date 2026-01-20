namespace Core.Models
{
    /// <summary>
    /// Represents a tuple type definition
    /// Example: tuple Product { string name; float cost; int demand; }
    /// </summary>
    public class TupleSchema
    {
        public string Name { get; set; }
        public Dictionary<string, VariableType> Fields { get; set; }

        public TupleSchema(string name)
        {
            Name = name;
            Fields = new Dictionary<string, VariableType>();
        }

        public void AddField(string fieldName, VariableType type)
        {
            if (Fields.ContainsKey(fieldName))
            {
                throw new InvalidOperationException($"Field '{fieldName}' already exists in tuple '{Name}'");
            }
            Fields[fieldName] = type;
        }

        public override string ToString()
        {
            var fields = Fields.Select(kvp => $"  {kvp.Value.ToString().ToLower()} {kvp.Key};");
            return $"tuple {Name} {{\n{string.Join("\n", fields)}\n}}";
        }
    }

    /// <summary>
    /// Represents a tuple instance (a record)
    /// </summary>
    public class TupleInstance
    {
        public string SchemaName { get; set; }
        public Dictionary<string, object> Values { get; set; }

        public TupleInstance(string schemaName)
        {
            SchemaName = schemaName;
            Values = new Dictionary<string, object>();
        }

        public void SetValue(string fieldName, object value)
        {
            Values[fieldName] = value;
        }

        public object? GetValue(string fieldName)
        {
            return Values.TryGetValue(fieldName, out var value) ? value : null;
        }

        public override string ToString()
        {
            var values = Values.Select(kvp => $"{kvp.Key}={kvp.Value}");
            return $"<{string.Join(", ", values)}>";
        }
    }

    /// <summary>
    /// Represents a set of tuples
    /// Example: {Product} Products = ...;
    /// </summary>
    public class TupleSet
    {
        public string Name { get; set; }
        public string SchemaName { get; set; }
        public List<TupleInstance> Instances { get; set; }
        public bool IsExternal { get; set; }

        public TupleSet(string name, string schemaName, bool isExternal = false)
        {
            Name = name;
            SchemaName = schemaName;
            Instances = new List<TupleInstance>();
            IsExternal = isExternal;
        }

        public void AddInstance(TupleInstance instance)
        {
            if (instance.SchemaName != SchemaName)
            {
                throw new InvalidOperationException(
                    $"Tuple instance schema '{instance.SchemaName}' does not match set schema '{SchemaName}'");
            }
            Instances.Add(instance);
        }

        public override string ToString()
        {
            string externalMarker = IsExternal ? " = ..." : "";
            return $"{{{SchemaName}}} {Name}{externalMarker};";
        }
    }
}