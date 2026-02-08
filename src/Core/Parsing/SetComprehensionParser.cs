using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses advanced set comprehensions with multiple iterators, conditions, and projections
    /// Examples:
    ///   {n | n in nodes: n.stage >= 1}
    ///   {c | c in contracts: c.type == "Buy"}
    ///   {s | s in stations, d in defs: s.id == d.stationId && d.id == g}
    ///   {i.id | i in HydroNodes}
    ///   {Station} filtered[g in groups] = {s | s in stations: s.groupId == g}
    /// </summary>
    public class SetComprehensionParser
    {
        private readonly ModelManager modelManager;

        public SetComprehensionParser(ModelManager manager)
        {
            modelManager = manager;
        }

        public bool TryParse(string statement, out ComputedSet? result, out string error)
        {
            result = null;
            error = string.Empty;

            statement = statement.Trim();

            // Pattern 1: Indexed set collection
            // {Type} setName[index] = {...}
            var indexedPattern = @"^\s*\{([a-zA-Z][a-zA-Z0-9_]*)\}\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\]\s*=\s*(.+)$";
            var indexedMatch = Regex.Match(statement, indexedPattern);

            if (indexedMatch.Success)
            {
                return ParseIndexedSetCollection(indexedMatch, out result, out error);
            }

            // Pattern 2: Simple set comprehension
            // {Type} setName = {expr | iterators: condition}
            var simplePattern = @"^\s*\{([a-zA-Z][a-zA-Z0-9_]*)\}\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
            var simpleMatch = Regex.Match(statement, simplePattern);

            if (!simpleMatch.Success)
            {
                error = "Not a set comprehension";
                return false;
            }

            string elementType = simpleMatch.Groups[1].Value;
            string setName = simpleMatch.Groups[2].Value;
            string comprehensionExpr = simpleMatch.Groups[3].Value.Trim();

            // Must start with '{'
            if (!comprehensionExpr.StartsWith("{"))
            {
                error = "Not a set comprehension";
                return false;
            }

            // Must contain '|' for comprehension syntax
            if (!comprehensionExpr.Contains('|'))
            {
                error = "Not a set comprehension";
                return false;
            }

            return ParseSetComprehension(setName, elementType, comprehensionExpr, out result, out error);
        }

        private bool ParseIndexedSetCollection(Match match, out ComputedSet? result, out string error)
        {
            result = null;
            error = string.Empty;

            string elementType = match.Groups[1].Value;
            string setName = match.Groups[2].Value;
            string indexVar = match.Groups[3].Value;
            string indexSet = match.Groups[4].Value;
            string comprehensionExpr = match.Groups[5].Value.Trim();

            // Validate index set exists
            if (!modelManager.IndexSets.ContainsKey(indexSet) &&
                !modelManager.Ranges.ContainsKey(indexSet) &&
                !modelManager.PrimitiveSets.ContainsKey(indexSet))
            {
                error = $"Index set '{indexSet}' not found";
                return false;
            }

            // Parse the comprehension expression
            if (!ParseSetComprehension(setName, elementType, comprehensionExpr, out var comprehension, out error))
            {
                return false;
            }

            // Create an indexed computed set
            result = new IndexedComputedSet(setName, elementType, indexVar, indexSet, comprehension!);
            return true;
        }

        private bool ParseSetComprehension(string setName, string elementType, string comprehensionExpr, 
            out ComputedSet? result, out string error)
        {
            result = null;
            error = string.Empty;

            // Remove outer braces
            if (!comprehensionExpr.StartsWith("{") || !comprehensionExpr.EndsWith("}"))
            {
                error = "Set comprehension must be enclosed in braces";
                return false;
            }

            string content = comprehensionExpr.Substring(1, comprehensionExpr.Length - 2).Trim();

            // Split by pipe '|'
            int pipeIndex = FindTopLevelChar(content, '|');
            if (pipeIndex == -1)
            {
                error = "Set comprehension missing '|' separator";
                return false;
            }

            string outputExpr = content.Substring(0, pipeIndex).Trim();
            string iteratorsPart = content.Substring(pipeIndex + 1).Trim();

            // Parse iterators and condition
            // Format: "i in Set" or "i in Set: condition" or "i in Set1, j in Set2: condition"
            List<SetIterator> iterators;
            Expression? condition = null;

            int colonIndex = FindTopLevelChar(iteratorsPart, ':');
            if (colonIndex >= 0)
            {
                string iteratorsStr = iteratorsPart.Substring(0, colonIndex).Trim();
                string conditionStr = iteratorsPart.Substring(colonIndex + 1).Trim();

                iterators = ParseIterators(iteratorsStr, out error);
                if (iterators == null)
                    return false;

                condition = ParseCondition(conditionStr, out error);
                if (condition == null && !string.IsNullOrEmpty(error))
                    return false;
            }
            else
            {
                iterators = ParseIterators(iteratorsPart, out error);
                if (iterators == null)
                    return false;
            }

            // Determine if it's a projection or filter
            bool isProjection = IsProjectionExpression(outputExpr);

            result = new ComputedSet(setName, elementType, iterators, outputExpr, condition, isProjection);
            return true;
        }

        private List<SetIterator>? ParseIterators(string iteratorsStr, out string error)
        {
            error = string.Empty;
            var iterators = new List<SetIterator>();

            // Split by comma at top level
            var parts = SplitByCommaTopLevel(iteratorsStr);

            foreach (var part in parts)
            {
                // Format: "varName in SetName"
                var match = Regex.Match(part.Trim(), @"^([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)$");
                if (!match.Success)
                {
                    error = $"Invalid iterator syntax: '{part}'";
                    return null;
                }

                string varName = match.Groups[1].Value;
                string setName = match.Groups[2].Value;

                iterators.Add(new SetIterator(varName, setName));
            }

            return iterators;
        }

        private Expression? ParseCondition(string conditionStr, out string error)
        {
            error = string.Empty;

            try
            {
                // Use EquationParser's expression parsing
                var parser = new EquationParser(modelManager);
                return parser.ParseExpression(conditionStr);
            }
            catch (Exception ex)
            {
                error = $"Error parsing condition: {ex.Message}";
                return null;
            }
        }

        private bool IsProjectionExpression(string expr)
        {
            // Projection if it's accessing a field: variable.field
            return Regex.IsMatch(expr, @"^[a-zA-Z][a-zA-Z0-9_]*\.[a-zA-Z][a-zA-Z0-9_]*$");
        }

        private int FindTopLevelChar(string text, char target)
        {
            int depth = 0;
            int angleDepth = 0;
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes)
                {
                    if (c == '(') depth++;
                    else if (c == ')') depth--;
                    else if (c == '<') angleDepth++;
                    else if (c == '>') angleDepth--;
                    else if (c == target && depth == 0 && angleDepth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private List<string> SplitByCommaTopLevel(string text)
        {
            var parts = new List<string>();
            var current = new System.Text.StringBuilder();
            int depth = 0;
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                }
                else if (!inQuotes)
                {
                    if (c == '(' || c == '<' || c == '[') depth++;
                    else if (c == ')' || c == '>' || c == ']') depth--;
                    else if (c == ',' && depth == 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                        continue;
                    }

                    current.Append(c);
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                parts.Add(current.ToString());
            }

            return parts;
        }
    }
}