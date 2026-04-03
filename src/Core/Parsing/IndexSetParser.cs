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

            // Pattern: range Name = a..b
            string rangePattern = @"^\s*range\s+([a-zA-Z][a-zA-Z0-9]*)\s*=\s*([a-zA-Z0-9]+)\.\.([a-zA-Z0-9]+)$";
            var rangeMatch = Regex.Match(statement.Trim(), rangePattern);
            if (rangeMatch.Success)
            {
                string name = rangeMatch.Groups[1].Value;
                string startStr = rangeMatch.Groups[2].Value;
                string endStr = rangeMatch.Groups[3].Value;

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
                int end   = endResult.Value;

                // When both endpoints are plain integer literals, reject start > end immediately.
                // When either is a parameter reference, allow it (value may not be loaded yet).
                bool startIsLiteral = int.TryParse(startStr, out _);
                bool endIsLiteral   = int.TryParse(endStr,   out _);
                if (startIsLiteral && endIsLiteral && start > end)
                {
                    error = $"Range start ({start}) is greater than end ({end})";
                    return false;
                }

                indexSet = new IndexSet(name, start, end);
                return true;
            }

            // Pattern: range Name = A union B  /  A inter B  /  A \ B
            string setOpPattern = @"^\s*range\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)\s+(union|inter|\\\\|except)\s+(.+)$";
            var setOpMatch = Regex.Match(statement.Trim(), setOpPattern, RegexOptions.IgnoreCase);
            // Also handle backslash without doubling
            if (!setOpMatch.Success)
            {
                setOpPattern = @"^\s*range\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*([a-zA-Z][a-zA-Z0-9_]*)\s+(union|inter|except)\s+([a-zA-Z][a-zA-Z0-9_]*)$";
                setOpMatch = Regex.Match(statement.Trim(), setOpPattern, RegexOptions.IgnoreCase);
            }

            if (setOpMatch.Success)
            {
                string name = setOpMatch.Groups[1].Value;
                string leftName = setOpMatch.Groups[2].Value.Trim();
                string op = setOpMatch.Groups[3].Value.ToLowerInvariant();
                string rightName = setOpMatch.Groups[4].Value.Trim();

                var leftElements = ResolveSetElements(leftName, out string leftErr);
                if (leftElements == null) { error = $"Cannot resolve set '{leftName}': {leftErr}"; return false; }

                var rightElements = ResolveSetElements(rightName, out string rightErr);
                if (rightElements == null) { error = $"Cannot resolve set '{rightName}': {rightErr}"; return false; }

                List<int> result = op switch
                {
                    "union"  => leftElements.Union(rightElements).OrderBy(x => x).ToList(),
                    "inter"  => leftElements.Intersect(rightElements).OrderBy(x => x).ToList(),
                    "except" => leftElements.Except(rightElements).OrderBy(x => x).ToList(),
                    _        => leftElements.Union(rightElements).OrderBy(x => x).ToList()
                };

                modelManager.Sets[name] = result;
                // indexSet stays null — caller must check Sets when indexSet is null but no error
                return true;
            }

            // Try backslash difference: range K = I \ J
            var diffMatch = Regex.Match(statement.Trim(),
                @"^\s*range\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\\\s*([a-zA-Z][a-zA-Z0-9_]*)$");
            if (diffMatch.Success)
            {
                string name = diffMatch.Groups[1].Value;
                string leftName = diffMatch.Groups[2].Value;
                string rightName = diffMatch.Groups[3].Value;

                var leftElements = ResolveSetElements(leftName, out string leftErr);
                if (leftElements == null) { error = $"Cannot resolve set '{leftName}': {leftErr}"; return false; }

                var rightElements = ResolveSetElements(rightName, out string rightErr);
                if (rightElements == null) { error = $"Cannot resolve set '{rightName}': {rightErr}"; return false; }

                modelManager.Sets[name] = leftElements.Except(rightElements).OrderBy(x => x).ToList();
                return true;
            }

            error = "Not an index set declaration";
            return false;
        }

        private List<int>? ResolveSetElements(string name, out string error)
        {
            error = string.Empty;
            if (modelManager.IndexSets.TryGetValue(name, out var idxSet))
                return idxSet.GetIndices().ToList();
            if (modelManager.Sets.TryGetValue(name, out var setList))
                return setList;
            error = $"Set '{name}' is not defined";
            return null;
        }
    }
}   