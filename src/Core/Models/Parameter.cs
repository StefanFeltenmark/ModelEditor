namespace Core.Models
{
    /// <summary>
    /// Represents a typed parameter (constant)
    /// </summary>
    public class Parameter
    {
        public string Name { get; set; }
        public ParameterType Type { get; set; }
        public object Value { get; set; }

        public Parameter(string name, ParameterType type, object value)
        {
            Name = name;
            Type = type;
            Value = value;
        }

        public int GetIntValue()
        {
            return Type == ParameterType.Integer ? (int)Value : 0;
        }

        public double GetFloatValue()
        {
            return Type == ParameterType.Float ? (double)Value : 0.0;
        }

        public string GetStringValue()
        {
            return Type == ParameterType.String ? (string)Value : string.Empty;
        }

        public override string ToString()
        {
            string typeStr = Type switch
            {
                ParameterType.Integer => "int",
                ParameterType.Float => "float",
                ParameterType.String => "string",
                _ => "unknown"
            };
            return $"{typeStr} {Name} = {Value}";
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