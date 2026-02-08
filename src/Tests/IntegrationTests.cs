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
                objective: 2*x + 3*y1 == 100;
                capacity[i in I]: y[i] <= T;
                constraint: x + y1 >= 5;
            ";

            // Act
            var result = parser.Parse(input);
            parser.ExpandIndexedEquations(new ParseSessionResult());

            // Assert
            AssertNoErrors(result);
            
            // Verify parameters
            Assert.Equal(3, manager.Parameters.Count);
            var tParam = manager.Parameters["T"];
            Assert.NotNull(tParam);
            Assert.Equal(10, tParam.Value);
            
            // Verify index sets
            Assert.Equal(2, manager.IndexSets.Count);
            
            // Verify variables
            Assert.Equal(3, manager.IndexedVariables.Count);
            Assert.True(manager.IndexedVariables.ContainsKey("x"));
            Assert.True(manager.IndexedVariables.ContainsKey("y"));
            Assert.True(manager.IndexedVariables.ContainsKey("z"));
            
            // Verify equations (10 from capacity[i in I] + 2 regular)
            Assert.Equal(12, manager.Equations.Count);
            
            // Check that we have equations with the right base names
            Assert.Single(manager.Equations.Where(e => e.Label == "objective"));
            Assert.Single(manager.Equations.Where(e => e.Label == "constraint"));
            Assert.Equal(10, manager.Equations.Count(e => e.BaseName == "capacity"));
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
                var float x; // variable comment
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
                eq[i in I]: x + y[i] == T;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(5, result.SuccessCount);
        }

       
    }
}