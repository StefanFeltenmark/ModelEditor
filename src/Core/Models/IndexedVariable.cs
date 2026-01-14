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
        public double? LowerBound { get; set; }
        public double? UpperBound { get; set; }

        public IndexedVariable(string baseName, string indexSetName, VariableType type, string? secondIndexSetName = null, double? lowerBound = null, double? upperBound = null)
        {
            BaseName = baseName;
            IndexSetName = indexSetName;
            SecondIndexSetName = secondIndexSetName;
            Type = type;
            LowerBound = lowerBound;
            UpperBound = upperBound;
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
        /// Returns true if the variable has bounds specified
        /// </summary>
        public bool HasBounds => LowerBound.HasValue || UpperBound.HasValue;

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
            string varDecl;
            if (IsScalar)
                varDecl = $"{Type} {BaseName}";
            else if (IsTwoDimensional)
                varDecl = $"{Type} {BaseName}[{IndexSetName},{SecondIndexSetName}]";
            else
                varDecl = $"{Type} {BaseName}[{IndexSetName}]";

            if (HasBounds)
            {
                string lower = LowerBound?.ToString() ?? "-∞";
                string upper = UpperBound?.ToString() ?? "∞";
                varDecl += $" in {lower}..{upper}";
            }

            return varDecl;
        }
    }
}