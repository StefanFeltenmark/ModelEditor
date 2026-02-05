using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Expands sum(...) expressions and distributes coefficients over parentheses
    /// </summary>
    public class SummationExpander
    {
        private readonly ModelManager modelManager;

        public SummationExpander(ModelManager manager)
        {
            modelManager = manager;
        }

        public string ExpandSummations(string expression, out string error)
        {
            error = string.Empty;

            // Check for empty sum bodies first
            var emptySumPattern = @"sum\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\)(?:\s*(?:$|<=|>=|==|<|>|;|\)|,))";
            var emptyMatch = Regex.Match(expression, emptySumPattern);
            
            if (emptyMatch.Success)
            {
                string indexVar = emptyMatch.Groups[1].Value;
                string setName = emptyMatch.Groups[2].Value;
                error = $"Empty sum expression: 'sum({indexVar} in {setName})' has no body. Expected: sum({indexVar} in {setName}) <expression>";
                return expression;
            }

            // Process sums from left to right
            while (true)
            {
                var sumPattern = @"sum\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\)";
                var match = Regex.Match(expression, sumPattern);
                
                if (!match.Success)
                    break;
                
                string iterVar = match.Groups[1].Value;
                string setName = match.Groups[2].Value;
                int sumEndIndex = match.Index + match.Length;
                
                // Extract the body
                string body = ExtractSumBody(expression, sumEndIndex, out int bodyLength);
                
                if (string.IsNullOrWhiteSpace(body))
                {
                    error = $"Empty sum expression: 'sum({iterVar} in {setName})' has no body";
                    return expression;
                }

                // Get indices
                IEnumerable<int> indices;
                if (modelManager.IndexSets.TryGetValue(setName, out var indexSet))
                {
                    indices = indexSet.GetIndices();
                }
                else if (modelManager.Ranges != null && modelManager.Ranges.TryGetValue(setName, out var range))
                {
                    indices = range.GetValues(modelManager);
                }
                else
                {
                    error = $"Set or range '{setName}' not found";
                    return expression;
                }

                // Expand
                var expandedTerms = new List<string>();
                foreach (int index in indices)
                {
                    string expandedTerm = SubstituteIterator(body, iterVar, index);
                    expandedTerms.Add(expandedTerm);
                }

                if (expandedTerms.Count == 0)
                {
                    error = $"Set '{setName}' is empty - cannot expand summation";
                    return expression;
                }

                string expandedSum = string.Join("+", expandedTerms);
                
                // Wrap in parentheses if multiple terms
                if (expandedTerms.Count > 1)
                {
                    expandedSum = $"({expandedSum})";
                }

                // Replace
                int sumStartIndex = match.Index;
                int totalLength = sumEndIndex - sumStartIndex + bodyLength;
                
                expression = expression.Substring(0, sumStartIndex) + 
                            expandedSum + 
                            expression.Substring(sumStartIndex + totalLength);
            }

            return expression;
        }

        /// <summary>
        /// Extracts sum body using proper precedence rules.
        /// 
        /// Rules:
        /// 1. Body = one multiplicative term (or parenthesized expression followed by *, /)
        /// 2. Stops at: top-level +, -, relational operators, semicolon
        /// 3. Continues past ) or ] if followed by *, /, or [
        /// 4. Handles unary +/- at start or after operators
        /// </summary>
        private string ExtractSumBody(string expression, int startIndex, out int length)
        {
            length = 0;
            
            // Skip initial whitespace
            int originalStart = startIndex;
            while (startIndex < expression.Length && char.IsWhiteSpace(expression[startIndex]))
            {
                startIndex++;
                length++;
            }
            
            if (startIndex >= expression.Length)
                return string.Empty;
            
            var body = new StringBuilder();
            int i = startIndex;
            int depth = 0;
            
            while (i < expression.Length)
            {
                char c = expression[i];
                
                // Handle opening brackets/parens
                if (c == '(' || c == '[')
                {
                    depth++;
                    body.Append(c);
                    i++;
                    length++;
                    continue;
                }
                
                // Handle closing brackets/parens
                if (c == ')' || c == ']')
                {
                    depth--;
                    
                    // If closing more than we opened, exit
                    if (depth < 0)
                        break;
                    
                    body.Append(c);
                    i++;
                    length++;
                    
                    // At depth 0, check what follows
                    if (depth == 0)
                    {
                        int j = i;
                        // Skip whitespace
                        while (j < expression.Length && char.IsWhiteSpace(expression[j]))
                            j++;
                        
                        if (j < expression.Length)
                        {
                            char next = expression[j];
                            
                            // Continue if: *, /, [ (indexing)
                            if (next == '*' || next == '/' || next == '[')
                            {
                                // Include whitespace and continue
                                while (i < j)
                                {
                                    body.Append(expression[i]);
                                    i++;
                                    length++;
                                }
                                continue;
                            }
                            
                            // Stop if: relational, semicolon, comma, or additive operators
                            if (IsStoppingCharacter(next))
                            {
                                break;
                            }
                        }
                        else
                        {
                            // End of expression
                            break;
                        }
                    }
                    
                    continue;
                }
                
                // At depth 0, check for stopping conditions
                if (depth == 0)
                {
                    // Whitespace: append and continue
                    if (char.IsWhiteSpace(c))
                    {
                        body.Append(c);
                        i++;
                        length++;
                        continue;
                    }
                    
                    // Relational operators always stop
                    if (c == '<' || c == '>' || c == '=' || c == '!' || c == ';' || c == ',')
                    {
                        break;
                    }
                    
                    // Plus or minus: check if unary or binary
                    if (c == '+' || c == '-')
                    {
                        // If body is empty or ends with operator/opening bracket, it's unary
                        if (IsUnaryPosition(body, startIndex, i, expression))
                        {
                            // Unary: continue
                            body.Append(c);
                            i++;
                            length++;
                            continue;
                        }
                        else
                        {
                            // Binary: stop (end of term)
                            break;
                        }
                    }
                }
                
                // Default: append character
                body.Append(c);
                i++;
                length++;
            }
            
            return body.ToString().Trim();
        }

        /// <summary>
        /// Checks if a + or - at position i is in unary position
        /// </summary>
        private bool IsUnaryPosition(StringBuilder body, int startIndex, int currentPos, string fullExpression)
        {
            // If body is empty, it's unary
            if (body.Length == 0)
                return true;
            
            // Look back at the last non-whitespace character in the body
            string bodyStr = body.ToString();
            int j = bodyStr.Length - 1;
            while (j >= 0 && char.IsWhiteSpace(bodyStr[j]))
                j--;
            
            if (j < 0)
                return true; // Only whitespace before, treat as unary
            
            char lastChar = bodyStr[j];
            
            // If last char is operator or opening bracket, this is unary
            if (lastChar == '*' || lastChar == '/' || lastChar == '(' || lastChar == '[' ||
                lastChar == '+' || lastChar == '-')
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Checks if a character should stop sum body extraction at depth 0
        /// </summary>
        private bool IsStoppingCharacter(char c)
        {
            return c == '+' || c == '-' || c == '<' || c == '>' || 
                   c == '=' || c == '!' || c == ';' || c == ')' || c == ',';
        }

        private string SubstituteIterator(string expression, string iteratorVar, int value)
        {
            // 1. Replace tupleSet[iterator].field
            string pattern = $@"([a-zA-Z][a-zA-Z0-9_]*)\[{Regex.Escape(iteratorVar)}\]\.([a-zA-Z][a-zA-Z0-9_]*)";
            string result = Regex.Replace(expression, pattern, m =>
            {
                return $"{m.Groups[1].Value}[{value}].{m.Groups[2].Value}";
            });

            // 2. Replace indexed variables: var[iterator]
            string varPattern = $@"([a-zA-Z][a-zA-Z0-9_]*)\[{Regex.Escape(iteratorVar)}\]";
            result = Regex.Replace(result, varPattern, m =>
            {
                string varName = m.Groups[1].Value;
                
                if (modelManager.IndexedVariables.ContainsKey(varName))
                {
                    // Variable: no brackets
                    return $"{varName}{value}";
                }
                else
                {
                    // Parameter or tuple: keep brackets
                    return $"{varName}[{value}]";
                }
            });

            // 3. Replace bare iterator
            result = Regex.Replace(result, $@"\b{Regex.Escape(iteratorVar)}\b", value.ToString());

            return result;
        }

        public string ExpandEquationTemplate(string template, string iteratorVar, int value)
        {
            return SubstituteIterator(template, iteratorVar, value);
        }
    }
}