using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses OPL set comprehension declarations
    /// </summary>
    public class SetComprehensionParser
    {
        private readonly ModelManager modelManager;
        
        public SetComprehensionParser(ModelManager manager)
        {
            modelManager = manager;
        }
        
        /// <summary>
        /// Tries to parse a set comprehension declaration
        /// </summary>
        public bool TryParse(string statement, out ComputedSet? computedSet, out string error)
        {
            computedSet = null;
            error = string.Empty;
            
            statement = statement.Trim();
            
            // Early exit: Set comprehensions MUST have a pipe character |
            if (!statement.Contains('|'))
            {
                error = "Not a set comprehension declaration";
                return false;
            }
            
            // Early exit: Reject if content starts with angle bracket (tuple data)
            var contentMatch = Regex.Match(statement, @"=\s*\{(.+)\}");
            if (contentMatch.Success)
            {
                string content = contentMatch.Groups[1].Value.Trim();
                if (content.StartsWith("<"))
                {
                    error = "Not a set comprehension declaration";
                    return false;
                }
            }
            
            // Pattern 1: Indexed set comprehension
            // {Type} name[indexVar in IndexSet] = {expr | ...}
            string indexedPattern = @"^\s*\{([a-zA-Z][a-zA-Z0-9_]*)\}\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\]\s*=\s*\{(.+)\}$";
            var indexedMatch = Regex.Match(statement, indexedPattern);
            
            if (indexedMatch.Success)
            {
                return ParseIndexedSetComprehension(indexedMatch, out computedSet, out error);
            }
            
            // Pattern 2: Simple set comprehension
            // {Type} name = {expr | ...}
            string simplePattern = @"^\s*\{([a-zA-Z][a-zA-Z0-9_]*)\}\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*\{(.+)\}$";
            var simpleMatch = Regex.Match(statement, simplePattern);
            
            if (simpleMatch.Success)
            {
                return ParseSimpleSetComprehension(simpleMatch, out computedSet, out error);
            }
            
            error = "Not a set comprehension declaration";
            return false;
        }
        
        private bool ParseSimpleSetComprehension(Match match, out ComputedSet? computedSet, out string error)
        {
            computedSet = null;
            error = string.Empty;
            
            string elementType = match.Groups[1].Value;
            string setName = match.Groups[2].Value;
            string comprehensionBody = match.Groups[3].Value.Trim();
            
            // Parse the comprehension body: expr | iterators : condition
            if (!ParseComprehensionBody(comprehensionBody, out var expression, out var iterators, out var condition, out error))
            {
                return false;
            }
            
            var comprehension = new SetComprehension(elementType, expression, iterators, condition);
            computedSet = new ComputedSet(setName, elementType, comprehension, isIndexed: false);
            
            return true;
        }
        
        private bool ParseIndexedSetComprehension(Match match, out ComputedSet? computedSet, out string error)
        {
            computedSet = null;
            error = string.Empty;
            
            string elementType = match.Groups[1].Value;
            string setName = match.Groups[2].Value;
            string indexVar = match.Groups[3].Value;
            string indexSetName = match.Groups[4].Value;
            string comprehensionBody = match.Groups[5].Value.Trim();
            
            // Validate index set exists
            if (!modelManager.Ranges.ContainsKey(indexSetName) && 
                !modelManager.IndexSets.ContainsKey(indexSetName) &&
                !modelManager.Sets.ContainsKey(indexSetName))
            {
                error = $"Index set '{indexSetName}' not found";
                return false;
            }
            
            // Parse the comprehension body
            if (!ParseComprehensionBody(comprehensionBody, out var expression, out var iterators, out var condition, out error))
            {
                return false;
            }
            
            var comprehension = new SetComprehension(elementType, expression, iterators, condition);
            computedSet = new ComputedSet(setName, elementType, comprehension, isIndexed: true, indexVar, indexSetName);
            
            return true;
        }
        
        private bool ParseComprehensionBody(
            string body, 
            out string expression, 
            out List<SetIterator> iterators,
            out string? condition,
            out string error)
        {
            expression = string.Empty;
            iterators = new List<SetIterator>();
            condition = null;
            error = string.Empty;
            
            // Split by pipe: expr | iterators : condition
            var pipeIndex = body.IndexOf('|');
            if (pipeIndex == -1)
            {
                error = "Invalid set comprehension syntax. Expected: {expr | var in Set}";
                return false;
            }
            
            expression = body.Substring(0, pipeIndex).Trim();
            string rest = body.Substring(pipeIndex + 1).Trim();
            
            // Split by colon to separate iterators from condition
            var colonIndex = rest.IndexOf(':');
            string iteratorsPart;
            
            if (colonIndex != -1)
            {
                iteratorsPart = rest.Substring(0, colonIndex).Trim();
                condition = rest.Substring(colonIndex + 1).Trim();
            }
            else
            {
                iteratorsPart = rest;
            }
            
            // Parse iterators: var1 in Set1, var2 in Set2, ...
            var iteratorStrings = SplitByComma(iteratorsPart);
            
            foreach (var iterStr in iteratorStrings)
            {
                var iterMatch = Regex.Match(iterStr.Trim(), @"([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)");
                if (!iterMatch.Success)
                {
                    error = $"Invalid iterator syntax: '{iterStr}'";
                    return false;
                }
                
                string varName = iterMatch.Groups[1].Value;
                string setName = iterMatch.Groups[2].Value;
                
                iterators.Add(new SetIterator(varName, setName));
            }
            
            if (iterators.Count == 0)
            {
                error = "Set comprehension must have at least one iterator";
                return false;
            }
            
            return true;
        }
        
        private List<string> SplitByComma(string input)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            int depth = 0;
            
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                
                if (c == '(' || c == '<' || c == '[')
                {
                    depth++;
                    current.Append(c);
                }
                else if (c == ')' || c == '>' || c == ']')
                {
                    depth--;
                    current.Append(c);
                }
                else if (c == ',' && depth == 0)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            
            if (current.Length > 0)
            {
                result.Add(current.ToString().Trim());
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// Represents a computed set (result of a set comprehension)
    /// </summary>
    public class ComputedSet
    {
        public string Name { get; set; }
        public string ElementType { get; set; }
        public SetComprehension Comprehension { get; set; }
        public bool IsIndexed { get; set; }
        public string? IndexVariable { get; set; }
        public string? IndexSetName { get; set; }
        
        // Cache for computed values
        private Dictionary<object, object>? cachedSets;
        
        public ComputedSet(
            string name, 
            string elementType, 
            SetComprehension comprehension,
            bool isIndexed = false,
            string? indexVariable = null,
            string? indexSetName = null)
        {
            Name = name;
            ElementType = elementType;
            Comprehension = comprehension;
            IsIndexed = isIndexed;
            IndexVariable = indexVariable;
            IndexSetName = indexSetName;
        }
        
        /// <summary>
        /// Evaluates the set comprehension for a specific index (if indexed)
        /// </summary>
        public object EvaluateForIndex(object indexValue, ModelManager modelManager)
        {
            if (!IsIndexed)
            {
                throw new InvalidOperationException($"Set '{Name}' is not indexed");
            }
            
            // Check cache
            if (cachedSets != null && cachedSets.TryGetValue(indexValue, out var cached))
            {
                return cached;
            }
            
            // Create context with index variable
            var context = new Dictionary<string, object>
            {
                { IndexVariable!, indexValue }
            };
            
            var result = Comprehension.EvaluateSet(modelManager, context);
            
            // Cache result
            if (cachedSets == null)
            {
                cachedSets = new Dictionary<object, object>();
            }
            cachedSets[indexValue] = result;
            
            return result;
        }
        
        /// <summary>
        /// Evaluates the set comprehension (for non-indexed sets)
        /// </summary>
        public object Evaluate(ModelManager modelManager)
        {
            if (IsIndexed)
            {
                throw new InvalidOperationException($"Set '{Name}' is indexed. Use EvaluateForIndex instead");
            }
            
            return Comprehension.EvaluateSet(modelManager);
        }
        
        public void InvalidateCache()
        {
            cachedSets?.Clear();
        }
        
        public override string ToString()
        {
            if (IsIndexed)
            {
                return $"{{{ElementType}}} {Name}[{IndexVariable} in {IndexSetName}] = {Comprehension}";
            }
            else
            {
                return $"{{{ElementType}}} {Name} = {Comprehension}";
            }
        }
    }
}