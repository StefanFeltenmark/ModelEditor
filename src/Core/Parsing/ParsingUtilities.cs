namespace Core.Parsing
{
    /// <summary>
    /// Shared parsing utilities used across all parsers
    /// </summary>
    public static class ParsingUtilities
    {
        public static string RemoveBlockComments(string text)
        {
            var result = new System.Text.StringBuilder();
            int i = 0;
            
            while (i < text.Length)
            {
                if (i < text.Length - 1 && text[i] == '/' && text[i + 1] == '*')
                {
                    int closeIndex = text.IndexOf("*/", i + 2);
                    if (closeIndex == -1) break;
                    
                    string commentBlock = text.Substring(i, closeIndex + 2 - i);
                    int lineBreaks = commentBlock.Count(c => c == '\n');
                    
                    for (int n = 0; n < lineBreaks; n++)
                        result.Append('\n');
                    
                    i = closeIndex + 2;
                }
                else
                {
                    result.Append(text[i]);
                    i++;
                }
            }
            
            return result.ToString();
        }
        
        public static List<string> SplitByCommaRespectingQuotes(string input)
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
                result.Add(current.ToString());

            return result;
        }
        
        public static int FindClosingBrace(string text, int startIndex)
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
                        return searchIndex;
                }
                searchIndex++;
            }

            return -1;
        }
        
        public static string RemoveSingleLineComments(string line)
        {
            return line.Split(new[] { "//" }, StringSplitOptions.None)[0];
        }
    }
}