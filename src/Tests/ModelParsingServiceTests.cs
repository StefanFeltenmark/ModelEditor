using Core;
using Core.Models;

namespace Tests
{
    public class ModelParsingServiceTests : TestBase
    {
        [Fact]
        public void ParseModel_WithValidModel_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            var dataParser = new DataFileParser(manager);
            var service = new ModelParsingService(manager, parser, dataParser);

            var modelTexts = new List<string>
            {
                @"
                range I = 1..3;
                var float x[I];
                constraint[i in I]: x[i] >= 0;
                "
            };

            // Act
            var result = service.ParseModel(modelTexts, new List<string>());

            // Assert
            Assert.True(result.Success);
            Assert.True(result.TotalSuccess > 0);
            Assert.Equal(0, result.TotalErrors);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ParseModel_WithNoModelFiles_ShouldFail()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            var dataParser = new DataFileParser(manager);
            var service = new ModelParsingService(manager, parser, dataParser);

            // Act
            var result = service.ParseModel(new List<string>(), new List<string>());

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No model files", result.Errors[0]);
        }

        [Fact]
        public void ParseModel_WithMissingExternalParameters_ShouldReportError()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            var dataParser = new DataFileParser(manager);
            var service = new ModelParsingService(manager, parser, dataParser);

            var modelTexts = new List<string>
            {
                @"
                range I = 1..3;
                float cost[I] = ...;
                var float x[I];
                "
            };

            // Act
            var result = service.ParseModel(modelTexts, new List<string>());

            // Assert
            Assert.False(result.Success);
            Assert.True(result.TotalErrors > 0);
            Assert.Contains(result.Errors, e => e.Contains("cost"));
        }

        [Fact]
        public void ParseModel_WithDataFile_ShouldPopulateParameters()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            var dataParser = new DataFileParser(manager);
            var service = new ModelParsingService(manager, parser, dataParser);

            var modelTexts = new List<string>
            {
                @"
                range I = 1..3;
                float cost[I] = ...;
                var float x[I];
                budget: sum(i in I) cost[i]*x[i] <= 100;
                "
            };

            var dataTexts = new List<string>
            {
                "cost = [10, 20, 30];"
            };

            // Act
            var result = service.ParseModel(modelTexts, dataTexts);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.TotalErrors);
            Assert.NotNull(manager.Parameters["cost"]);
        }

        [Fact]
        public void ParseModel_WithSyntaxError_ShouldReportError()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            var dataParser = new DataFileParser(manager);
            var service = new ModelParsingService(manager, parser, dataParser);

            var modelTexts = new List<string>
            {
                "invalid syntax here!!!"
            };

            // Act
            var result = service.ParseModel(modelTexts, new List<string>());

            // Assert
            Assert.False(result.Success);
            Assert.True(result.TotalErrors > 0);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void ParseModel_MultipleFiles_ShouldParseAll()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            var dataParser = new DataFileParser(manager);
            var service = new ModelParsingService(manager, parser, dataParser);

            var modelTexts = new List<string>
            {
                "int T = 10;",
                "range I = 1..T;",
                "var float x[I];"
            };

            // Act
            var result = service.ParseModel(modelTexts, new List<string>());

            // Assert
            Assert.True(result.Success);
            Assert.Equal(3, result.TotalSuccess);
        }
    }
}