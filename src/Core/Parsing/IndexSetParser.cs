using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    public class IndexSetParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;

        public IndexSetParser(ModelManager manager, ExpressionEvaluator eval)
        {
            modelManager = manager;
            evaluator = eval;
        }

        public bool TryParse(string statement, out IndexSet? indexSet, out string error)
        {
            indexSet = null;
            error = string.Empty;

            string pattern = @"^\s*range\s+([a-zA-Z][a-zA-Z0-9]*)\s*=\s*([a-zA-Z0-9]+)\.\.([a-zA-Z0-9]+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not an index set declaration";
                return false;
            }

            string name = match.Groups[1].Value;
            string startStr = match.Groups[2].Value;
            string endStr = match.Groups[3].Value;

            var startResult = evaluator.EvaluateIntExpression(startStr);
            if (!startResult.IsSuccess)
            {
                error = $"Invalid start index '{startStr}': {startResult.ErrorMessage}";
                return false;
            }

            var endResult = evaluator.EvaluateIntExpression(endStr);
            if (!endResult.IsSuccess)
            {
                error = $"Invalid end index '{endStr}': {endResult.ErrorMessage}";
                return false;
            }

            int start = startResult.Value;
            int end = endResult.Value;

            if (start > end)
            {
                error = $"Invalid range: start index {start} is greater than end index {end}";
                return false;
            }

            indexSet = new IndexSet(name, start, end);
            return true;
        }
    }
}   