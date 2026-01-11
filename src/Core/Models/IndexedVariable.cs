namespace Core.Models
{
    /// <summary>
    /// Represents a declared indexed variable
    /// </summary>
    public class IndexedVariable
    {
        public string BaseName { get; }
        public string IndexSetName { get; }
        public VariableType Type { get; }

        public IndexedVariable(string baseName, string indexSetName, VariableType type)
        {
            BaseName = baseName;
            IndexSetName = indexSetName;
            Type = type;
        }

        public override string ToString()
        {
            string typeStr = Type switch
            {
                VariableType.Float => "float",
                VariableType.Integer => "int",
                VariableType.Boolean => "bool",
                _ => "float"
            };
            return $"var {typeStr} {BaseName}[{IndexSetName}]";
        }
    }
}