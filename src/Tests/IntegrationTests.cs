using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Integration tests for complex parsing scenarios
    /// </summary>
    public class IntegrationTests : TestBase
    {
        [Fact]
        public void Parse_CompleteModel_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                // Define parameters
                int T = 10;
                float rate = 0.05;
                string modelName = ""TestModel"";
                
                // Define index sets
                range I = 1..T;
                range J = 1..5;
                
                // Define variables
                var float x;
                var int y[I];
                var bool z[J];
                
                // Define equations
                objective: 2*x + 3*y1 = 100;
                equation capacity[I]: y[i] <= T;
                constraint: x + y1 >= 5;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            // Verify parameters
            Assert.Equal(3, manager.Parameters.Count);
            Assert.Equal(10, manager.GetParameter("T")?.Value);
            
            // Verify index sets
            Assert.Equal(2, manager.IndexSets.Count);
            
            // Verify variables
            Assert.Equal(3, manager.IndexedVariables.Count);
            Assert.Equal(VariableType.Float, manager.GetVariableType("x"));
            Assert.Equal(VariableType.Integer, manager.GetVariableType("y"));
            Assert.Equal(VariableType.Boolean, manager.GetVariableType("z"));
            
            // Verify equations
            Assert.True(manager.ParsedEquations.Count >= 3);
            Assert.NotNull(manager.GetEquationByLabel("objective"));
            Assert.NotNull(manager.GetEquationByLabel("constraint"));
        }

        [Fact]
        public void Parse_WithComments_ShouldIgnoreComments()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                // This is a comment
                int T = 10; // inline comment
                range I = 1..T; // another comment
                var x; // variable comment
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
        }

        [Fact]
        public void Parse_ParameterReferenceInMultipleContexts_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int N = 5;
                int M = N;
                range I = 1..N;
                range J = 1..M;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(5, manager.GetParameter("M")?.Value);
            Assert.Equal(5, manager.IndexSets["I"].EndIndex);
            Assert.Equal(5, manager.IndexSets["J"].EndIndex);
        }

        [Fact]
        public void Parse_MixedStatements_ShouldParseInOrder()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                var x;
                int T = 10;
                range I = 1..T;
                var y[I];
                equation eq[I]: x + y[i] = T;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(5, result.SuccessCount);
        }

        [Fact]
        public void Parse_LargeIndexSet_ShouldHandleEfficiently()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..100;
                equation constraint[I]: x[i] <= 10;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(100, manager.GetEquationsByBaseName("constraint").Count);
        }
    }
}