using System.Text.RegularExpressions;
using Core.Models;


namespace Core.Parsing
{

    public class TupleParameterParser
    {
        private readonly ModelManager modelManager;

        public bool TryParse(string statement, out TupleParameter? param, out string error)
        {
            param = null;
            error = string.Empty;

            // Pattern: TupleType paramName = item(...);
            // Pattern: TupleType paramName[i in Set] = item(...);

            var scalarPattern = @"^([a-zA-Z][a-zA-Z0-9_]*)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), scalarPattern);

            if (!match.Success)
            {
                error = "Not a tuple parameter declaration";
                return false;
            }

            string typeName = match.Groups[1].Value;
            string paramName = match.Groups[2].Value;
            string valueExpr = match.Groups[3].Value.Trim();

            // Check if type is a tuple schema
            if (!modelManager.TupleSchemas.ContainsKey(typeName))
            {
                error = $"Not a tuple parameter ('{typeName}' is not a tuple schema)";
                return false;
            }

            param = new TupleParameter(paramName, typeName);

            // Store the expression for later evaluation
            // (item() will be evaluated when data is available)

            return true;
        }
    }

}