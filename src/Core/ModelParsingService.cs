using Core.Models;

namespace Core
{
    /// <summary>
    /// Service for parsing and validating complete models
    /// </summary>
    public class ModelParsingService
    {
        private readonly ModelManager modelManager;
        private readonly EquationParser parser;
        private readonly DataFileParser dataParser;

        public ModelParsingService(ModelManager modelManager, EquationParser parser, DataFileParser dataParser)
        {
            this.modelManager = modelManager;
            this.parser = parser;
            this.dataParser = dataParser;
        }

        /// <summary>
        /// Parses model and data files and returns a structured result
        /// </summary>
        /// <param name="modelTexts">Content of model files (.mod)</param>
        /// <param name="dataTexts">Content of data files (.dat)</param>
        /// <returns>ParseResult with success/error information</returns>
        public ParseResult ParseModel(List<string> modelTexts, List<string> dataTexts)
        {
            var result = new ParseResult();
            var allResults = new List<ParseSessionResult>();

            try
            {
                // Clear previous model state
                modelManager.Clear();

                // Validate inputs
                if (modelTexts == null || modelTexts.Count == 0)
                {
                    result.Success = false;
                    result.Errors.Add("No model files provided");
                    result.SummaryMessage = "No model files to parse";
                    return result;
                }

                // STEP 1: Parse model files (declarations and templates only)
                foreach (var text in modelTexts)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var parseResult = parser.Parse(text);
                        allResults.Add(parseResult);
                    }
                }

                // STEP 2: Parse data files (populate parameter values)
                if (dataTexts != null)
                {
                    foreach (var text in dataTexts)
                    {
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var dataResult = dataParser.Parse(text);
                            var sessionResult = new ParseSessionResult();

                            foreach (var error in dataResult.Errors)
                            {
                                sessionResult.Errors.Add(error);
                            }

                            allResults.Add(sessionResult);
                        }
                    }
                }

                // STEP 3: Check for missing external parameter values
                var missingParams = modelManager.Parameters.Values
                    .Where(p => p.IsExternal && !p.HasValue)
                    .ToList();

                if (missingParams.Any())
                {
                    var errorResult = new ParseSessionResult();

                    if (dataTexts == null || dataTexts.Count == 0)
                    {
                        var suggestions = string.Join("\n", missingParams.Select(p =>
                            p.IsIndexed
                                ? $"  {p.Name} = [value1, value2, ...];"
                                : $"  {p.Name} = <value>;"));

                        errorResult.AddError(
                            $"External parameters require data values. Create a .dat file with:\n{suggestions}",
                            0);
                    }
                    else
                    {
                        foreach (var param in missingParams)
                        {
                            errorResult.AddError(
                                $"Missing required data: parameter '{param.Name}' is declared as external " +
                                $"(type: {param.Type}) but no value was provided in the data file(s)",
                                0);
                        }
                    }

                    allResults.Add(errorResult);
                }

                // STEP 4: Expand indexed equations (after data is loaded)
                if (!missingParams.Any())
                {
                    var expansionResult = new ParseSessionResult();
                    parser.ExpandIndexedEquations(expansionResult);
                    allResults.Add(expansionResult);

                    if (expansionResult.HasErrors)
                    {
                        foreach (var error in expansionResult.Errors)
                        {
                            result.Warnings.Add($"Equation expansion warning: {error.Message}");
                        }
                    }
                }

                // Calculate totals
                result.TotalSuccess = allResults.Sum(r => r.SuccessCount);
                result.TotalErrors = allResults.Sum(r => r.Errors.Count);

                // Collect all errors
                foreach (var parseResult in allResults)
                {
                    foreach (var error in parseResult.Errors)
                    {
                        result.Errors.Add(error.Message);
                    }
                }

                // Set success flag and summary
                result.Success = result.TotalSuccess > 0 && result.TotalErrors == 0;
                result.SummaryMessage = result.Success
                    ? $"Parse successful: {result.TotalSuccess} statements"
                    : result.TotalSuccess > 0
                        ? $"Parsed with errors: {result.TotalSuccess} statements, {result.TotalErrors} errors"
                        : $"Parse failed: {result.TotalErrors} errors";

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.TotalErrors++;
                result.Errors.Add($"Critical error during parsing: {ex.Message}");
                result.SummaryMessage = "Critical error during parsing";
                return result;
            }
        }

        /// <summary>
        /// Gets a formatted help message for supported syntax
        /// </summary>
        public static string GetSyntaxHelpMessage()
        {
            return @"Supported formats:
  Model file (.mod):
    Parameters: int T = 10; float capacity = 100.5;
    External parameters: float c = ... (requires data file)
    Indexed parameters: float cost[Products] = ...;
    Index sets: range I = 1..T;
    Variables: var float x[I];
    Equations: constraint_name[i in I]: x[i] >= cost[i];
    Summations: budget: sum(i in I) cost[i]*x[i] <= 100;
    2D Equations: flow[i in I, j in J]: x[i,j] <= capacity[i,j];
  Data file (.dat):
    Scalar: c = 100;
    Vector: cost = [10, 20, 30];
    Matrix: capacity = [[1, 2], [3, 4]];
    Indexed: cost[1] = 10;";
        }
    }
}