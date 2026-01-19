using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Validates that variables used in equations are properly declared
    /// </summary>
    public class VariableValidator
    {
        private readonly ModelManager modelManager;

        public VariableValidator(ModelManager manager)
        {
            modelManager = manager;
        }

        public bool ValidateVariableDeclarations(List<string> coefficients, out string error)
        {
            error = string.Empty;
            var undeclaredVariables = new List<string>();

            foreach (var variableName in coefficients)
            {
                string baseVariableName = ExtractBaseVariableName(variableName);

                bool isDeclaredAsVariable = modelManager.IndexedVariables.ContainsKey(baseVariableName);
                bool isDeclaredAsParameter = modelManager.Parameters.ContainsKey(baseVariableName);

                if (!isDeclaredAsVariable && !isDeclaredAsParameter)
                {
                    undeclaredVariables.Add(baseVariableName);
                }
            }

            if (undeclaredVariables.Any())
            {
                var uniqueUndeclared = undeclaredVariables.Distinct().OrderBy(v => v).ToList();

                if (uniqueUndeclared.Count == 1)
                {
                    error = $"Variable '{uniqueUndeclared[0]}' is used but not declared. Use 'var {uniqueUndeclared[0]}' or 'var {uniqueUndeclared[0]}[IndexSet]' to declare it";
                }
                else
                {
                    error = $"Variables {string.Join(", ", uniqueUndeclared.Select(v => $"'{v}'"))} are used but not declared. Variables must be declared before use";
                }

                return false;
            }

            return true;
        }

        private string ExtractBaseVariableName(string variableName)
        {
            // Check if exact name exists
            if (modelManager.IndexedVariables.ContainsKey(variableName) ||
                modelManager.Parameters.ContainsKey(variableName))
            {
                return variableName;
            }

            // Pattern for indexed with variable indices: x_idx_i
            if (variableName.Contains("_idx_"))
            {
                int idxPos = variableName.IndexOf("_idx_");
                return variableName.Substring(0, idxPos);
            }

            // Pattern for 2D numeric indices: x1_2
            var match = Regex.Match(variableName, @"^([a-zA-Z][a-zA-Z0-9_]*?)(\d+_\d+)$");
            if (match.Success)
            {
                string baseName = match.Groups[1].Value;
                if (modelManager.IndexedVariables.ContainsKey(baseName))
                    return baseName;
            }

            // Pattern for 1D numeric index: x1
            match = Regex.Match(variableName, @"^([a-zA-Z]+)(\d+)$");
            if (match.Success)
            {
                string baseName = match.Groups[1].Value;
                if (modelManager.IndexedVariables.ContainsKey(baseName))
                    return baseName;
            }

            return variableName;
        }
    }
}