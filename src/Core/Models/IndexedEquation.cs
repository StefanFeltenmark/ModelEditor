namespace Core.Models
{
    /// <summary>
    /// Represents a template for indexed equations
    /// </summary>
    public class IndexedEquation
    {
        public string BaseName { get; set; }
        public string IndexSetName { get; set; }
        public string EquationTemplate { get; set; }

        public IndexedEquation(string baseName, string indexSetName, string equationTemplate)
        {
            BaseName = baseName;
            IndexSetName = indexSetName;
            EquationTemplate = equationTemplate;
        }

        public override string ToString()
        {
            return $"equation {BaseName}[{IndexSetName}]: {EquationTemplate}";
        }
    }
}