using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing.Tokenization
{
    /// <summary>
    /// Tokenizes math function calls: abs(x), sqrt(x), pow(x,y), sin(x), etc.
    /// Converts them to tokens mapped to MathFunctionExpression nodes so that
    /// the expression parser can treat them as atomic operands.
    /// </summary>
    public class MathFunctionTokenizer : ITokenizationStrategy
    {
        public int Priority => 2; // After ItemExpression (1) but before index tokenizers (3, 4)
        public string Name => "MathFunction";

        private static readonly Dictionary<string, MathFunction> FunctionMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["abs"]   = MathFunction.Abs,
                ["ceil"]  = MathFunction.Ceil,
                ["floor"] = MathFunction.Floor,
                ["round"] = MathFunction.Round,
                ["sqrt"]  = MathFunction.Sqrt,
                ["pow"]   = MathFunction.Pow,
                ["log"]   = MathFunction.Log,
                ["exp"]   = MathFunction.Exp,
                ["sin"]   = MathFunction.Sin,
                ["cos"]   = MathFunction.Cos,
                ["tan"]   = MathFunction.Tan,
                ["min"]   = MathFunction.Min,
                ["max"]   = MathFunction.Max,
            };

        // Match: funcName( ... ) where parens may be nested
        private static readonly Regex FuncNamePattern =
            new(@"\b(abs|ceil|floor|round|sqrt|pow|log|exp|sin|cos|tan|min|max)\s*\(",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string Tokenize(string expression, TokenManager tokenManager, ModelManager modelManager)
        {
            int searchFrom = 0;
            while (true)
            {
                var match = FuncNamePattern.Match(expression, searchFrom);
                if (!match.Success) break;

                string funcName = match.Groups[1].Value;
                int openParen = match.Index + match.Length - 1; // position of '('

                // Find the matching closing paren
                int closeParen = FindMatchingParen(expression, openParen);
                if (closeParen < 0)
                {
                    // Unmatched paren — skip past this match to avoid infinite loop
                    searchFrom = match.Index + match.Length;
                    continue;
                }

                string argsStr = expression.Substring(openParen + 1, closeParen - openParen - 1);

                if (!FunctionMap.TryGetValue(funcName, out var func))
                {
                    searchFrom = closeParen + 1;
                    continue;
                }

                // Parse each comma-separated argument (respecting nested parens)
                var argStrings = SplitArgs(argsStr);
                var argExpressions = argStrings
                    .Select(a => ParseArgument(a.Trim(), modelManager))
                    .ToArray();

                var mathExpr = new MathFunctionExpression { Function = func, Arguments = argExpressions };
                string token = tokenManager.CreateToken(mathExpr, "MATH");

                // Replace the entire function call with the token
                int callLength = closeParen - match.Index + 1;
                expression = expression.Substring(0, match.Index) + token + expression.Substring(match.Index + callLength);

                // searchFrom stays at match.Index; the token is shorter, so this is safe
                searchFrom = match.Index + token.Length;
            }

            return expression;
        }

        private static int FindMatchingParen(string text, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < text.Length; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static List<string> SplitArgs(string args)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == '(') depth++;
                else if (args[i] == ')') depth--;
                else if (args[i] == ',' && depth == 0)
                {
                    result.Add(args.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < args.Length)
                result.Add(args.Substring(start));
            return result;
        }

        /// <summary>
        /// Best-effort argument parsing: constant, parameter reference, or symbolic.
        /// Full expression parsing happens later in EquationParser.ParseExpression.
        /// </summary>
        private static Expression ParseArgument(string argStr, ModelManager modelManager)
        {
            if (double.TryParse(argStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double d))
                return new ConstantExpression(d);

            if (modelManager.Parameters.ContainsKey(argStr))
                return new ParameterExpression(argStr);

            return new SymbolicExpression(argStr);
        }
    }
}
