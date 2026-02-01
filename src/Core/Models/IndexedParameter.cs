namespace Core.Models
{
    /// <summary>
    /// Represents one dimension of an indexed structure
    /// </summary>
    public class IndexDimension
    {
        public string IteratorVariable { get; set; }
        public string SetName { get; set; }

        public IndexDimension(string iteratorVar, string setName)
        {
            IteratorVariable = iteratorVar;
            SetName = setName;
        }

        public override string ToString()
        {
            return $"[{IteratorVariable} in {SetName}]";
        }
    }

    /// <summary>
    /// Represents a parameter indexed over one or more sets
    /// Example: ArcTData arcT[s in HydroArcs][t in T] = item(HydroArcTs, <s.id,t>);
    /// </summary>
    public class IndexedParameter
    {
        public string Name { get; set; }
        public ParameterType Type { get; set; }
        public List<IndexDimension> Dimensions { get; set; }
        public Expression? ValueExpression { get; set; }
        public bool IsExternal { get; set; }

        // For storing actual values if computed
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();

        public IndexedParameter(string name, ParameterType type)
        {
            Name = name;
            Type = type;
            Dimensions = new List<IndexDimension>();
        }

        public void AddDimension(string iteratorVar, string setName)
        {
            Dimensions.Add(new IndexDimension(iteratorVar, setName));
        }

        public object? GetValue(params object[] indices)
        {
            string key = BuildKey(indices);
            return values.TryGetValue(key, out var value) ? value : null;
        }

        public void SetValue(object value, params object[] indices)
        {
            string key = BuildKey(indices);
            values[key] = value;
        }

        private string BuildKey(params object[] indices)
        {
            return string.Join("_", indices.Select(i => i.ToString()));
        }

        public override string ToString()
        {
            string dims = string.Join("", Dimensions.Select(d => d.ToString()));
            return $"{Type} {Name}{dims} = {(IsExternal ? "..." : "computed")}";
        }
    }

    /// <summary>
    /// Represents an indexed set collection
    /// Example: {Arc} Jin[i in hydroNodeIndices] = {j | j in HydroArcs: ...};
    /// </summary>
    public class IndexedSetCollection
    {
        public string Name { get; set; }
        public string ElementType { get; set; } // For tuple sets or primitive type
        public List<IndexDimension> Dimensions { get; set; }
        public Expression? ValueExpression { get; set; }
        public bool IsExternal { get; set; }

        // For storing actual sets if computed
        private readonly Dictionary<string, object> sets = new Dictionary<string, object>();

        public IndexedSetCollection(string name, string elementType)
        {
            Name = name;
            ElementType = elementType;
            Dimensions = new List<IndexDimension>();
        }

        public void AddDimension(string iteratorVar, string setName)
        {
            Dimensions.Add(new IndexDimension(iteratorVar, setName));
        }

        public object? GetSet(params object[] indices)
        {
            string key = BuildKey(indices);
            return sets.TryGetValue(key, out var set) ? set : null;
        }

        public void SetSet(object set, params object[] indices)
        {
            string key = BuildKey(indices);
            sets[key] = set;
        }

        private string BuildKey(params object[] indices)
        {
            return string.Join("_", indices.Select(i => i.ToString()));
        }

        public override string ToString()
        {
            string dims = string.Join("", Dimensions.Select(d => d.ToString()));
            return $"{{{ElementType}}} {Name}{dims} = {(IsExternal ? "..." : "computed")}";
        }
    }
}