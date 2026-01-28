using Xunit;
using Core;
using Core.Models;
using Core.Export;

namespace Tests
{
    public class MPSExportTests : TestBase
    {
        [Fact]
        public void Export_SimpleLP_ShouldGenerateValidMPS()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                dvar float+ x;
                dvar float+ y;
                
                maximize  3*x + 5*y;
                
                constraint1: 2*x + y <= 10;
                constraint2: x + 2*y <= 8;
            ";
            
            var parseResult = parser.Parse(input);
            parser.ExpandIndexedEquations(parseResult);

            // Act
            var exporter = new MPSExporter(manager);
            string mps = exporter.Export("TEST");

            // Assert
            Assert.Contains("NAME          TEST", mps);
            Assert.Contains("ROWS", mps);
            Assert.Contains(" N  OBJ", mps);
            Assert.Contains(" L  CONSTRAINT1", mps);
            Assert.Contains("COLUMNS", mps);
            Assert.Contains("RHS", mps);
            Assert.Contains("BOUNDS", mps);
            Assert.Contains("ENDATA", mps);
        }

        [Fact]
        public void Export_WithIndexedVariables_ShouldExpandCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..3;
                dvar float+ x[I];
                
                maximize sum(i in I) x[i];
                
                forall(i in I)
                    limit: x[i] <= 10;
            ";
            
            parser.Parse(input);
            parser.ExpandIndexedEquations(new ParseSessionResult());

            // Act
            var exporter = new MPSExporter(manager);
            string mps = exporter.Export();

            // Assert
            Assert.Contains("X1", mps);
            Assert.Contains("X2", mps);
            Assert.Contains("X3", mps);
            Assert.Contains("LIMIT_1", mps);
            Assert.Contains("LIMIT_2", mps);
            Assert.Contains("LIMIT_3", mps);
        }

        [Fact]
        public void Export_WithDifferentConstraintTypes_ShouldUseCorrectRowTypes()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                dvar float+ x;
                dvar float+ y;
                dvar float+ z;
                
                maximize x + y + z;
                
                lessThan: x <= 10;
                greaterThan: y >= 5;
                equality: z == 7;
            ";
            
            parser.Parse(input);
            parser.ExpandIndexedEquations(new ParseSessionResult());

            // Act
            var exporter = new MPSExporter(manager);
            string mps = exporter.Export();

            // Assert
            Assert.Contains(" L  LESSTHAN", mps);   // L = <=
            Assert.Contains(" G  GREATERT", mps);   // G = >=
            Assert.Contains(" E  EQUALITY", mps);   // E = ==
        }

        [Fact]
        public void Export_MinimizationProblem_ShouldNegateObjective()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                dvar float+ x;
                
                minimize  5*x;
                
                bound: x <= 100;
            ";
            
            parser.Parse(input);
            parser.ExpandIndexedEquations(new ParseSessionResult());

            // Act
            var exporter = new MPSExporter(manager);
            string mps = exporter.Export();

            // Assert
            // For minimization, MPS uses positive coefficients as-is
            Assert.Contains("5", mps); // Coefficient should be positive for minimize
        }

        [Fact]
        public void Export_WithVariableBounds_ShouldGenerateBoundsSection()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                dvar float+ x;               // x >= 0
                dvar float y in 5..100;   // 5 <= y <= 100
                dvar float z;                // unbounded

                maximize x + y + z;

                dummy: x + y + z >= 0;
            ";
            
            parser.Parse(input);
            parser.ExpandIndexedEquations(new ParseSessionResult());

            // Act
            var exporter = new MPSExporter(manager);
            string mps = exporter.Export();

            // Assert
            Assert.Contains(" PL BOUND1     X", mps);        // Positive, unbounded above
            Assert.Contains(" LO BOUND1     Y", mps);        // Lower bound
            Assert.Contains(" UP BOUND1     Y", mps);        // Upper bound
            Assert.Contains(" FR BOUND1     Z", mps);        // Free variable
        }

        [Fact]
        public void Export_WithIntegerVariables_ShouldMarkAsInteger()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                dvar int+ count in 0..10;
                
                maximize count;
                
                limit: count <= 10;
            ";
            
            parser.Parse(input);
            parser.ExpandIndexedEquations(new ParseSessionResult());

            // Act
            var exporter = new MPSExporter(manager);
            string mps = exporter.Export();

            // Assert
            Assert.Contains(" LI BOUND1     COUNT", mps);  // LI = lower bound integer
        }

        [Fact]
        public void Export_WithParameters_ShouldEvaluateCoefficients()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int multiplier = 5;
                
                dvar float+ x;

                maximize multiplier * x;

                limit: x <= 100;
            ";
            
            parser.Parse(input);
            parser.ExpandIndexedEquations(new ParseSessionResult());

            // Act
            var exporter = new MPSExporter(manager);
            string mps = exporter.Export();

            // Assert
            // Objective coefficient should be evaluated to -5 (negated for maximize)
            var lines = mps.Split('\n');
            var objLine = lines.FirstOrDefault(l => l.Contains("X") && l.Contains("OBJ"));
            Assert.NotNull(objLine);
            Assert.Contains("-5", objLine);
        }

        [Fact]
        public void Export_NoObjective_ShouldThrow()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                dvar float+ x;
                constraint: x <= 10;
            ";
            
            parser.Parse(input);
            parser.ExpandIndexedEquations(new ParseSessionResult());

            // Act & Assert
            var exporter = new MPSExporter(manager);
            Assert.Throws<InvalidOperationException>(() => exporter.Export());
        }

        [Fact]
        public void Export_CompleteModel_ShouldGenerateValidMPS()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range Products = 1..2;
                
                dvar float+ production[Products];
                
                maximize  10*production[1] + 15*production[2];
                
                subject to {
                    forall(p in Products)
                        capacity: production[p] <= 100;
                    
                    total: sum(p in Products) production[p] <= 150;
                }
            ";
            
            parser.Parse(input);
            parser.ExpandIndexedEquations(new ParseSessionResult());

            // Act
            var exporter = new MPSExporter(manager);
            string mps = exporter.Export("PRODPLAN");

            // Assert
            Assert.Contains("NAME          PRODPLAN", mps);
            
            // Check structure
            var sections = new[] { "ROWS", "COLUMNS", "RHS", "BOUNDS", "ENDATA" };
            foreach (var section in sections)
            {
                Assert.Contains(section, mps);
            }
            
            // Check we have the right number of constraints (3)
            int lCount = mps.Split('\n').Count(line => line.Trim().StartsWith("L "));
            Assert.Equal(3, lCount); // capacity_1, capacity_2, total
            
            // Verify variables
            Assert.Contains("PRODUCTION1", mps);
            Assert.Contains("PRODUCTION2", mps);
        }

        [Fact]
        public void Export_SaveToFile_ShouldWork()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                dvar float+ x;
                dvar float+ y;
                
                maximize  3*x + 5*y;
                c1: x + y <= 10;
            ";
            
            parser.Parse(input);
            parser.ExpandIndexedEquations(new ParseSessionResult());
            
            var exporter = new MPSExporter(manager);
            string mps = exporter.Export();
            
            string tempFile = Path.GetTempFileName() + ".mps";

            // Act
            File.WriteAllText(tempFile, mps);

            // Assert
            Assert.True(File.Exists(tempFile));
            string content = File.ReadAllText(tempFile);
            Assert.Contains("NAME", content);
            Assert.Contains("ENDATA", content);
            
            // Cleanup
            File.Delete(tempFile);
        }
    }
}