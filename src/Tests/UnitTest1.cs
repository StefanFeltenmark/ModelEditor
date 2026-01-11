namespace Tests
{
    /// <summary>
    /// Quick smoke tests
    /// </summary>
    public class UnitTest1
    {
        [Fact]
        public void SmokeTest_ParserExists()
        {
            // Arrange
            var manager = new Core.ModelManager();
            var parser = new Core.EquationParser(manager);

            // Act & Assert
            Assert.NotNull(parser);
            Assert.NotNull(manager);
        }

        [Fact]
        public void SmokeTest_BasicParsing()
        {
            // Arrange
            var manager = new Core.ModelManager();
            var parser = new Core.EquationParser(manager);

            // Act
            var result = parser.Parse("int T = 10;");

            // Assert
            Assert.False(result.HasErrors);
            Assert.True(result.HasSuccess);
        }
    }
}
