using Xunit;
using Xunit.Abstractions;
using Core;

namespace Tests
{
    public class PomFullFileTest : TestBase
    {
        private readonly ITestOutputHelper output;

        public PomFullFileTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void POM_FullFile_ShouldParse()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestFiles", "POM.mod");
            Assert.True(File.Exists(filePath), $"POM.mod not found at {Path.GetFullPath(filePath)}");

            var text = File.ReadAllText(filePath);
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            ParseSessionResult result;
            try
            {
                result = parser.Parse(text);
            }
            catch (Exception ex)
            {
                output.WriteLine($"EXCEPTION: {ex.Message}");
                output.WriteLine(ex.StackTrace ?? "");
                Assert.Fail($"Parser threw exception: {ex.Message}");
                return;
            }

            output.WriteLine($"Success: {result.SuccessCount}, Errors: {result.Errors.Count}");
            foreach (var e in result.Errors)
            {
                output.WriteLine($"  Line {e.LineNumber}: {e.Message}");
            }

            Assert.False(result.HasErrors,
                $"POM.mod had {result.Errors.Count} errors:\n{string.Join("\n", result.Errors.Select(e => $"  Line {e.LineNumber}: {e.Message}"))}");
        }
    }
}
