using Xunit;
using Xunit.Abstractions;
using Core;

namespace Tests
{
    public class PomParseTest
    {
        private readonly ITestOutputHelper output;

        public PomParseTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void POM_CollectAllParseErrors()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dataDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "Data"));

            var modPath = Path.Combine(dataDir, "POM.mod");
            var datPath = Path.Combine(dataDir, "POM.dat");

            output.WriteLine($"Looking for POM.mod at: {modPath}");
            output.WriteLine($"Looking for POM.dat at: {datPath}");

            Assert.True(File.Exists(modPath), $"POM.mod not found at {modPath}");
            Assert.True(File.Exists(datPath), $"POM.dat not found at {datPath}");

            var modText = File.ReadAllText(modPath);
            var datText = File.ReadAllText(datPath);

            var manager = new ModelManager();
            var parser = new EquationParser(manager);
            var dataParser = new DataFileParser(manager);
            var service = new ModelParsingService(manager, parser, dataParser);

            ParseResult result;
            try
            {
                result = service.ParseModel(
                    new List<string> { modText },
                    new List<string> { datText }
                );
            }
            catch (Exception ex)
            {
                output.WriteLine($"EXCEPTION during parsing: {ex.Message}");
                output.WriteLine(ex.StackTrace ?? "");
                return;
            }

            output.WriteLine($"=== PARSE SUMMARY ===");
            output.WriteLine($"Success: {result.Success}");
            output.WriteLine($"TotalSuccess: {result.TotalSuccess}");
            output.WriteLine($"TotalErrors: {result.TotalErrors}");
            output.WriteLine($"SummaryMessage: {result.SummaryMessage}");

            output.WriteLine($"");
            output.WriteLine($"=== ERRORS ({result.Errors.Count}) ===");
            if (result.Errors.Count == 0)
            {
                output.WriteLine("  (none)");
            }
            else
            {
                for (int i = 0; i < result.Errors.Count; i++)
                {
                    output.WriteLine($"  [{i + 1}] {result.Errors[i]}");
                }
            }

            output.WriteLine($"");
            output.WriteLine($"=== WARNINGS ({result.Warnings.Count}) ===");
            if (result.Warnings.Count == 0)
            {
                output.WriteLine("  (none)");
            }
            else
            {
                for (int i = 0; i < result.Warnings.Count; i++)
                {
                    output.WriteLine($"  [{i + 1}] {result.Warnings[i]}");
                }
            }

            // Write results to a file so they can be read even when test passes
            var lines = new List<string>
            {
                "=== PARSE SUMMARY ===",
                $"Success: {result.Success}",
                $"TotalSuccess: {result.TotalSuccess}",
                $"TotalErrors: {result.TotalErrors}",
                $"SummaryMessage: {result.SummaryMessage}",
                "",
                $"=== ERRORS ({result.Errors.Count}) ==="
            };
            if (result.Errors.Count == 0)
                lines.Add("  (none)");
            else
                for (int i = 0; i < result.Errors.Count; i++)
                    lines.Add($"  [{i + 1}] {result.Errors[i]}");

            lines.Add("");
            lines.Add($"=== WARNINGS ({result.Warnings.Count}) ===");
            if (result.Warnings.Count == 0)
                lines.Add("  (none)");
            else
                for (int i = 0; i < result.Warnings.Count; i++)
                    lines.Add($"  [{i + 1}] {result.Warnings[i]}");

            var outPath = Path.Combine(Path.GetTempPath(), "pom_parse_result.txt");
            File.WriteAllLines(outPath, lines);
        }
    }
}
