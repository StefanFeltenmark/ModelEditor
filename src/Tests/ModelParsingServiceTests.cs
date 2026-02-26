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

        [Fact]
        public void ParseModel()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            var dataParser = new DataFileParser(manager);
            var service = new ModelParsingService(manager, parser, dataParser);

            string modelText1 = @"int N = 3;
int M = 4;
range I = 1..N;
range J = 1..M;
float cost[I][J] = ...;
float weight[I][J] = ...;
float w[I] = ...;
dvar float+ x[I,J];
maximize sum(i in I) sum(j in J) cost[i,j]*x[i,j];
subject to{
forall(i in I)
eq1:         
sum(j in J) weight[i,j]*x[i,j] <= w[i];
}";

            string dataText1 = @"cost = [[10, 20, 30, 40], [15, 25, 35, 45], [20, 30, 40, 50]];
weight = [[1, 2, 3, 4], [1.5, 2.5, 3.5, 4.5], [2, 3, 4, 5]];
w = [8 7 10];";

            // Act
            var result = parser.Parse(modelText1);

            var dataResult = dataParser.Parse(dataText1);

            var serviceResult = service.ParseModel([modelText1], [dataText1]);

            // Assert
            Assert.True(!result.HasErrors);
            Assert.Equal(10, result.SuccessCount);
        }
    }
}