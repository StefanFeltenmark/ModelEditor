using System.Text;
using Core.Models;
using Core.Parsing;
using System.Text.RegularExpressions;

namespace Core
{
    /// <summary>
    /// Main parser that orchestrates the parsing of model files
    /// </summary>
    public class EquationParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;
        private readonly JavaScriptEvaluator jsEvaluator;

        // Specialized parsers
        private readonly ParameterParser parameterParser;
        private readonly IndexSetParser indexSetParser;
        private readonly VariableDeclarationParser variableParser;
        private readonly ExpressionParser expressionParser;

        private readonly SummationExpander summationExpander;
        private readonly ParenthesesExpander parenthesesExpander;
        private readonly VariableValidator variableValidator;
        private readonly DecisionExpressionParser dexprParser;

        // Add field
        private readonly SetComprehensionParser setComprehensionParser;

        // Add field to EquationParser class:
        private readonly MultiDimensionalParser multiDimParser;

        // Add field to EquationParser class
        private readonly DvarParser dvarParser;

        // In constructor, add:
        private readonly MultiDimensionalParameterParser multiDimParamParser;

        public EquationParser(ModelManager manager)
        {
            modelManager = manager;
            evaluator = new ExpressionEvaluator(manager);
            jsEvaluator = new JavaScriptEvaluator(manager);

            // Initialize specialized parsers
            parameterParser = new ParameterParser(manager, evaluator);
            indexSetParser = new IndexSetParser(manager, evaluator);
            variableParser = new VariableDeclarationParser(manager, evaluator);
            dexprParser = new DecisionExpressionParser(manager, evaluator);  // ADD THIS
            expressionParser = new ExpressionParser(manager);
            summationExpander = new SummationExpander(manager);
            parenthesesExpander = new ParenthesesExpander();
            variableValidator = new VariableValidator(manager);
            setComprehensionParser = new SetComprehensionParser(manager);

            // ADD THIS
            multiDimParser = new MultiDimensionalParser(manager);

            // In constructor:
            dvarParser = new DvarParser(manager, evaluator);

            // In constructor, add:
            multiDimParamParser = new MultiDimensionalParameterParser(manager, evaluator);
        }



        public ModelManager GetModelManager()
        {
            return modelManager;
        }

        public ParseSessionResult Parse(string text)
        {
            var result = new ParseSessionResult();

            if (string.IsNullOrWhiteSpace(text))
            {
                result.AddError("No text to parse", 0);
                return result;
            }

            // **Remove block comments FIRST**
            text = RemoveBlockComments(text);

            // Extract and process JavaScript execute blocks
            var (processedText, lineMapping) = ExtractAndProcessExecuteBlocks(text, result);

            // Extract and process tuple schemas
            processedText = ExtractAndProcessTupleSchemas(processedText, result);

            // Extract and process subject to blocks
            processedText = ExtractSubjectToBlocks(processedText, lineMapping, result);

            // Split into statements
            var statements = SplitIntoStatements(processedText, lineMapping);

            // Process each statement
            foreach (var (statement, lineNumber) in statements)
            {
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    ProcessStatement(statement, lineNumber, result);
                }
            }

            // **REMOVED: Don't auto-expand here!**
            // Templates remain as templates until explicitly expanded

            return result;
        }

        /// <summary>
        /// Expands all constraint templates into concrete equations
        /// Call this AFTER external data has been loaded
        /// </summary>
        public void ExpandAllTemplates(ParseSessionResult result)
        {
            // 1. Expand indexed equation templates (simple forall, bracket notation)
            ExpandIndexedEquations(result);
            
            // 2. Expand forall statements (advanced forall with filters)
            ExpandForallStatements(result);
        }

        /// <summary>
        /// Expands all forall statements into concrete equations
        /// </summary>
        private void ExpandForallStatements(ParseSessionResult result)
        {
            if (modelManager.ForallStatements.Count == 0)
                return;

            int beforeCount = modelManager.Equations.Count;

            foreach (var forall in modelManager.ForallStatements)
            {
                try
                {
                    var expandedConstraints = forall.Expand(modelManager);
                    foreach (var constraint in expandedConstraints)
                    {
                        modelManager.AddEquation(constraint);
                        result.IncrementSuccess();
                    }
                }
                catch (Exception ex)
                {
                    result.AddError($"Error expanding forall statement: {ex.Message}", 0);
                }
            }

            int expandedCount = modelManager.Equations.Count - beforeCount;
    
            // Clear the templates after expansion to prevent re-expansion
            modelManager.ForallStatements.Clear();
        }

        private string ExtractAndProcessTupleSchemas(string text, ParseSessionResult result)
        {
            // Pattern: tuple Name { ... }
            string pattern = @"tuple\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\{";
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);

            if (matches.Count == 0)
            {
                return text; // No tuples
            }

            var resultText = new System.Text.StringBuilder();
            int lastIndex = 0;

            foreach (Match match in matches)
            {
                // Append text before this tuple
                resultText.Append(text.Substring(lastIndex, match.Index - lastIndex));

                // Find matching closing brace
                int closingBraceIndex = FindClosingBrace(text, match.Index + match.Length);

                if (closingBraceIndex == -1)
                {
                    result.AddError($"Tuple schema: Missing closing brace '}}' for tuple '{match.Groups[1].Value}'",
                        text.Substring(0, match.Index).Count(c => c == '\n') + 1);
                    continue;
                }

                // Extract the complete tuple definition
                string tupleDefinition = text.Substring(match.Index, closingBraceIndex - match.Index + 1);

                // Parse and register the tuple schema
                if (TryParseTupleSchemaBlock(tupleDefinition, out var schema, out string error))
                {
                    modelManager.AddTupleSchema(schema);
                    result.IncrementSuccess();
                }
                else
                {
                    result.AddError($"Error parsing tuple schema: {error}",
                        text.Substring(0, match.Index).Count(c => c == '\n') + 1);
                }

                lastIndex = closingBraceIndex + 1;
            }

            // Append remaining text
            if (lastIndex < text.Length)
            {
                resultText.Append(text.Substring(lastIndex));
            }

            return resultText.ToString();
        }

        private string ExtractSubjectToBlocks(
            string text,
            Dictionary<int, int> lineMapping,
            ParseSessionResult result)
        {
            // Pattern to match: subject to { ... }
            string pattern = @"subject\s+to\s*\{";
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (matches.Count == 0)
            {
                return text; // No subject to blocks
            }

            var resultText = new System.Text.StringBuilder();
            int lastIndex = 0;

            foreach (Match match in matches)
            {
                // Append text before this block
                resultText.Append(text.Substring(lastIndex, match.Index - lastIndex));

                // Find matching closing brace
                int closingBraceIndex = FindClosingBrace(text, match.Index + match.Length);

                if (closingBraceIndex == -1)
                {
                    result.AddError("Subject to block: Missing closing brace '}'",
                        text.Substring(0, match.Index).Count(c => c == '\n') + 1);
                    lastIndex = match.Index + match.Length;
                    continue;
                }

                // Extract the constraints block content
                int blockStartIndex = match.Index + match.Length;
                string blockContent = text.Substring(blockStartIndex, closingBraceIndex - blockStartIndex);

                // Simply append the block content (constraints will be parsed normally)
                // The "subject to" is just syntactic sugar for grouping - we extract the contents
                resultText.AppendLine();
                resultText.Append(blockContent);
                resultText.AppendLine();

                lastIndex = closingBraceIndex + 1;
            }

            // Append remaining text
            if (lastIndex < text.Length)
            {
                resultText.Append(text.Substring(lastIndex));
            }

            return resultText.ToString();
        }

        private bool TryParseTupleSchemaBlock(string block, out TupleSchema? schema, out string error)
        {
            schema = null;
            error = string.Empty;

            // Extract tuple name
            var nameMatch = Regex.Match(block, @"tuple\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\{", RegexOptions.IgnoreCase);
            if (!nameMatch.Success)
            {
                error = "Invalid tuple syntax";
                return false;
            }

            string tupleName = nameMatch.Groups[1].Value;
            schema = new TupleSchema(tupleName);

            // Extract body (everything between { and })
            int openBrace = block.IndexOf('{');
            int closeBrace = block.LastIndexOf('}');

            if (openBrace == -1 || closeBrace == -1)
            {
                error = "Missing braces in tuple definition";
                return false;
            }

            string body = block.Substring(openBrace + 1, closeBrace - openBrace - 1);

            // Parse field declarations with optional "key" keyword
            var fieldPattern = @"(key\s+)?(float|int|bool|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*;";
            var fieldMatches = Regex.Matches(body, fieldPattern, RegexOptions.IgnoreCase);

            if (fieldMatches.Count == 0)
            {
                error = "Tuple must have at least one field";
                return false;
            }

            foreach (Match fieldMatch in fieldMatches)
            {
                bool isKey = !string.IsNullOrWhiteSpace(fieldMatch.Groups[1].Value);
                string typeStr = fieldMatch.Groups[2].Value.ToLower();
                string fieldName = fieldMatch.Groups[3].Value;

                VariableType fieldType = typeStr switch
                {
                    "float" => VariableType.Float,
                    "int" => VariableType.Integer,
                    "bool" => VariableType.Boolean,
                    "string" => VariableType.String,
                    _ => VariableType.Float
                };

                try
                {
                    schema.AddField(fieldName, fieldType, isKey);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            return true;
        }

        private List<(string content, int lineNumber)> SplitIntoStatements(
            string text,
            Dictionary<int, int> lineMapping)
        {
            // Split by lines first to handle comments properly
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var processedLines = new List<(string content, int lineNumber)>();

            int currentLineNumber = 1;
            foreach (string line in lines)
            {
                string lineWithoutComment = line.Split(new[] { "//" }, StringSplitOptions.None)[0].Trim();
                if (!string.IsNullOrWhiteSpace(lineWithoutComment))
                {
                    // Map to original line number
                    int originalLineNumber = lineMapping.ContainsKey(currentLineNumber)
                        ? lineMapping[currentLineNumber]
                        : currentLineNumber;
                    processedLines.Add((lineWithoutComment, originalLineNumber));
                }

                currentLineNumber++;
            }

            // Group lines by statements (split by semicolons)
            var statements = new List<(string content, int lineNumber)>();
            string currentStatement = "";
            int statementStartLine = 0;

            foreach (var (content, lineNumber) in processedLines)
            {
                if (string.IsNullOrEmpty(currentStatement))
                {
                    statementStartLine = lineNumber;
                }

                currentStatement += " " + content;

                if (content.Contains(';'))
                {
                    var parts = currentStatement.Split(';',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var part in parts)
                    {
                        if (!string.IsNullOrWhiteSpace(part))
                        {
                            statements.Add((part.Trim(), statementStartLine));
                        }
                    }

                    currentStatement = "";
                }
            }

            // Process any remaining statement without semicolon
            if (!string.IsNullOrWhiteSpace(currentStatement))
            {
                statements.Add((currentStatement.Trim(), statementStartLine));
            }

            return statements;
        }

        private (string processedText, Dictionary<int, int> lineMapping) ExtractAndProcessExecuteBlocks(
            string text,
            ParseSessionResult result)
        {
            var lineMapping = new Dictionary<int, int>();

            string pattern = @"execute\s*\{";
            var matches = Regex.Matches(text, pattern, RegexOptions.Singleline);

            if (matches.Count == 0)
            {
                // No execute blocks, return original text with identity mapping
                var lines = text.Split(new[] { '\r', '\n' });
                for (int i = 0; i < lines.Length; i++)
                {
                    lineMapping[i + 1] = i + 1;
                }

                return (text, lineMapping);
            }

            var resultText = new System.Text.StringBuilder();
            int lastIndex = 0;
            int blockNumber = 0;
            int currentOutputLine = 1;
            int currentInputLine = 1;

            foreach (Match match in matches)
            {
                blockNumber++;

                // Find the matching closing brace
                int closingBraceIndex = FindClosingBrace(text, match.Index + match.Length);

                if (closingBraceIndex == -1)
                {
                    result.AddError($"Execute block {blockNumber}: Missing closing brace '}}' ",
                        text.Substring(0, match.Index).Count(c => c == '\n') + 1);
                    continue;
                }

                // Extract and process JavaScript code
                int jsStartIndex = match.Index + match.Length;
                string jsCode = text.Substring(jsStartIndex, closingBraceIndex - jsStartIndex).Trim();
                int blockStartLine = text.Substring(0, match.Index).Count(c => c == '\n') + 1;

                // Append text before this execute block
                string beforeBlock = text.Substring(lastIndex, match.Index - lastIndex);
                resultText.Append(beforeBlock);

                // Update line mapping
                UpdateLineMapping(lineMapping, beforeBlock, ref currentOutputLine, ref currentInputLine);

                // Process the execute block
                ProcessExecuteBlock(jsCode, blockNumber, blockStartLine, result);

                // Skip the input lines consumed by the execute block
                int blockEndLine = text.Substring(0, closingBraceIndex + 1).Count(c => c == '\n') + 1;
                currentInputLine = blockEndLine;

                lastIndex = closingBraceIndex + 1;
            }

            // Append remaining text
            if (lastIndex < text.Length)
            {
                string afterBlock = text.Substring(lastIndex);
                resultText.Append(afterBlock);
                UpdateLineMapping(lineMapping, afterBlock, ref currentOutputLine, ref currentInputLine);
            }

            return (resultText.ToString(), lineMapping);
        }

        private int FindClosingBrace(string text, int startIndex)
        {
            int openBraceCount = 1;
            int searchIndex = startIndex;

            while (searchIndex < text.Length && openBraceCount > 0)
            {
                if (text[searchIndex] == '{')
                    openBraceCount++;
                else if (text[searchIndex] == '}')
                {
                    openBraceCount--;
                    if (openBraceCount == 0)
                    {
                        return searchIndex;
                    }
                }

                searchIndex++;
            }

            return -1;
        }

        private void UpdateLineMapping(
            Dictionary<int, int> lineMapping,
            string text,
            ref int currentOutputLine,
            ref int currentInputLine)
        {
            var lines = text.Split(new[] { '\r', '\n' });
            foreach (var line in lines)
            {
                lineMapping[currentOutputLine] = currentInputLine;
                currentOutputLine++;
                currentInputLine++;
            }
        }

        private void ProcessExecuteBlock(string jsCode, int blockNumber, int blockStartLine, ParseSessionResult result)
        {
            if (string.IsNullOrWhiteSpace(jsCode))
            {
                result.AddError($"Execute block {blockNumber}: JavaScript code is empty", blockStartLine);
                return;
            }

            var executeResult = jsEvaluator.ExecuteCodeBlock(jsCode);

            if (!executeResult.IsSuccess)
            {
                result.AddError($"Execute block {blockNumber}: {executeResult.ErrorMessage}", blockStartLine);
                return;
            }

            // Add results as parameters
            foreach (var kvp in executeResult.Value)
            {
                string name = kvp.Key;
                object value = kvp.Value;

                Parameter? param = CreateParameterFromValue(name, value);
                if (param != null)
                {
                    modelManager.AddParameter(param);
                    result.IncrementSuccess();
                }
            }
        }

        private Parameter? CreateParameterFromValue(string name, object value)
        {
            return value switch
            {
                double d => new Parameter(name, ParameterType.Float, d),
                float f => new Parameter(name, ParameterType.Float, Convert.ToDouble(f)),
                int i => new Parameter(name, ParameterType.Integer, i),
                long l => new Parameter(name, ParameterType.Integer, Convert.ToInt32(l)),
                string s => new Parameter(name, ParameterType.String, s),
                List<object> list => new Parameter(name, ParameterType.String, $"[{string.Join(", ", list)}]"),
                _ => null
            };
        }



        private void ProcessStatement(string statement, int lineNumber, ParseSessionResult result)
        {
            string error = string.Empty;

            // Try multi-dimensional indexed parameter FIRST
            if (multiDimParser.TryParseIndexedParameter(statement, out var multiDimParam, out error))
            {
                if (multiDimParam != null)
                {
                    modelManager.AddParameter(multiDimParam);  // Use regular AddParameter!
                    result.IncrementSuccess();
                    return;
                }
                // ... handle error
            }
            
            // Remove all references to AddIndexedParameter and AddIndexedSetCollection
    
            // Continue with regular parameter parsing, etc.
            // 0.5. Multi-dimensional parameters (before regular parameters)
            if (multiDimParamParser.TryParse(statement, out var multiDimParam1, out error))
            {
                if (multiDimParam1 != null)
                {
                    modelManager.AddParameter(multiDimParam1);
                    result.IncrementSuccess();
                    return;
                }
                else
                {
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            error = string.Empty;

            // 1. Parameters
            if (parameterParser.TryParse(statement, out Parameter param, out error))
            {
                if (param != null)
                {
                    modelManager.AddParameter(param);
                    result.IncrementSuccess();
                    return;
                }
                else
                {
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            error = string.Empty;

            // 2. Index sets
            if (indexSetParser.TryParse(statement, out var indexSet, out error))
            {
                if (indexSet != null)
                {
                    modelManager.AddIndexSet(indexSet);
                    result.IncrementSuccess();
                    return;
                }
                else
                {
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            error = string.Empty;

            // 2.5. Dvar declarations (before regular var)
            if (dvarParser.TryParse(statement, out var dvar, out error))
            {
                if (dvar != null)
                {
                    modelManager.AddIndexedVariable(dvar);
                    result.IncrementSuccess();
                    return;
                }
                else
                {
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            error = string.Empty;

            // 3. Variable declarations
            if (variableParser.TryParse(statement, out var variable, out error))
            {
                if (variable != null)
                {
                    modelManager.AddIndexedVariable(variable);
                    result.IncrementSuccess();
                    return;
                }
                else
                {
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            error = string.Empty;

            // 4. Primitive sets
            if (TryParsePrimitiveSet(statement, out var primitiveSet, out error))
            {
                if (primitiveSet != null)
                {
                    modelManager.AddPrimitiveSet(primitiveSet);
                    result.IncrementSuccess();
                    return;
                }
                else
                {
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            error = string.Empty;

            // 5. Tuple sets (before set comprehensions)
            if (TryParseTupleSet(statement, out var tupleSet, out error))
            {
                if (tupleSet != null)
                {
                    modelManager.AddTupleSet(tupleSet);
                    result.IncrementSuccess();
                    return;
                }
                else
                {
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            error = string.Empty;

            // 6. Set comprehensions (after tuple sets)
            if (setComprehensionParser != null && 
                setComprehensionParser.TryParse(statement, out var computedSet, out error))
            {
                if (computedSet != null)
                {
                    modelManager.AddComputedSet(computedSet);
                    result.IncrementSuccess();
                    return;
                }
                else
                {
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            error = string.Empty;

            // 7. DECISION EXPRESSIONS (BEFORE equations!)
            if (dexprParser != null && dexprParser.TryParse(statement, out var dexpr, out error))
            {
                if (dexpr != null)
                {
                    modelManager.AddDecisionExpression(dexpr);
                    result.IncrementSuccess();
                    return;
                }
                else
                {
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            error = string.Empty;

            // 8. Indexed equations
            if (TryParseIndexedEquation(statement, lineNumber, out error, result))
            {
                result.IncrementSuccess();
                return;
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            error = string.Empty;

            // 9. Regular equations
            if (TryParseEquation(statement, out var equation, out error))
            {
                if (equation != null)
                {
                    try
                    {
                        modelManager.AddEquation(equation);
                        result.IncrementSuccess();
                        return;
                    }
                    catch (Exception ex)
                    {
                        result.AddError($"Error adding equation - {ex.Message}", lineNumber);
                        return;
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            error = string.Empty;

            // 10. Objective function
            if (TryParseObjective(statement, out var objective, out error))
            {
                if (objective != null)
                {
                    modelManager.SetObjective(objective);
                    result.IncrementSuccess();
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(error) && !IsNotRecognizedError(error))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }

            // Nothing matched
            result.AddError($"\"{statement}\"\n  Error: Unknown statement type", lineNumber);
        }

        private bool IsNotRecognizedError(string error)
        {
            if (string.IsNullOrEmpty(error))
                return true;
            
            return error.Contains("Not a parameter", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("Not an index set", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("Not a variable", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("Not a dvar declaration", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("Not a tuple", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("Not a primitive set", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("Not a set comprehension", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("Not a decision expression", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("Not an indexed equation declaration", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("Not an indexed parameter", StringComparison.OrdinalIgnoreCase) ||  
                   error.Contains("Not an indexed set", StringComparison.OrdinalIgnoreCase) ||        
                   error.Contains("Not an external multi-dimensional", StringComparison.OrdinalIgnoreCase) || 
                   error.Contains("Not a multi-dimensional parameter", StringComparison.OrdinalIgnoreCase) || 
                   error.Contains("Not an equation", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("Not an objective", StringComparison.OrdinalIgnoreCase);
        }

        // Add this method to parse tuple sets
        private bool TryParseTupleSet(string statement, out TupleSet? tupleSet, out string error)
        {
            tupleSet = null;
            error = string.Empty;

            // Pattern 1: Indexed tuple set: {SchemaName} setName[indexSet] = ...;
            string indexedPattern = @"^\s*\{([a-zA-Z][a-zA-Z0-9_]*)\}\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[([a-zA-Z][a-zA-Z0-9_]*)\]\s*=\s*(.+)$";
            var indexedMatch = Regex.Match(statement.Trim(), indexedPattern);

            if (indexedMatch.Success)
            {
                string schemaName = indexedMatch.Groups[1].Value;
                string setName = indexedMatch.Groups[2].Value;
                string indexSetName = indexedMatch.Groups[3].Value;
                string value = indexedMatch.Groups[4].Value.Trim();

                // Validate schema exists
                if (!modelManager.TupleSchemas.ContainsKey(schemaName))
                {
                    error = $"Tuple schema '{schemaName}' is not defined";
                    return false;
                }

                // Validate index set exists
                if (!modelManager.IndexSets.ContainsKey(indexSetName))
                {
                    error = $"Index set '{indexSetName}' is not defined";
                    return false;
                }

                bool isExternal = value == "...";
                tupleSet = new TupleSet(setName, schemaName, indexSetName, isExternal);

                if (!isExternal)
                {
                    // Parse inline tuple data if provided
                    if (!ParseInlineTupleData(value, modelManager.TupleSchemas[schemaName], tupleSet, out error))
                    {
                        return false;
                    }
                }

                return true;
            }

            // Pattern 2: Non-indexed tuple set: {SchemaName} setName = ...;
            string pattern = @"^\s*\{([a-zA-Z][a-zA-Z0-9_]*)\}\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not a tuple set declaration";
                return false;
            }

            string schemaName2 = match.Groups[1].Value;
            string setName2 = match.Groups[2].Value;
            string value2 = match.Groups[3].Value.Trim();

            // CRITICAL: Check if this is actually a set comprehension
            // Set comprehensions have a pipe character: {a | a in Set: condition}
            if (value2.Contains('|'))
            {
                error = "Not a tuple set declaration"; // It's a set comprehension
                return false;
            }

            if (!modelManager.TupleSchemas.ContainsKey(schemaName2))
            {
                error = $"Tuple schema '{schemaName2}' is not defined";
                return false;
            }

            var schema2 = modelManager.TupleSchemas[schemaName2];
            bool isExternal2 = value2 == "...";
            tupleSet = new TupleSet(setName2, schemaName2, isExternal2);  // No index set

            // Parse inline tuple data if provided
            if (!isExternal2)
            {
                if (!value2.StartsWith("{") || !value2.EndsWith("}"))
                {
                    error = "Tuple set data must be enclosed in braces: {<...>, <...>}";

                    return false;
                }

                string tupleData = value2.Substring(1, value2.Length - 2).Trim();

                if (!string.IsNullOrEmpty(tupleData))
                {
                    if (!ParseInlineTupleData(tupleData, schema2, tupleSet, out error))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool TryParsePrimitiveSet(string statement, out PrimitiveSet? primitiveSet, out string error)
        {
            primitiveSet = null;
            error = string.Empty;

            // Pattern: {int|string|float} setName = {values} or ...;
            string pattern = @"^\s*\{(int|string|float)\}\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                error = "Not a primitive set declaration";
                return false;
            }

            string typeStr = match.Groups[1].Value.ToLower();
            string setName = match.Groups[2].Value;
            string valueStr = match.Groups[3].Value.Trim();

            PrimitiveSetType setType = typeStr switch
            {
                "int" => PrimitiveSetType.Int,
                "string" => PrimitiveSetType.String,
                "float" => PrimitiveSetType.Float,
                _ => PrimitiveSetType.Int
            };

            bool isExternal = valueStr == "...";
            primitiveSet = new PrimitiveSet(setName, setType, isExternal);

            // Parse inline data if provided
            if (!isExternal)
            {
                if (!valueStr.StartsWith("{") || !valueStr.EndsWith("}"))
                {
                    error = "Primitive set data must be enclosed in braces: {value1, value2, ...}";
                    return false;
                }

                string data = valueStr.Substring(1, valueStr.Length - 2).Trim();

                if (!string.IsNullOrEmpty(data))
                {
                    if (!ParsePrimitiveSetData(data, primitiveSet, out error))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool ParsePrimitiveSetData(string data, PrimitiveSet primitiveSet, out string error)
        {
            error = string.Empty;

            // Split by commas, respecting quotes for strings
            var values = SplitByCommaRespectingQuotes(data);

            foreach (var valueStr in values)
            {
                string trimmed = valueStr.Trim();

                if (string.IsNullOrEmpty(trimmed))
                    continue;

                try
                {
                    switch (primitiveSet.Type)
                    {
                        case PrimitiveSetType.Int:
                            if (int.TryParse(trimmed, out int intVal))
                            {
                                primitiveSet.Add(intVal);
                            }
                            else
                            {
                                error = $"Invalid integer value: '{trimmed}'";
                                return false;
                            }

                            break;

                        case PrimitiveSetType.String:
                            string strVal = trimmed.Trim('"');
                            primitiveSet.Add(strVal);
                            break;

                        case PrimitiveSetType.Float:
                            if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out double floatVal))
                            {
                                primitiveSet.Add(floatVal);
                            }
                            else
                            {
                                error = $"Invalid float value: '{trimmed}'";
                                return false;
                            }

                            break;
                    }
                }
                catch (Exception ex)
                {
                    error = $"Error parsing value '{trimmed}': {ex.Message}";
                    return false;
                }
            }

            return true;
        }

        private List<string> SplitByCommaRespectingQuotes(string input)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }

            return result;
        }

        private bool ParseInlineTupleData(string tupleData, TupleSchema schema, TupleSet tupleSet, out string error)
        {
            error = string.Empty;

            // Pattern: <value1, value2, ...>
            var tuplePattern = @"<([^>]+)>";
            var tupleMatches = Regex.Matches(tupleData, tuplePattern);

            if (tupleMatches.Count == 0)
            {
                error = "No valid tuple data found. Use angle bracket notation: <value1, value2, ...>";
                return false;
            }

            foreach (Match tupleMatch in tupleMatches)
            {
                string instanceData = tupleMatch.Groups[1].Value;
                var values = SplitTupleValues(instanceData);

                if (values.Count != schema.Fields.Count)
                {
                    error =
                        $"Tuple has {values.Count} values but schema '{schema.Name}' requires {schema.Fields.Count} fields";
                    return false;
                }

                var instance = new TupleInstance(schema.Name);
                int fieldIndex = 0;

                foreach (var field in schema.Fields)
                {
                    string fieldName = field.Key;
                    VariableType fieldType = field.Value;
                    string valueStr = values[fieldIndex++];

                    object parsedValue = ParseTupleFieldValue(valueStr, fieldType, out string parseError);

                    if (!string.IsNullOrEmpty(parseError))
                    {
                        error = $"Error parsing field '{fieldName}': {parseError}";
                        return false;
                    }

                    instance.SetValue(fieldName, parsedValue);
                }

                tupleSet.AddInstance(instance);
            }

            return true;
        }

        private List<string> SplitTupleValues(string input)
        {
            var values = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            int depth = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                }
                else if (!inQuotes)
                {
                    if (c == '<' || c == '(')
                    {
                        depth++;
                        current.Append(c);
                    }
                    else if (c == '>' || c == ')')
                    {
                        depth--;
                        current.Append(c);
                    }
                    else if (c == ',' && depth == 0)
                    {
                        values.Add(current.ToString().Trim());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                values.Add(current.ToString().Trim());
            }

            return values;
        }

        private object ParseTupleFieldValue(string valueStr, VariableType type, out string error)
        {
            error = string.Empty;
            valueStr = valueStr.Trim();

            try
            {
                return type switch
                {
                    VariableType.String => valueStr.Trim('"'),
                    VariableType.Integer => int.Parse(valueStr),
                    VariableType.Float => double.Parse(valueStr, System.Globalization.CultureInfo.InvariantCulture),
                    VariableType.Boolean => bool.Parse(valueStr),
                    _ => throw new InvalidOperationException($"Unknown type: {type}")
                };
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null!;
            }
        }

        private bool TryParseIndexedEquation(string statement, int lineNumber, out string error,
            ParseSessionResult result)
        {
            error = string.Empty;


            // OPL-style forall: forall(i in 1..n) x[i] <= capacity[i];
            // Example: forall(i in 1..n, j in 1..m: i != j) flow[i][j] <= cap[i][j];
            string forallTwoDimPattern =
                @"^\s*forall\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\)\s*(?:([a-zA-Z][a-zA-Z0-9_]*)\s*:\s*)?(.+)$";
            var forallTwoDimMatch = Regex.Match(statement.Trim(), forallTwoDimPattern,
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (forallTwoDimMatch.Success)
            {
                string indexVar1 = forallTwoDimMatch.Groups[1].Value;
                string indexSetName1 = forallTwoDimMatch.Groups[2].Value;
                string indexVar2 = forallTwoDimMatch.Groups[3].Value;
                string indexSetName2 = forallTwoDimMatch.Groups[4].Value;
                string baseName = forallTwoDimMatch.Groups[5].Value;
                string template = forallTwoDimMatch.Groups[6].Value.Trim();

                if (string.IsNullOrEmpty(baseName))
                {
                    baseName = $"constraint_{modelManager.IndexedEquationTemplates.Count + 1}";
                }

                if (!modelManager.IndexSets.ContainsKey(indexSetName1))
                {
                    error = $"First index set '{indexSetName1}' is not declared";
                    return false;
                }

                if (!modelManager.IndexSets.ContainsKey(indexSetName2))
                {
                    error = $"Second index set '{indexSetName2}' is not declared";
                    return false;
                }

                var indexedEquation = new IndexedEquation(baseName, indexSetName1, template, indexSetName2);
                modelManager.AddIndexedEquationTemplate(indexedEquation);
                return true;
            }

            // Pattern: forall(iterators) [label:] constraint
            // With filter support: forall(i in Set: filter, j in Set2: filter2)
            string forallPattern = 
                @"^\s*forall\s*\(([^)]+)\)\s*(?:([a-zA-Z][a-zA-Z0-9_]*(?:\[[^\]]+\])*)\s*:\s*)?(.+)$";
            var forallMatch = Regex.Match(statement.Trim(), forallPattern, 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (forallMatch.Success)
            {
                string iteratorsPart = forallMatch.Groups[1].Value;
                string labelPart = forallMatch.Groups[2].Value.Trim();
                string constraintPart = forallMatch.Groups[3].Value.Trim();

                return ParseForallWithFilters(iteratorsPart, labelPart, constraintPart, out error);
            }
            

            // Original bracket notation: constraint[i in I, j in J]: ...
            string twoDimPattern =
                @"^\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\]\s*:\s*(.+)$";
            var twoDimMatch = Regex.Match(statement.Trim(), twoDimPattern);

            if (twoDimMatch.Success)
            {
                return ProcessTwoDimensionalIndexedEquation(twoDimMatch, out error);
            }

            // Original bracket notation: constraint[i in I]: ...
            string pattern =
                @"^\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\]\s*:\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not an indexed equation declaration";
                return false;
            }

            return ProcessSingleDimensionalIndexedEquation(match, out error);
        }

        private bool ProcessTwoDimensionalIndexedEquation(Match match, out string error)
        {
            error = string.Empty;

            string baseName = match.Groups[1].Value;
            string indexVar1 = match.Groups[2].Value;
            string indexSetName1 = match.Groups[3].Value;
            string indexVar2 = match.Groups[4].Value;
            string indexSetName2 = match.Groups[5].Value;
            string template = match.Groups[6].Value;

            if (!modelManager.IndexSets.ContainsKey(indexSetName1))
            {
                error = $"First index set '{indexSetName1}' is not declared";
                return false;
            }

            if (!modelManager.IndexSets.ContainsKey(indexSetName2))
            {
                error = $"Second index set '{indexSetName2}' is not declared";
                return false;
            }

            var indexedEquation = new IndexedEquation(baseName, indexSetName1, template, indexSetName2);
            modelManager.AddIndexedEquationTemplate(indexedEquation);

            return true;
        }

        private bool ProcessSingleDimensionalIndexedEquation(Match match, out string error)
        {
            error = string.Empty;

            string baseName = match.Groups[1].Value;
            string indexVar = match.Groups[2].Value;
            string indexSetName = match.Groups[3].Value;
            string template = match.Groups[4].Value;

            if (!modelManager.IndexSets.ContainsKey(indexSetName))
            {
                error = $"Index set '{indexSetName}' is not declared";
                return false;
            }

            var indexedEquation = new IndexedEquation(baseName, indexSetName, template);
            modelManager.AddIndexedEquationTemplate(indexedEquation);

            return true;
        }

        public void ExpandIndexedEquations(ParseSessionResult result)
        {
            foreach (var indexedEquation in modelManager.IndexedEquationTemplates.Values)
            {
                if (indexedEquation.IsTwoDimensional)
                {
                    ExpandTwoDimensionalEquation(indexedEquation, result);
                }
                else
                {
                    ExpandSingleDimensionalEquation(indexedEquation, result);
                }
            }
        }

        private void ExpandTwoDimensionalEquation(IndexedEquation indexedEquation, ParseSessionResult result)
        {
            var indexSet1 = modelManager.IndexSets[indexedEquation.IndexSetName];
            var indexSet2 = modelManager.IndexSets[indexedEquation.SecondIndexSetName!];

            // Extract iterator variables from the template
            string indexVar1 = ExtractIteratorVariable(indexedEquation.Template, 0);
            string indexVar2 = ExtractIteratorVariable(indexedEquation.Template, 1);

            foreach (int index1 in indexSet1.GetIndices())
            {
                foreach (int index2 in indexSet2.GetIndices())
                {
                    // Use the local method instead of summationExpander
                    string expandedEquation = SubstituteIteratorInTemplate(
                        indexedEquation.Template, indexVar1, index1);
                    expandedEquation = SubstituteIteratorInTemplate(
                        expandedEquation, indexVar2, index2);

                    if (TryParseEquation(expandedEquation, out var eq, out var eqError))
                    {
                        if (eq != null)
                        {
                            eq.BaseName = indexedEquation.BaseName;
                            eq.Index = index1;
                            eq.SecondIndex = index2;
                            modelManager.AddEquation(eq);
                            result.IncrementSuccess();
                        }
                    }
                    else
                    {
                        result.AddError(
                            $"Error expanding equation '{indexedEquation.BaseName}[{index1},{index2}]': {eqError}",
                            0);
                    }
                }
            }
        }

        
        
        
        private bool ParseForallWithFilters(string iteratorsPart, string label, string constraintPart, 
    out string error)
{
    error = string.Empty;

    try
    {
        var forall = new ForallStatement();

        forall.Label = string.IsNullOrWhiteSpace(label) ? null : label;

        // Parse iterators with optional filters
        // Format: "i in Set: filter, j in Set2: filter2, k in Set3"
        var iterators = ParseForallIteratorsWithFilters(iteratorsPart, out error);
        if (iterators == null)
            return false;

        forall.Iterators = iterators;

        // Parse constraint template
        var template = ParseConstraintTemplate(constraintPart, out error);
        if (template == null)
            return false;

        forall.ConstraintTemplate = template;

        // Store for later expansion
        modelManager.AddForallStatement(forall);
        
        return true;
    }
    catch (Exception ex)
    {
        error = $"Error parsing forall: {ex.Message}";
        return false;
    }
}

private List<ForallIterator>? ParseForallIteratorsWithFilters(string iteratorsPart, out string error)
{
    error = string.Empty;
    var iterators = new List<ForallIterator>();

    // Split by comma at top level
    var parts = SplitByCommaTopLevel(iteratorsPart);

    foreach (var part in parts)
    {
        string trimmed = part.Trim();

        // Check for filter: "varName in SetName: filter"
        int colonIndex = FindTopLevelChar(trimmed, ':');

        if (colonIndex > 0)
        {
            // Has filter
            string iteratorDecl = trimmed.Substring(0, colonIndex).Trim();
            string filterExpr = trimmed.Substring(colonIndex + 1).Trim();

            // Parse iterator declaration
            var match = Regex.Match(iteratorDecl, @"^([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)$");
            if (!match.Success)
            {
                error = $"Invalid iterator syntax: '{iteratorDecl}'";
                return null;
            }

            string varName = match.Groups[1].Value;
            string setName = match.Groups[2].Value;

            // Parse filter expression
            var filterExpression = ParseExpression(filterExpr);

            var iterator = new ForallIterator(varName, setName, filterExpression);
            iterators.Add(iterator);
        }
        else
        {
            // No filter
            var match = Regex.Match(trimmed, @"^([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)$");
            if (!match.Success)
            {
                error = $"Invalid iterator syntax: '{trimmed}'";
                return null;
            }

            string varName = match.Groups[1].Value;
            string setName = match.Groups[2].Value;

            var iterator = new ForallIterator(varName, setName);
            iterators.Add(iterator);
        }
    }

    return iterators;
}

private ConstraintTemplate? ParseConstraintTemplate(string constraintPart, out string error)
{
    error = string.Empty;

    // Remove trailing semicolon
    constraintPart = constraintPart.TrimEnd(';').Trim();

    // Find relational operator
    var relOps = new[] { "<=", ">=", "==", "<", ">" };
    string? foundOp = null;
    int opIndex = -1;

    foreach (var op in relOps)
    {
        opIndex = FindOperatorAtTopLevel(constraintPart, op);
        if (opIndex >= 0)
        {
            foundOp = op;
            break;
        }
    }

    if (foundOp == null)
    {
        error = "No relational operator found in constraint";
        return null;
    }

    string leftPart = constraintPart.Substring(0, opIndex).Trim();
    string rightPart = constraintPart.Substring(opIndex + foundOp.Length).Trim();

    var template = new ConstraintTemplate
    {
        LeftSide = ParseExpression(leftPart),
        Operator = ParseRelationalOperator(foundOp),
        RightSide = ParseExpression(rightPart)
    };

    return template;
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
        private void ExpandSingleDimensionalEquation(IndexedEquation indexedEquation, ParseSessionResult result)
        {
            var indexSet = modelManager.IndexSets[indexedEquation.IndexSetName];
            
            // Extract the iterator variable ONCE before the loop
            // For single-dimensional, we always want the first (and only) iterator at position 0
            string indexVar = ExtractIteratorVariable(indexedEquation.Template, 0);

            foreach (int index in indexSet.GetIndices())
            {
                // Substitute iterator in template
                string expandedEquation = SubstituteIteratorInTemplate(
                    indexedEquation.Template, 
                    indexVar,   // Use the extracted iterator variable name
                    index);     // Use the actual index value (1, 2, 3...)

                if (TryParseEquation(expandedEquation, out var eq, out var eqError))
                {
                    if (eq != null)
                    {
                        eq.BaseName = indexedEquation.BaseName;
                        eq.Index = index;
                        modelManager.AddEquation(eq);
                        result.IncrementSuccess();
                    }
                }
                else
                {
                    result.AddError($"Error expanding equation '{indexedEquation.BaseName}[{index}]': {eqError}", 0);
                }
            }
        }

        private string SubstituteIteratorInTemplate(string template, string iteratorVar, int value)
        {
            // Replace tupleSet[iterator].field with tupleSet[value].field
            string pattern = $@"([a-zA-Z][a-zA-Z0-9_]*)\[{Regex.Escape(iteratorVar)}\]\.([a-zA-Z][a-zA-Z0-9_]*)";
    
            string result = Regex.Replace(template, pattern, m =>
            {
                string setName = m.Groups[1].Value;
                string fieldName = m.Groups[2].Value;
                return $"{setName}[{value}].{fieldName}";
            });

            // NEW: Replace indexed VARIABLES: var[iterator] -> var{value} (no brackets!)
            string varPattern = $@"([a-zA-Z][a-zA-Z0-9_]*)\[{Regex.Escape(iteratorVar)}\]";
            result = Regex.Replace(result, varPattern, m =>
            {
                string varName = m.Groups[1].Value;
        
                // Check if it's an indexed variable (not parameter)
                if (modelManager.IndexedVariables.ContainsKey(varName))
                {
                    // Use variable naming convention: x1, x2, etc. (no brackets)
                    return $"{varName}{value}";
                }
                else
                {
                    // It's a parameter or tuple set - keep brackets
                    return $"{varName}[{value}]";
                }
            });

    // Replace simple iterator variable references
    result = Regex.Replace(result, $@"\b{Regex.Escape(iteratorVar)}\b", value.ToString());

    return result;
}

        /// <summary>
///
///   "objective: 2*x + 3*y1 == 100"
///   "constraint: x + y1 >= 5"
///   "x + y == 10"
/// </summary>
        public bool TryParseEquation(string equation, out LinearEquation? result, out string error)
        {
            result = null;
            error = string.Empty;

            equation = equation.Trim();

            // STEP 1: Early rejection of non-equation statements
            if (equation.StartsWith("dexpr", StringComparison.OrdinalIgnoreCase) ||
                equation.StartsWith("tuple", StringComparison.OrdinalIgnoreCase) ||
                equation.StartsWith("range", StringComparison.OrdinalIgnoreCase) ||
                equation.StartsWith("var ", StringComparison.OrdinalIgnoreCase) ||
                equation.StartsWith("dvar ", StringComparison.OrdinalIgnoreCase) ||
                equation.StartsWith("int ", StringComparison.OrdinalIgnoreCase) ||
                equation.StartsWith("float ", StringComparison.OrdinalIgnoreCase) ||
                equation.StartsWith("string ", StringComparison.OrdinalIgnoreCase) ||
                equation.StartsWith("bool ", StringComparison.OrdinalIgnoreCase) ||
                equation.StartsWith("minimize", StringComparison.OrdinalIgnoreCase) ||  // ADD THIS
                equation.StartsWith("maximize", StringComparison.OrdinalIgnoreCase))    // ADD THIS
            {
                error = "Not an equation";
                return false;
            }

            // STEP 2: Reject indexed equation pattern (those are handled by TryParseIndexedEquation)
            // Pattern: label[var in Set]: ...
            if (Regex.IsMatch(equation, @"^[a-zA-Z][a-zA-Z0-9_]*\s*\[\s*[a-zA-Z][a-zA-Z0-9_]*\s+in\s+"))
            {
                error = "Not an equation"; // This is an indexed equation
                return false;
            }

            try
            {
                string equationText = equation;
                string? label = null;

                // STEP 3: Extract label if present (e.g., "objective: ...")
                int colonIndex = equationText.IndexOf(':');
                if (colonIndex > 0)
                {
                    // Make sure the colon is not inside brackets or parentheses
                    if (!IsInsideBracketsOrParens(equationText, colonIndex))
                    {
                        string potentialLabel = equationText.Substring(0, colonIndex).Trim();

                        // Validate: label should be a simple identifier (no spaces, brackets, etc.)
                        if (Regex.IsMatch(potentialLabel, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
                        {
                            label = potentialLabel;
                            equationText = equationText.Substring(colonIndex + 1).Trim();
                        }
                    }
                }

                // STEP 4: Find and split by relational operator
                if (!SplitByOperator(equationText, out RelationalOperator op, out string[] parts, out error))
                {
                    return false;
                }

                if (parts.Length != 2)
                {
                    error = "Equation must have exactly two sides separated by a relational operator (==, <=, >=, <, >)";
                    return false;
                }

                string leftSide = parts[0].Trim();
                string rightSide = parts[1].Trim();

                if (string.IsNullOrWhiteSpace(leftSide))
                {
                    error = "Left side of equation is empty";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(rightSide))
                {
                    error = "Right side of equation is empty";
                    return false;
                }

                // NORMALIZE: Remove spaces around operators
                // This fixes "x - 2*y" being parsed incorrectly as "x" and "2*y" instead of "x" and "-2*y"
                leftSide = Regex.Replace(leftSide, @"\s*([+\-*/])\s*", "$1");
                rightSide = Regex.Replace(rightSide, @"\s*([+\-*/])\s*", "$1");

                // STEP 4.5: Validate no implicit multiplication
                if (!ValidateNoImplicitMultiplication(leftSide, out string validationError))
                {
                    error = $"Error in left side: {validationError}";
                    return false;
                }

                if (!ValidateNoImplicitMultiplication(rightSide, out validationError))
                {
                    error = $"Error in right side: {validationError}";
                    return false;
                }

                // STEP 5: Expand summations if present
                leftSide = summationExpander.ExpandSummations(leftSide, out string sumError);
                if (!string.IsNullOrEmpty(sumError))
                {
                    error = $"Error in left side summation: {sumError}";
                    return false;
                }

                rightSide = summationExpander.ExpandSummations(rightSide, out sumError);
                if (!string.IsNullOrEmpty(sumError))
                {
                    error = $"Error in right side summation: {sumError}";
                    return false;
                }

                // STEP 6: Expand parentheses multiplication (e.g., 2*(x+y) -> 2*x+2*y)
                leftSide = parenthesesExpander.ExpandParenthesesMultiplication(leftSide);
                rightSide = parenthesesExpander.ExpandParenthesesMultiplication(rightSide);

                // STEP 7: Parse both sides as expressions
                if (!expressionParser.TryParseExpression(leftSide, out var leftCoefficients, out var leftConstant, out error))
                {
                    error = $"Error parsing left side: {error}";
                    return false;
                }

                if (!expressionParser.TryParseExpression(rightSide, out var rightCoefficients, out var rightConstant, out error))
                {
                    error = $"Error parsing right side: {error}";
                    return false;
                }

                // STEP 8: Combine coefficients (move everything to left side)
                // Standard form: left - right {op} 0
                // So we combine as: left - right
                var combinedCoefficients = CombineCoefficients(leftCoefficients, rightCoefficients);

                // STEP 9: Calculate constant term
                // Standard form: coeffs*vars {op} constant
                // So constant = rightConstant - leftConstant
                double leftConstantValue = leftConstant.Evaluate(modelManager);
                double rightConstantValue = rightConstant.Evaluate(modelManager);
                double constantValue = rightConstantValue - leftConstantValue;

                // STEP 10: Create the equation
                result = new LinearEquation
                {
                    Label = label,
                    BaseName = label ?? "eq",
                    Coefficients = combinedCoefficients,
                    Constant = new ConstantExpression(constantValue),
                    Operator = op
                };

                return true;
            }
            catch (Exception ex)
            {
                error = $"Unexpected error parsing equation: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Helper: Check if a character position is inside brackets or parentheses
        /// </summary>
        private bool IsInsideBracketsOrParens(string text, int position)
{
    int parenDepth = 0;
    int bracketDepth = 0;
    
    for (int i = 0; i < position; i++)
    {
        char c = text[i];
        
        if (c == '(') parenDepth++;
        else if (c == ')') parenDepth--;
        else if (c == '[') bracketDepth++;
        else if (c == ']') bracketDepth--;
    }
    
    return parenDepth > 0 || bracketDepth > 0;
}

/// <summary>
/// Splits an equation by relational operator
/// </summary>
private bool SplitByOperator(
    string equation,
    out RelationalOperator op,
    out string[] parts,
    out string error)
{
    op = RelationalOperator.Equal;
    parts = Array.Empty<string>();
    error = string.Empty;
    
    // Try operators in order (check two-character operators first)
    var operators = new[]
    {
        ("==", RelationalOperator.Equal),
        ("<=", RelationalOperator.LessThanOrEqual),
        (">=", RelationalOperator.GreaterThanOrEqual),
        ("<", RelationalOperator.LessThan),
        (">", RelationalOperator.GreaterThan)
    };
    
    foreach (var (opStr, opEnum) in operators)
    {
        int opIndex = FindOperatorAtTopLevel(equation, opStr);
        
        if (opIndex >= 0)
        {
            parts = new[]
            {
                equation.Substring(0, opIndex),
                equation.Substring(opIndex + opStr.Length)
            };
            op = opEnum;
            return true;
        }
    }
    
    error = "No relational operator (==, <=, >=, <, >) found in equation";
    return false;
}

/// <summary>
/// Finds an operator at the top level (not inside parentheses or brackets)
/// Returns -1 if not found
/// </summary>
private int FindOperatorAtTopLevel(string expression, string op)
{
    int depth = 0;
    int bracketDepth = 0;
    
    for (int i = 0; i <= expression.Length - op.Length; i++)
    {
        char c = expression[i];
        
        if (c == '(')
        {
            depth++;
        }
        else if (c == ')')
        {
            depth--;
        }
        else if (c == '[')
        {
            bracketDepth++;
        }
        else if (c == ']')
        {
            bracketDepth--;
        }
        else if (depth == 0 && bracketDepth == 0)
        {
            // Check if operator matches at this position
            if (i + op.Length <= expression.Length && 
                expression.Substring(i, op.Length) == op)
            {
                return i;
            }
        }
    }
    
    return -1;
}

/// <summary>
/// Combines coefficients from left and right sides
/// Result = left - right (for standard form: left - right {op} constant)
/// </summary>
private Dictionary<string, Expression> CombineCoefficients(
    Dictionary<string, Expression> leftCoefficients,
    Dictionary<string, Expression> rightCoefficients)
{
    var combined = new Dictionary<string, Expression>();
    
    // Add all left side coefficients
    foreach (var kvp in leftCoefficients)
    {
        combined[kvp.Key] = kvp.Value;
    }
    
    // Subtract all right side coefficients
    foreach (var kvp in rightCoefficients)
    {
        if (combined.ContainsKey(kvp.Key))
        {
            // Variable exists on both sides: left - right
            combined[kvp.Key] = new BinaryExpression(
                combined[kvp.Key],
                BinaryOperator.Subtract,
                kvp.Value
            );
        }
        else
        {
            // Variable only on right side: 0 - right = -right
            combined[kvp.Key] = new UnaryExpression(
                UnaryOperator.Negate,
                kvp.Value
            );
        }
    }
    
    return combined;
}

        private bool TryParseObjective(string statement, out Objective? objective, out string error)
        {
            objective = null;
            error = string.Empty;

            // Pattern: minimize/maximize [name:] expression;
            string pattern = @"^\s*(minimize|maximize)\s+(?:([a-zA-Z][a-zA-Z0-9_]*)\s*:\s*)?(.+)$";
            var match = Regex.Match(statement.Trim(), pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                error = "Not an objective function";
                return false;
            }

            string senseStr = match.Groups[1].Value.ToLower();
            string name = match.Groups[2].Value;
            string expression = match.Groups[3].Value;

            ObjectiveSense sense = senseStr == "minimize"
                ? ObjectiveSense.Minimize
                : ObjectiveSense.Maximize;

            // VALIDATE: No implicit multiplication
            if (!ValidateNoImplicitMultiplication(expression, out string validationError))
            {
                error = validationError;
                return false;
            }

            // Expand summations
            expression = summationExpander.ExpandSummations(expression, out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            // Expand parentheses multiplication
            expression = parenthesesExpander.ExpandParenthesesMultiplication(expression);

            // Remove whitespace
            string cleaned = Regex.Replace(expression, @"\s+", "");

            // Parse the expression
            if (!expressionParser.TryParseExpression(cleaned, out var coefficients, out var constant, out error))
            {
                error = $"Error parsing objective expression: {error}";
                return false;
            }

            // Validate variables
            if (!variableValidator.ValidateVariableDeclarations(coefficients.Keys.ToList(), out error))
            {
                return false;
            }

            objective = new Objective(sense, coefficients, constant,
                string.IsNullOrEmpty(name) ? null : name);

            return true;
        }

        private string RemoveBlockComments(string text)
        {
            var result = new StringBuilder();
            int i = 0;

            while (i < text.Length)
            {
                // Check for block comment start
                if (i < text.Length - 1 && text[i] == '/' && text[i + 1] == '*')
                {
                    // Find the closing */
                    int closeIndex = text.IndexOf("*/", i + 2);

                    if (closeIndex == -1)
                    {
                        // Unclosed block comment - treat rest of file as comment
                        break;
                    }

                    // Skip the entire comment block
                    // But preserve line breaks for accurate line number tracking
                    string commentBlock = text.Substring(i, closeIndex + 2 - i);
                    int lineBreaks = commentBlock.Count(c => c == '\n');

                    // Add newlines to maintain line numbers
                    for (int n = 0; n < lineBreaks; n++)
                    {
                        result.Append('\n');
                    }

                    i = closeIndex + 2; // Skip past */
                }
                else
                {
                    result.Append(text[i]);
                    i++;
                }
            }

            return result.ToString();
        }

        // Add to EquationParser class

        public ForallStatement? ParseForallStatement(string line)
        {
            // Example: forall(i in 1..n) x[i] <= capacity[i];
            // Example: forall(i in 1..n, j in 1..m: i != j) flow[i][j] <= cap[i][j];

            if (!line.TrimStart().StartsWith("forall", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                var forall = new ForallStatement();

                // Extract forall(...) part
                int openParen = line.IndexOf('(');
                int closeParen = FindMatchingParen(line, openParen);

                if (openParen == -1 || closeParen == -1)
                    throw new InvalidOperationException("Invalid forall syntax: missing parentheses");

                string forallDecl = line.Substring(openParen + 1, closeParen - openParen - 1);
                string constraintPart = line.Substring(closeParen + 1).Trim();

                // Parse forall declaration
                ParseForallDeclaration(forallDecl, forall);

                // Parse constraint template
                ParseConstraintTemplate(constraintPart, forall);

                return forall;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error parsing forall statement: {ex.Message}", ex);
            }
        }

        private void ParseForallDeclaration(string declaration, ForallStatement forall)
        {
            // Split by ':' to separate iterators from condition
            string[] parts = declaration.Split(':');
            string iteratorsPart = parts[0].Trim();
            string? conditionPart = parts.Length > 1 ? parts[1].Trim() : null;

            // Parse iterators (comma-separated)
            var iteratorDecls = iteratorsPart.Split(',');
            foreach (var iterDecl in iteratorDecls)
            {
                var iterator = ParseIterator(iterDecl.Trim());
                forall.Iterators.Add(iterator);
            }

            // Parse condition if present
            if (!string.IsNullOrEmpty(conditionPart))
            {
                forall.Condition = ParseConditionExpression(conditionPart);
            }
        }

        private ForallIterator ParseIterator(string iterDecl)
        {
            // Example: "i in 1..n" or "j in Cities"
            var match = System.Text.RegularExpressions.Regex.Match(
                iterDecl,
                @"(\w+)\s+in\s+(.+)" );

            if (!match.Success)
                throw new InvalidOperationException($"Invalid iterator syntax: {iterDecl}");

            string varName = match.Groups[1].Value;
            string rangePart = match.Groups[2].Value;

            return new ForallIterator
            {
                VariableName = varName,
                Range = ParseRangeExpression(rangePart)
            };
        }

        private RangeExpression ParseRangeExpression(string rangePart)
        {
            // Check for range operator ".."
            if (rangePart.Contains(".."))
            {
                var parts = rangePart.Split(new[] { ".." }, StringSplitOptions.None);
                if (parts.Length != 2)
                    throw new InvalidOperationException($"Invalid range syntax: {rangePart}");

                return new RangeExpression
                {
                    Start = ParseExpression(parts[0].Trim()),
                    End = ParseExpression(parts[1].Trim())
                };
            }
            else
            {
                // It's a set name
                return new RangeExpression
                {
                    SetName = rangePart.Trim()
                };
            }
        }

        private Expression ParseConditionExpression(string condition)
        {
            // Parse comparison expressions like "i != j", "i < j", etc.
            var comparisonOps = new[] { "!=", "==", "<=", ">=", "<", ">" };

            foreach (var op in comparisonOps)
            {
                if (condition.Contains(op))
                {
                    var parts = condition.Split(new[] { op }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        var left = ParseExpression(parts[0].Trim());
                        var right = ParseExpression(parts[1].Trim());

                        var binaryOp = op switch
                        {
                            "!=" => BinaryOperator.NotEqual,
                            "==" => BinaryOperator.Equal,
                            "<=" => BinaryOperator.LessThanOrEqual,
                            ">=" => BinaryOperator.GreaterThanOrEqual,
                            "<" => BinaryOperator.LessThan,
                            ">" => BinaryOperator.GreaterThan,
                            _ => throw new InvalidOperationException($"Unknown operator: {op}")
                        };

                        return new ComparisonExpression(left, binaryOp, right);
                    }
                }
            }

            throw new InvalidOperationException($"Invalid condition: {condition}");
        }

        private void ParseConstraintTemplate(string constraintPart, ForallStatement forall)
        {
            // Remove trailing semicolon
            constraintPart = constraintPart.TrimEnd(';').Trim();

            // Parse as regular constraint but create template
            var relOps = new[] { "<=", ">=", "==", "<", ">" };
            string? foundOp = null;
            int opIndex = -1;

            foreach (var op in relOps)
            {
                opIndex = constraintPart.IndexOf(op);
                if (opIndex >= 0)
                {
                    foundOp = op;
                    break;
                }
            }

            if (foundOp == null)
                throw new InvalidOperationException("No relational operator found in constraint");

            string leftPart = constraintPart.Substring(0, opIndex).Trim();
            string rightPart = constraintPart.Substring(opIndex + foundOp.Length).Trim();

            var template = new ConstraintTemplate
            {
                LeftSide = ParseExpression(leftPart),
                Operator = ParseRelationalOperator(foundOp),
                RightSide = ParseExpression(rightPart)
            };

            forall.ConstraintTemplate = template;
        }

        private int FindMatchingParen(string text, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < text.Length; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')') depth--;
                if (depth == 0) return i;
            }

            return -1;
        }

        public Expression ParseExpression(string exprStr)
        {
            exprStr = exprStr.Trim();
            
            // **NEW: Check for angle bracket tuple reference: <expr>**
            if (exprStr.StartsWith("<") && exprStr.EndsWith(">"))
            {
                string innerExpr = exprStr.Substring(1, exprStr.Length - 2).Trim();
                
                // Parse the inner expression (could be n.pred, s.id, etc.)
                var inner = ParseExpression(innerExpr);
                
                // Return wrapped in a tuple key expression
                return new TupleKeyExpression(inner);
            }
            
            // Check for item() function FIRST
            if (exprStr.StartsWith("item("))
            {
                if (ItemFunctionParser.TryParse(exprStr, modelManager, out var itemExpr, out var error))
                {
                    return itemExpr;
                }
            }

            // Check for item().field pattern
            if (exprStr.Contains("item(") && exprStr.Contains(")."))
            {
                if (ParseItemFieldAccess(exprStr, out var itemFieldExpr, out var error))
                {
                    return itemFieldExpr;
                }
            }

            if (exprStr.StartsWith("if", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseConditional(exprStr, out var conditional))
                {
                    return conditional;
                }
            }

            // **ENHANCED: Summation with filter**
            var sumMatch = Regex.Match(exprStr, 
                @"^\s*sum\s*\(([^:)]+)(?::(.+))?\)\s*(.+)$",
                RegexOptions.IgnoreCase);
    
            if (sumMatch.Success)
            {
                string iteratorsPart = sumMatch.Groups[1].Value;
                string? filterPart = sumMatch.Groups[2].Success ? sumMatch.Groups[2].Value : null;
                string bodyStr = sumMatch.Groups[3].Value.Trim();
        
                // Parse iterators (could be multiple: "i in Set1, j in Set2")
                var iterators = ParseSummationIterators(iteratorsPart, out var error);
                if (iterators == null || iterators.Count == 0)
                {
                    return new ConstantExpression(0); // Fallback
                }
        
                // Parse filter if present
                Expression? filter = null;
                if (!string.IsNullOrEmpty(filterPart))
                {
                    filter = ParseExpression(filterPart.Trim());
                }
        
                // Remove surrounding parentheses from body
                if (bodyStr.StartsWith("(") && bodyStr.EndsWith(")"))
                {
                    bodyStr = bodyStr.Substring(1, bodyStr.Length - 2).Trim();
                }
        
                Expression bodyExpr = ParseExpression(bodyStr);
        
                // Create filtered summation
                if (iterators.Count == 1 && filter == null)
                {
                    // Simple summation
                    return new SummationExpression(iterators[0].Item1, iterators[0].Item2, bodyExpr);
                }
                else
                {
                    // Filtered/multi-iterator summation
                    return new FilteredSummationExpression(iterators, filter, bodyExpr);
                }
            }
            // CHECK: Is this a summation expression?
            var sumMatch1 = Regex.Match(exprStr, 
                @"^\s*sum\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\)\s*(.+)$",
                RegexOptions.IgnoreCase);
            
            if (sumMatch1.Success)
            {
                string indexVar = sumMatch1.Groups[1].Value;
                string setName = sumMatch1.Groups[2].Value;
                string bodyStr = sumMatch1.Groups[3].Value.Trim();
                
                // Remove surrounding parentheses if present
                if (bodyStr.StartsWith("(") && bodyStr.EndsWith(")"))
                {
                    bodyStr = bodyStr.Substring(1, bodyStr.Length - 2).Trim();
                }
                
                // Recursively parse the body
                Expression bodyExpr = ParseExpression(bodyStr);
                
                // Create a SummationExpression
                return new SummationExpression(indexVar, setName, bodyExpr);
            }

            // Check for tuple field access FIRST
            if (TupleFieldAccessParser.IsTupleFieldAccess(exprStr))
            {
                if (TupleFieldAccessParser.TryParse(exprStr, out string varName, out string fieldName))
                {
                    return new DynamicTupleFieldAccessExpression(varName, fieldName);
                }
            }

            // NEW: Check for iterator-indexed tuple field access: tupleSet[iterator].field
            if (Regex.IsMatch(exprStr, @"^([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z][a-zA-Z0-9_]*)\]\.([a-zA-Z][a-zA-Z0-9_]*)$"))
            {
                var match = Regex.Match(exprStr, @"^([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z][a-zA-Z0-9_]*)\]\.([a-zA-Z][a-zA-Z0-9_]*)$");
        
                string setName = match.Groups[1].Value;
                string indexVar = match.Groups[2].Value;
                string fieldName = match.Groups[3].Value;

                // Check if it's a tuple set
                if (modelManager.TupleSets.ContainsKey(setName))
                {
                    // It's iterator-based tuple field access
                    return new IteratorIndexedTupleFieldAccessExpression(setName, indexVar, fieldName);
                }
            }

            // Try to parse as a number (constant)
            if (double.TryParse(exprStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double constValue))
            {
                return new ConstantExpression(constValue);
            }

            // Try to parse as a parameter
            if (modelManager.Parameters.ContainsKey(exprStr))
            {
                return new ParameterExpression(exprStr);
            }
    
            // Try to parse as a decision expression
    if (modelManager.DecisionExpressions.ContainsKey(exprStr))
    {
        return new DecisionExpressionExpression(exprStr);
    }

            // Try to parse as a variable reference
            if (IsVariableName(exprStr))
            {
                return new VariableExpression(exprStr);
            }

            // Try to parse as a binary expression
            if (TryParseBinaryExpression(exprStr, out var binaryExpr))
            {
                return binaryExpr;
            }

            // Try to parse as an indexed variable
            if (TryParseIndexedVariable(exprStr, out var indexedVar))
            {
                return indexedVar;
            }
    
            // Try to parse as indexed dexpr
    if (TryParseIndexedDexpr(exprStr, out var indexedDexpr))
    {
        return indexedDexpr;
    }

            // Fallback: return as a parameter expression (might be an iterator variable)
            return new ParameterExpression(exprStr);
        }

        private bool TryParseIndexedDexpr(string exprStr, out Expression? result)
        {
            result = null;

            // Pattern: dexprName[index]
            var match = Regex.Match(exprStr, @"^([a-zA-Z][a-zA-Z0-9_]*)\[([^\]]+)\]$");

            if (!match.Success)
                return false;

            string dexprName = match.Groups[1].Value;
            string indexStr = match.Groups[2].Value;

            // Check if it's a decision expression
            if (!modelManager.DecisionExpressions.ContainsKey(dexprName))
                return false;

            var dexpr = modelManager.DecisionExpressions[dexprName];
            
            if (!dexpr.IsIndexed)
            {
                // Trying to index a scalar dexpr - error
                return false;
            }

            // Parse index - could be literal or expression
            if (int.TryParse(indexStr, out int literalIndex))
            {
                result = new DecisionExpressionExpression(dexprName, literalIndex);
            }
            else
            {
                // Index is an expression (like 'i')
                var indexExpr = ParseExpression(indexStr);
                result = new DecisionExpressionExpression(dexprName, indexExpr);
            }

            return true;
        }

        /// <summary>
        /// Parses summation iterators (could be multiple)
        /// Example: "i in Set1" or "i in Set1, j in Set2"
        /// </summary>
        private List<(string varName, string setName)>? ParseSummationIterators(string iteratorsPart, out string error)
        {
            error = string.Empty;
            var iterators = new List<(string, string)>();
    
            var parts = SplitByCommaTopLevel(iteratorsPart);
    
            foreach (var part in parts)
            {
                var match = Regex.Match(part.Trim(), @"^([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)$");
                if (!match.Success)
                {
                    error = $"Invalid iterator: {part}";
                    return null;
                }
        
                iterators.Add((match.Groups[1].Value, match.Groups[2].Value));
            }
    
            return iterators;
        }

        private bool TryParseConditional(string exprStr, out Expression? result)
        {
            result = null;
    
            // Pattern: if(condition) { trueValue } else { falseValue }
            // Or: (condition) ? trueValue : falseValue
    
            // Try ternary operator first
            var ternaryMatch = Regex.Match(exprStr, @"^\((.+?)\)\s*\?\s*(.+?)\s*:\s*(.+)$");
            if (ternaryMatch.Success)
            {
                var condition = ParseExpression(ternaryMatch.Groups[1].Value.Trim());
                var trueValue = ParseExpression(ternaryMatch.Groups[2].Value.Trim());
                var falseValue = ParseExpression(ternaryMatch.Groups[3].Value.Trim());
        
                result = new ConditionalExpression(condition, trueValue, falseValue);
                return true;
            }
    
            // Try if/else block syntax
            var ifMatch = Regex.Match(exprStr, @"^if\s*\((.+?)\)\s*\{(.+?)\}\s*else\s*\{(.+?)\}$", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
    
            if (ifMatch.Success)
            {
                var condition = ParseExpression(ifMatch.Groups[1].Value.Trim());
                var trueValue = ParseExpression(ifMatch.Groups[2].Value.Trim());
                var falseValue = ParseExpression(ifMatch.Groups[3].Value.Trim());
        
                result = new ConditionalExpression(condition, trueValue, falseValue);
                return true;
            }
    
            return false;
        }

        private RelationalOperator ParseRelationalOperator(string op)
        {
            return op switch
            {
                "==" or "=" => RelationalOperator.Equal,
                "<=" or "≤" => RelationalOperator.LessThanOrEqual,
                ">=" or "≥" => RelationalOperator.GreaterThanOrEqual,
                "<" => RelationalOperator.LessThan,
                ">" => RelationalOperator.GreaterThan,
                _ => throw new InvalidOperationException($"Unknown relational operator: {op}")
            };
        }

        // Add these methods to the EquationParser class

private bool IsVariableName(string name)
{
    // Check if it matches variable naming pattern
    if (string.IsNullOrEmpty(name) || !char.IsLetter(name[0]))
        return false;

    if (!name.All(c => char.IsLetterOrDigit(c) || c == '_'))
        return false;
    
    // Make sure it's not a reserved keyword or existing parameter/dexpr
    if (modelManager.Parameters.ContainsKey(name))
        return false;
    
    if (modelManager.DecisionExpressions.ContainsKey(name))
        return false;
    
    return true;
}

private bool ParseItemFieldAccess(string expr, out Expression? result, out string error)
{
    result = null;
    error = string.Empty;

    // Pattern: item(...).field
    var match = Regex.Match(expr, @"^(item\(.+\))\.([a-zA-Z][a-zA-Z0-9_]*)$");
    if (!match.Success)
    {
        error = "Not item field access";
        return false;
    }

    string itemPart = match.Groups[1].Value;
    string fieldName = match.Groups[2].Value;

    if (!ItemFunctionParser.TryParse(itemPart, modelManager, out var itemExpr, out error))
        return false;

    result = new ItemFieldAccessExpression((ItemFunctionExpression)itemExpr, fieldName);
    return true;
}

private bool TryParseBinaryExpression(string exprStr, out Expression? result)
{
    result = null;

    // Simple binary operations: +, -, *, /
    var operators = new[] { "+", "-", "*", "/" };

    foreach (var op in operators)
    {
        int opIndex = FindOperatorIndex(exprStr, op);
        if (opIndex > 0 && opIndex < exprStr.Length - 1)
        {
            string leftPart = exprStr.Substring(0, opIndex).Trim();
            string rightPart = exprStr.Substring(opIndex + 1).Trim();

            var left = ParseExpression(leftPart);
            var right = ParseExpression(rightPart);

            var binaryOp = op switch
            {
                "+" => BinaryOperator.Add,
                "-" => BinaryOperator.Subtract,
                "*" => BinaryOperator.Multiply,
                "/" => BinaryOperator.Divide,
                _ => throw new InvalidOperationException($"Unknown operator: {op}")
            };

            result = new BinaryExpression(left, binaryOp, right);
            return true;
        }
    }

    return false;
}

private int FindOperatorIndex(string expr, string op)
{
    // Find the operator at the lowest precedence level (outside parentheses/brackets)
    int depth = 0;
    int lastIndex = -1;

    for (int i = 0; i < expr.Length; i++)
    {
        char c = expr[i];
        
        if (c == '(')
        {
            depth++;
        }
        else if (c == ')')
        {
            depth--;
        }
        else if (depth == 0)
        {
            // For - and +, we want the rightmost occurrence at depth 0 (lowest precedence)
            if (op == "+" || op == "-")
            {
                lastIndex = i;
            }
            else // For * and /, we want the leftmost occurrence at depth 0
            {
                if (expr[i] == op[0])
                {
                    return i;
                }
            }
        }
    }

    return lastIndex;
}

private bool TryParseIndexedVariable(string exprStr, out Expression? result)
{
    result = null;

    // Pattern: varName[index] or varName[index1][index2]
    var match = Regex.Match(exprStr, @"^([a-zA-Z][a-zA-Z0-9_]*)\[([^\]]+)\](?:\[([^\]]+)\])?$");

    if (!match.Success)
        return false;

    string baseName = match.Groups[1].Value;
    string index1Str = match.Groups[2].Value;
    string? index2Str = match.Groups[3].Success ? match.Groups[3].Value : null;

    // Make sure it's not a parameter or dexpr (they have their own handling)
    if (modelManager.Parameters.ContainsKey(baseName))
        return false;
    
    if (modelManager.DecisionExpressions.ContainsKey(baseName))
        return false;

    // Parse indices as expressions
    var index1Expr = ParseExpression(index1Str);

    if (index2Str != null)
    {
        var index2Expr = ParseExpression(index2Str);
        result = new IndexedVariableExpression(baseName, index1Expr, index2Expr);
    }
    else
    {
        result = new IndexedVariableExpression(baseName, index1Expr);
    }

    return true;
}
        
/// <summary>
/// Validates that there are no consecutive identifiers at the top level
/// Ignores content inside parentheses, brackets, or function calls
/// </summary>
private bool ValidateNoImplicitMultiplication(string expression, out string error)
{
    error = string.Empty;
    
    var tokens = TokenizeTopLevel(expression);
    
    // Check for consecutive identifiers
    for (int i = 0; i < tokens.Count - 1; i++)
    {
        if (tokens[i].Type == TokenType.Identifier && 
            tokens[i + 1].Type == TokenType.Identifier)
        {
            // Check if this is valid OPL syntax
            if (!IsValidOplSyntax(tokens[i].Value, tokens[i + 1].Value))
            {
                error = $"Consecutive identifiers '{tokens[i].Value}' and '{tokens[i + 1].Value}' without operator. Did you mean '{tokens[i].Value} * {tokens[i + 1].Value}'?";
                return false;
            }
        }
    }
    
    return true;
}

/// <summary>
/// Checks if two consecutive identifiers form valid OPL syntax
/// (e.g., type declarations like "int x", "float y")
/// </summary>
private bool IsValidOplSyntax(string firstWord, string secondWord)
{
    string first = firstWord.ToLower();
    string second = secondWord.ToLower();
    
    // Type declarations: "int x", "float y", etc.
    var types = new[] { "int", "float", "string", "bool", "range", "tuple", "dvar", "var", "dexpr" };
    if (types.Contains(first))
        return true;
    
    // Keywords that can be followed by identifiers
    var validFirstWords = new[] { "subject", "key", "minimize", "maximize", "forall", "sum" };
    if (validFirstWords.Contains(first))
        return true;
    
    // "in" keyword (used in iterators: "i in Set")
    if (second == "in")
        return true;
    
    return false;
}   

private enum TokenType
{
    Identifier,
    Number,
    Operator,
    FunctionCall, // sum(...), forall(...), etc.
    Bracket,
    Other
}

private class Token
{
    public TokenType Type { get; set; }
    public string Value { get; set; }
    
    public Token(TokenType type, string value)
    {
        Type = type;
        Value = value;
    }
}

/// <summary>
/// Tokenizes only the top level of an expression (content inside parens is treated as single token)
/// </summary>
private List<Token> TokenizeTopLevel(string expression)
{
    var tokens = new List<Token>();
    var current = new System.Text.StringBuilder();
    int depth = 0;
    int bracketDepth = 0;
    
    for (int i = 0; i < expression.Length; i++)
    {
        char c = expression[i];
        
        if (c == '(' || c == '[')
        {
            if (depth == 0 && bracketDepth == 0)
            {
                // Starting a nested section
                if (current.Length > 0)
                {
                    // This was a function name
                    tokens.Add(new Token(TokenType.FunctionCall, current.ToString().Trim()));
                    current.Clear();
                }
            }
            
            if (c == '(') depth++;
            else bracketDepth++;
            continue;
        }
        
        if (c == ')' || c == ']')
        {
            if (c == ')') depth--;
            else bracketDepth--;
            continue;
        }
        
        // Only process top-level characters
        if (depth == 0 && bracketDepth == 0)
        {
            if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    tokens.Add(ClassifyToken(current.ToString()));
                    current.Clear();
                }
            }
            else if ("+-*/".Contains(c))
            {
                if (current.Length > 0)
                {
                    tokens.Add(ClassifyToken(current.ToString()));
                    current.Clear();
                }
                tokens.Add(new Token(TokenType.Operator, c.ToString()));
            }
            else
            {
                current.Append(c);
            }
        }
    }
    
    if (current.Length > 0)
    {
        tokens.Add(ClassifyToken(current.ToString()));
    }
    
    return tokens;
}

private Token ClassifyToken(string value)
{
    value = value.Trim();
    
    if (string.IsNullOrEmpty(value))
        return new Token(TokenType.Other, value);
    
    // Check if it's a number (including scientific notation)
    if (double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _))
        return new Token(TokenType.Number, value);
    
    // Check if it's an identifier
    if (char.IsLetter(value[0]) || value[0] == '_')
        return new Token(TokenType.Identifier, value);
    
    return new Token(TokenType.Other, value);
}

/// <summary>
/// Extracts the Nth iterator variable from a template (for multi-dimensional equations)
/// </summary>
private string ExtractIteratorVariable(string template, int iteratorIndex)
{
    // Find all indexed access patterns: something[iterator]
    var matches = Regex.Matches(template, @"\[([a-zA-Z][a-zA-Z0-9_]*)\]");
    
    // Collect unique iterator variables
    var iteratorVars = new HashSet<string>();
    foreach (Match match in matches)
    {
        string varName = match.Groups[1].Value;
        // Only add if it looks like an iterator (not a number)
        if (!int.TryParse(varName, out _))
        {
            iteratorVars.Add(varName);
        }
    }
    
    var iteratorList = iteratorVars.ToList();
    
    if (iteratorIndex < iteratorList.Count)
    {
        return iteratorList[iteratorIndex];
    }
    
    // Fallback to default names
    return iteratorIndex == 0 ? "i" : "j";
}
    }
}