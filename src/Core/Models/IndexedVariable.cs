namespace Core.Models
{
    /// <summary>
    /// Represents a variable in the optimization model with optional indexing
    /// </summary>
    public class IndexedVariable
    {
        public string BaseName { get; set; }
        public string IndexSetName { get; set; }
        public string? SecondIndexSetName { get; set; }
        public VariableType Type { get; set; }

        public IndexedVariable(string baseName, string indexSetName, VariableType type, string? secondIndexSetName = null)
        {
            BaseName = baseName;
            IndexSetName = indexSetName;
            SecondIndexSetName = secondIndexSetName;
            Type = type;
        }

        /// <summary>
        /// Returns true if this is a scalar variable (no indices)
        /// </summary>
        public bool IsScalar => string.IsNullOrEmpty(IndexSetName);

        /// <summary>
        /// Returns true if this variable has two indices
        /// </summary>
        public bool IsTwoDimensional => !string.IsNullOrEmpty(SecondIndexSetName);

        /// <summary>
        /// Gets the dimensionality of the variable (0 for scalar, 1 for single index, 2 for double index)
        /// </summary>
        public int Dimensionality
        {
            get
            {
                if (IsScalar) return 0;
                if (IsTwoDimensional) return 2;
                return 1;
            }
        }

        public override string ToString()
        {
            if (IsScalar)
                return $"{Type} {BaseName}";
            else if (IsTwoDimensional)
                return $"{Type} {BaseName}[{IndexSetName},{SecondIndexSetName}]";
            else
                return $"{Type} {BaseName}[{IndexSetName}]";
        }
    }
}