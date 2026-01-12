using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Tests for indexed equation parsing and expansion
    /// </summary>
    public class IndexedEquationTests : TestBase
    {
        [Fact]
        public void Parse_IndexedEquation_ShouldExpand()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..3;
                equation constraint[I]: x[i] + y[i] <= 10;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, result.SuccessCount); // range + equation template
            
            // Should create 3 expanded equations
            var equations = manager.GetEquationsByBaseName("constraint");
            Assert.Equal(3, equations.Count);
            
            for (int i = 1; i <= 3; i++)
            {
                var eq = manager.GetIndexedEquation("constraint", i);
                Assert.NotNull(eq);
                Assert.Equal(RelationalOperator.LessThanOrEqual, eq.Operator);
                Assert.Equal(10, eq.Constant);
            }
        }

        [Fact]
        public void Parse_IndexedEquationWithCoefficients_ShouldExpandCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..2;
                equation eq[I]: 2*x[i] + 3*y[i] == 15;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            var eq1 = manager.GetIndexedEquation("eq", 1);
            Assert.NotNull(eq1);
            Assert.Equal(2, eq1.GetCoefficient("x1"));
            Assert.Equal(3, eq1.GetCoefficient("y1"));
            
            var eq2 = manager.GetIndexedEquation("eq", 2);
            Assert.NotNull(eq2);
            Assert.Equal(2, eq2.GetCoefficient("x2"));
            Assert.Equal(3, eq2.GetCoefficient("y2"));
        }

        [Fact]
        public void Parse_IndexedEquationWithUndeclaredIndexSet_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "equation constraint[I]: x[i] <= 10;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }

        [Fact]
        public void Parse_MultipleIndexedEquations_ShouldExpandAll()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..2;
                range J = 1..2;
                equation c1[I]: x[i] <= 10;
                equation c2[J]: y[j] >= 5;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, manager.GetEquationsByBaseName("c1").Count);
            Assert.Equal(2, manager.GetEquationsByBaseName("c2").Count);
        }

        [Fact]
        public void GetIndexedVariableCoefficient_ShouldReturnCorrectValue()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..2;
                equation eq[I]: 5*x[i] == 10;
            ";

            // Act
            parser.Parse(input);
            var equation = manager.GetIndexedEquation("eq", 1);

            // Assert
            Assert.NotNull(equation);
            var coeff = manager.GetIndexedVariableCoefficient(equation, "x", 1);
            Assert.Equal(5, coeff);
        }
    }
}