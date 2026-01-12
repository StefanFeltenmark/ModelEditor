using Jint;

namespace Core
{
    /// <summary>
    /// Evaluates JavaScript expressions and code blocks
    /// </summary>
    public class JavaScriptEvaluator
    {
        private readonly Engine engine;
        private readonly ModelManager modelManager;

        public JavaScriptEvaluator(ModelManager manager)
        {
            modelManager = manager;
            engine = new Engine(options =>
            {
                options.LimitRecursion(100);
                options.TimeoutInterval(TimeSpan.FromSeconds(5));
                options.AllowClr();
            });

            // Expose model data to JavaScript
            RefreshModelContext();
        }

        private void RefreshModelContext()
        {
            // Make parameters available in JavaScript
            var paramObject = new Dictionary<string, object>();
            foreach (var param in modelManager.Parameters.Values)
            {
                paramObject[param.Name] = param.Value;
            }
            engine.SetValue("params", paramObject);

            // Make variables available in JavaScript
            var varObject = new Dictionary<string, object>();
            foreach (var variable in modelManager.IndexedVariables.Values)
            {
                // For indexed variables, create an object with metadata
                if (!string.IsNullOrEmpty(variable.IndexSetName))
                {
                    var indexSet = modelManager.IndexSets[variable.IndexSetName];
                    var varInfo = new Dictionary<string, object>
                    {
                        ["name"] = variable.BaseName,
                        ["type"] = variable.Type.ToString(),
                        ["indexSet"] = variable.IndexSetName,
                        ["startIndex"] = indexSet.StartIndex,
                        ["endIndex"] = indexSet.EndIndex
                    };
                    varObject[variable.BaseName] = varInfo;
                }
                else
                {
                    // Scalar variable
                    var varInfo = new Dictionary<string, object>
                    {
                        ["name"] = variable.BaseName,
                        ["type"] = variable.Type.ToString(),
                        ["isScalar"] = true
                    };
                    varObject[variable.BaseName] = varInfo;
                }
            }
            engine.SetValue("variables", varObject);

            // Make index sets available
            var indexSetObject = new Dictionary<string, object>();
            foreach (var indexSet in modelManager.IndexSets.Values)
            {
                var setInfo = new Dictionary<string, object>
                {
                    ["name"] = indexSet.Name,
                    ["start"] = indexSet.StartIndex,
                    ["end"] = indexSet.EndIndex,
                    ["count"] = indexSet.GetIndices().Count()
                };
                indexSetObject[indexSet.Name] = setInfo;
            }
            engine.SetValue("indexSets", indexSetObject);

            // Add common math functions and utilities
            engine.Execute(@"
                var sqrt = Math.sqrt;
                var pow = Math.pow;
                var abs = Math.abs;
                var floor = Math.floor;
                var ceil = Math.ceil;
                var round = Math.round;
                var min = Math.min;
                var max = Math.max;
                var log = Math.log;
                var exp = Math.exp;
                var sin = Math.sin;
                var cos = Math.cos;
                var tan = Math.tan;
                
                // Helper function to create a range
                function range(start, end) {
                    var result = [];
                    for (var i = start; i <= end; i++) {
                        result.push(i);
                    }
                    return result;
                }
                
                // Helper function to sum an array
                function sum(array) {
                    return array.reduce(function(a, b) { return a + b; }, 0);
                }
            ");
        }

        public EvaluationResult<Dictionary<string, object>> ExecuteCodeBlock(string jsCode)
        {
            try
            {
                // Refresh parameter and variable context before execution
                RefreshModelContext();

                // Create a results object to store computed values
                engine.Execute("var results = {};");

                // Execute the JavaScript code
                engine.Execute(jsCode);

                // Extract results
                var resultsValue = engine.Evaluate("results");
                var results = new Dictionary<string, object>();

                if (resultsValue.IsObject())
                {
                    var obj = resultsValue.AsObject();
                    foreach (var prop in obj.GetOwnProperties())
                    {
                        var key = prop.Key.ToString();
                        var value = prop.Value.Value;

                        if (value.IsNumber())
                        {
                            results[key] = value.AsNumber();
                        }
                        else if (value.IsString())
                        {
                            results[key] = value.AsString();
                        }
                        else if (value.IsBoolean())
                        {
                            results[key] = value.AsBoolean();
                        }
                        else if (value.IsArray())
                        {
                            var array = value.AsArray();
                            var list = new List<object>();
                            for (int i = 0; i < array.Length; i++)
                            {
                                var item = array.Get(i.ToString());
                                if (item.IsNumber())
                                    list.Add(item.AsNumber());
                                else if (item.IsString())
                                    list.Add(item.AsString());
                                else if (item.IsBoolean())
                                    list.Add(item.AsBoolean());
                            }
                            results[key] = list;
                        }
                    }
                }

                return EvaluationResult<Dictionary<string, object>>.Success(results);
            }
            catch (Jint.Runtime.JavaScriptException jsEx)
            {
                return EvaluationResult<Dictionary<string, object>>.Failure($"JavaScript error: {jsEx.Message}");
            }
            catch (Exception ex)
            {
                return EvaluationResult<Dictionary<string, object>>.Failure($"Error executing JavaScript: {ex.Message}");
            }
        }

        public EvaluationResult<double> EvaluateExpression(string jsExpression)
        {
            try
            {
                // Refresh parameter context before evaluation
                RefreshModelContext();

                var result = engine.Evaluate(jsExpression);
                
                if (result.IsNumber())
                {
                    double value = result.AsNumber();
                    return EvaluationResult<double>.Success(value);
                }
                
                return EvaluationResult<double>.Failure($"JavaScript expression did not return a number: {result}");
            }
            catch (Jint.Runtime.JavaScriptException jsEx)
            {
                return EvaluationResult<double>.Failure($"JavaScript error: {jsEx.Message}");
            }
            catch (Exception ex)
            {
                return EvaluationResult<double>.Failure($"Error evaluating JavaScript: {ex.Message}");
            }
        }

        public EvaluationResult<int> EvaluateIntExpression(string jsExpression)
        {
            var result = EvaluateExpression(jsExpression);
            if (result.IsSuccess)
            {
                return EvaluationResult<int>.Success((int)Math.Round(result.Value));
            }
            return EvaluationResult<int>.Failure(result.ErrorMessage);
        }
    }
}