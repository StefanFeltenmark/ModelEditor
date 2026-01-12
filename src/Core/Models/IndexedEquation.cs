namespace Core.Models
{
    /// <summary>
    /// Represents an indexed equation template that expands over one or two index sets
    /// </summary>
    public class IndexedEquation
    {
        public string BaseName { get; set; }
        public string IndexSetName { get; set; }
        public string? SecondIndexSetName { get; set; }
        public string Template { get; set; }

        public IndexedEquation(string baseName, string indexSetName, string template, string? secondIndexSetName = null)
        {
            BaseName = baseName;
            IndexSetName = indexSetName;
            SecondIndexSetName = secondIndexSetName;
            Template = template;
        }

        /// <summary>
        /// Returns true if this equation has two indices
        /// </summary>
        public bool IsTwoDimensional => !string.IsNullOrEmpty(SecondIndexSetName);

        public override string ToString()
        {
            if (IsTwoDimensional)
                return $"equation {BaseName}[{IndexSetName},{SecondIndexSetName}]: {Template}";
            else
                return $"equation {BaseName}[{IndexSetName}]: {Template}";
        }
    }
}