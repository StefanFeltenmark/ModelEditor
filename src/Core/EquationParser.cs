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

        public EquationParser(ModelManager manager)
        {
            modelManager = manager;
            evaluator = new ExpressionEvaluator(manager);
            jsEvaluator = new JavaScriptEvaluator(manager);
            
            // Initialize specialized parsers
            parameterParser = new ParameterParser(manager, evaluator);
            indexSetParser = new IndexSetParser(manager, evaluator);
            variableParser = new VariableDeclarationParser(manager, evaluator);
            expressionParser = new ExpressionParser(manager);
            summationExpander = new SummationExpander(manager);
            parenthesesExpander = new ParenthesesExpander();
            variableValidator = new VariableValidator(manager);
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

            // Extract and process JavaScript execute blocks FIRST
            var (processedText, lineMapping) = ExtractAndProcessExecuteBlocks(text, result);

            // **NEW: Extract and process tuple schemas**
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

            return result;
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

            // Parse field declarations
            var fieldPattern = @"(float|int|bool|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*;";
            var fieldMatches = Regex.Matches(body, fieldPattern, RegexOptions.IgnoreCase);

            if (fieldMatches.Count == 0)
            {
                error = "Tuple must have at least one field";
                return false;
            }

            foreach (Match fieldMatch in fieldMatches)
            {
                string typeStr = fieldMatch.Groups[1].Value.ToLower();
                string fieldName = fieldMatch.Groups[2].Value;

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
                    schema.AddField(fieldName, fieldType);
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
                    var parts = currentStatement.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
            bool matched = false;
            
            // Try parameter parsing
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

            // Try index set parsing
            matched = indexSetParser.TryParse(statement, out var indexSet, out error);
            if (matched)
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
            
            if (!string.IsNullOrEmpty(error) && 
                !error.Equals("Not an index set declaration", StringComparison.Ordinal))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            
            error = string.Empty;

            // Try variable declaration
            matched = variableParser.TryParse(statement, out var variable, out error);
            if (matched)
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
            
            if (!string.IsNullOrEmpty(error) && 
                !error.Equals("Not a variable declaration", StringComparison.Ordinal))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            
            error = string.Empty;

            // **NEW: Try tuple set declaration**
            if (TryParseTupleSet(statement, out var tupleSet, out error))
            {
                if (tupleSet != null)
                {
                    modelManager.AddTupleSet(tupleSet);
                    result.IncrementSuccess();
                    return;
                }
            }
            
            // Check for validation errors
            if (!string.IsNullOrEmpty(error) && 
                !error.Equals("Not a tuple set declaration", StringComparison.Ordinal))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            
            error = string.Empty;

            // Try indexed equation
            if (TryParseIndexedEquation(statement, lineNumber, out error, result))
            {
                result.IncrementSuccess();
                return;
            }
            
            if (!string.IsNullOrEmpty(error) && 
                !error.Equals("Not an indexed equation declaration", StringComparison.Ordinal))
            {
                result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                return;
            }
            
            error = string.Empty;

            // Try regular equation
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

            // Nothing matched
            if (string.IsNullOrEmpty(error))
            {
                error = "Unknown statement type. Expected: parameter, index set, variable, tuple set, or equation declaration";
            }
            
            result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
        }

        // Add this method to parse tuple sets
        private bool TryParseTupleSet(string statement, out TupleSet? tupleSet, out string error)
        {
            tupleSet = null;
            error = string.Empty;

            // Pattern: {SchemaName} setName = ...;
            string pattern = @"^\s*\{([a-zA-Z][a-zA-Z0-9_]*)\}\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not a tuple set declaration";
                return false;
            }

            string schemaName = match.Groups[1].Value;
            string setName = match.Groups[2].Value;
            string value = match.Groups[3].Value.Trim();

            if (!modelManager.TupleSchemas.ContainsKey(schemaName))
            {
                error = $"Tuple schema '{schemaName}' is not defined";
                return false;
            }

            bool isExternal = value == "...";
            tupleSet = new TupleSet(setName, schemaName, isExternal);

            return true;
        }

        private bool TryParseIndexedEquation(string statement, int lineNumber, out string error, ParseSessionResult result)
        {
            error = string.Empty;

            // OPL-style forall: forall(i in I, j in J) [label:] expression
            // More flexible pattern to handle labels and whitespace better
            string forallTwoDimPattern = @"^\s*forall\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\)\s*(?:([a-zA-Z][a-zA-Z0-9_]*)\s*:\s*)?(.+)$";
            var forallTwoDimMatch = Regex.Match(statement.Trim(), forallTwoDimPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

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

            // OPL-style forall single dimension: forall(i in I) [label:] expression
            // More flexible pattern - handles whitespace and optional label
            string forallPattern = @"^\s*forall\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\)\s*(?:([a-zA-Z][a-zA-Z0-9_]*)\s*:\s*)?(.+)$";
            var forallMatch = Regex.Match(statement.Trim(), forallPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (forallMatch.Success)
            {
                string indexVar = forallMatch.Groups[1].Value;
                string indexSetName = forallMatch.Groups[2].Value;
                string baseName = forallMatch.Groups[3].Value.Trim();
                string template = forallMatch.Groups[4].Value.Trim();

                if (string.IsNullOrEmpty(baseName))
                {
                    baseName = $"constraint_{modelManager.IndexedEquationTemplates.Count + 1}";
                }

                if (!modelManager.IndexSets.ContainsKey(indexSetName))
                {
                    error = $"Index set '{indexSetName}' is not declared";
                    return false;
                }

                var indexedEquation = new IndexedEquation(baseName, indexSetName, template);
                modelManager.AddIndexedEquationTemplate(indexedEquation);
                return true;
            }

            // Original bracket notation: constraint[i in I, j in J]: ...
            string twoDimPattern = @"^\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\]\s*:\s*(.+)$";
            var twoDimMatch = Regex.Match(statement.Trim(), twoDimPattern);

            if (twoDimMatch.Success)
            {
                return ProcessTwoDimensionalIndexedEquation(twoDimMatch, out error);
            }

            // Original bracket notation: constraint[i in I]: ...
            string pattern = @"^\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\]\s*:\s*(.+)$";
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

            string indexVar1 = indexedEquation.IndexSetName.ToLower();
            string indexVar2 = indexedEquation.SecondIndexSetName!.ToLower();

            foreach (int index1 in indexSet1.GetIndices())
            {
                foreach (int index2 in indexSet2.GetIndices())
                {
                    string expandedEquation = summationExpander.ExpandEquationTemplate(
                        indexedEquation.Template, indexVar1, index1);
                    expandedEquation = summationExpander.ExpandEquationTemplate(
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

        private void ExpandSingleDimensionalEquation(IndexedEquation indexedEquation, ParseSessionResult result)
        {
            var indexSet = modelManager.IndexSets[indexedEquation.IndexSetName];
            string indexVar = indexedEquation.IndexSetName.ToLower();

            foreach (int index in indexSet.GetIndices())
            {
                string expandedEquation = summationExpander.ExpandEquationTemplate(
                    indexedEquation.Template, indexVar, index);
                
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
                    result.AddError(
                        $"Error expanding equation '{indexedEquation.BaseName}[{index}]': {eqError}", 
                        0);
                }
            }
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

        private bool TryParseEquation(string equation, out LinearEquation? result, out string error)
        {
            result = null;
            error = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(equation))
                {
                    error = "Equation is empty or contains only whitespace";
                    return false;
                }

                // Extract label if present
                string? label = null;
                string equationText = equation.Trim();
                
                if (!ExtractLabel(ref equationText, ref label, out error))
                {
                    return false;
                }

                // Expand summations
                equationText = summationExpander.ExpandSummations(equationText, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }

                // Expand parentheses multiplication
                equationText = parenthesesExpander.ExpandParenthesesMultiplication(equationText);

                // Remove whitespace
                string cleaned = Regex.Replace(equationText, @"\s+", "");

                // Validate no remaining colons
                if (cleaned.Contains(':'))
                {
                    error = "Invalid equation format. Unexpected ':' found in equation";
                    return false;
                }

                // Parse operator and split
                if (!SplitByOperator(cleaned, out var op, out var parts, out error))
                {
                    return false;
                }

                // Parse both sides
                if (!expressionParser.TryParseExpression(parts[0], out var leftCoefficients, out var leftConstant, out error))
                {
                    error = $"Error parsing left side: {error}";
                    return false;
                }

                if (!expressionParser.TryParseExpression(parts[1], out var rightCoefficients, out var rightConstant, out error))
                {
                    error = $"Error parsing right side: {error}";
                    return false;
                }

                // Validate variables
                var allCoefficients = leftCoefficients.Keys.Concat(rightCoefficients.Keys).Distinct();
                if (!variableValidator.ValidateVariableDeclarations(allCoefficients.ToList(), out error))
                {
                    return false;
                }

                // Combine coefficients and constants
                var finalCoefficients = CombineCoefficients(leftCoefficients, rightCoefficients);
                var finalConstant = new BinaryExpression(rightConstant, BinaryOperator.Subtract, leftConstant);

                result = new LinearEquation(finalCoefficients, finalConstant, op, label);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Unexpected error: {ex.Message}";
                return false;
            }
        }

        private bool ExtractLabel(ref string equationText, ref string? label, out string error)
        {
            error = string.Empty;
            
            string labelPattern = @"^([a-zA-Z][a-zA-Z0-9_]*)\s*:\s*(.+)$";
            var labelMatch = Regex.Match(equationText, labelPattern);
            
            if (labelMatch.Success)
            {
                string potentialLabel = labelMatch.Groups[1].Value;
                string remainingText = labelMatch.Groups[2].Value;

                // Verify remaining text has a relational operator
                if (remainingText.Contains("==") || remainingText.Contains("<=") || 
                    remainingText.Contains(">=") || remainingText.Contains("<") || 
                    remainingText.Contains(">") || remainingText.Contains("≤") || 
                    remainingText.Contains("≥"))
                {
                    label = potentialLabel;
                    equationText = remainingText;
                }
                else
                {
                    error = $"Invalid label format. Found '{potentialLabel}:' but no relational operator";
                    return false;
                }
            }

            return true;
        }

        private bool SplitByOperator(
            string cleaned, 
            out RelationalOperator op, 
            out string[] parts, 
            out string error)
        {
            op = RelationalOperator.Equal;
            parts = Array.Empty<string>();
            error = string.Empty;

            // Check operators in order of longest to shortest
            if (cleaned.Contains("=="))
            {
                op = RelationalOperator.Equal;
                parts = cleaned.Split(new[] { "==" }, StringSplitOptions.None);
            }
            else if (cleaned.Contains("<=") || cleaned.Contains("≤"))
            {
                op = RelationalOperator.LessThanOrEqual;
                parts = cleaned.Contains("<=") 
                    ? cleaned.Split(new[] { "<=" }, StringSplitOptions.None)
                    : cleaned.Split('≤');
            }
            else if (cleaned.Contains(">=") || cleaned.Contains("≥"))
            {
                op = RelationalOperator.GreaterThanOrEqual;
                parts = cleaned.Contains(">=")
                    ? cleaned.Split(new[] { ">=" }, StringSplitOptions.None)
                    : cleaned.Split('≥');
            }
            else if (cleaned.Contains('<'))
            {
                op = RelationalOperator.LessThan;
                parts = cleaned.Split('<');
            }
            else if (cleaned.Contains('>'))
            {
                op = RelationalOperator.GreaterThan;
                parts = cleaned.Split('>');
            }
            else if (cleaned.Contains('='))
            {
                error = "Invalid operator '='. Use '==' for equality";
                return false;
            }
            else
            {
                error = "Missing relational operator. Must contain ==, <, >, <=, or >=";
                return false;
            }

            if (parts.Length > 2)
            {
                error = "Multiple relational operators found";
                return false;
            }

            if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
            {
                error = "Invalid equation structure";
                return false;
            }

            return true;
        }

        private Dictionary<string, Expression> CombineCoefficients(
            Dictionary<string, Expression> leftCoefficients, 
            Dictionary<string, Expression> rightCoefficients)
        {
            var finalCoefficients = new Dictionary<string, Expression>();
            
            // Add left side coefficients
            foreach (var kvp in leftCoefficients)
            {
                finalCoefficients[kvp.Key] = kvp.Value;
            }

            // Subtract right side coefficients
            foreach (var kvp in rightCoefficients)
            {
                if (finalCoefficients.ContainsKey(kvp.Key))
                {
                    finalCoefficients[kvp.Key] = new BinaryExpression(
                        finalCoefficients[kvp.Key],
                        BinaryOperator.Subtract,
                        kvp.Value);
                }
                else
                {
                    finalCoefficients[kvp.Key] = new UnaryExpression(
                        UnaryOperator.Negate,
                        kvp.Value);
                }
            }

            return finalCoefficients;
        }
    }

    public class ParseSessionResult
    {
        public List<(string Message, int LineNumber)> Errors { get; private set; } = new List<(string, int)>();
        public int SuccessCount { get; private set; } = 0;

        public void AddError(string error, int lineNumber)
        {
            Errors.Add(($"Line {lineNumber}: {error}", lineNumber));
        }

        public void IncrementSuccess()
        {
            SuccessCount++;
        }

        public bool HasErrors => Errors.Count > 0;
        public bool HasSuccess => SuccessCount > 0;

        public IEnumerable<string> GetErrorMessages()
        {
            return Errors.Select(e => e.Message);
        }

        public IEnumerable<string> GetErrorsForLine(int lineNumber)
        {
            return Errors.Where(e => e.LineNumber == lineNumber).Select(e => e.Message);
        }
    }
}