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

        protected void AssertHasError(ParseSessionResult result)
        {
            Assert.True(result.HasErrors, "Expected parsing errors but got none");
        }

        protected void AssertHasError(ParseSessionResult result, string expectedErrorText)
        {
            Assert.True(result.HasErrors, "Expected parsing errors but got none");
            var errors = string.Join(", ", result.GetErrorMessages());
            Assert.Contains(expectedErrorText, errors, StringComparison.OrdinalIgnoreCase);
        }
    }
}