using Core;

namespace Tests
{
    /// <summary>
    /// Base class for parser tests with common setup
    /// </summary>
    public abstract class TestBase
    {
        protected ModelManager CreateModelManager()
        {
            return new ModelManager();
        }

        protected EquationParser CreateParser(ModelManager? manager = null)
        {
            manager ??= CreateModelManager();
            return new EquationParser(manager);
        }

        protected void AssertNoErrors(ParseSessionResult result)
        {
            Assert.False(result.HasErrors, $"Expected no errors, but got: {string.Join("; ", result.Errors)}");
        }

        protected void AssertHasError(ParseSessionResult result, string? expectedErrorFragment = null)
        {
            Assert.True(result.HasErrors, "Expected parsing errors, but got none");
            
            if (!string.IsNullOrEmpty(expectedErrorFragment))
            {
                Assert.Contains(result.Errors, e => e.Message.Contains(expectedErrorFragment, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}