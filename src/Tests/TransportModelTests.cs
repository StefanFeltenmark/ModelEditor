using Xunit;
using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Unit tests for parsing the Transport model.
    /// Based on Data\Transport.mod — the classic transportation / assignment problem:
    ///   - Integer parameters and ranges
    ///   - 2D external parameter c[I][J]
    ///   - 2D decision variable x[i in I][j in J]
    ///   - minimize objective with nested summations
    ///   - subject to block with forall constraints
    ///
    /// Note: the parser uses bracket-per-dimension syntax for parameters ([I][J])
    /// and multi-bracket syntax for dvars ([i in I][j in J]).
    /// </summary>
    public class TransportModelTests : TestBase
    {
        private const string TransportModel = @"
int N = 4;
int M = 4;

range I = 1..N;
range J = 1..M;

float c[I][J] = ...;

dvar float+ x[i in I][j in J];

minimize sum(i in I) sum(j in J) c[i,j]*x[i,j];

subject to
{
 forall(j in J)
   sum(i in I) x[i,j] == 1;

 forall(i in I)
   sum(j in J) x[i,j] == 1;
}
";

        [Fact]
        public void Transport_FullModel_ShouldParseWithoutErrors()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            var result = parser.Parse(TransportModel);

            AssertNoErrors(result);
        }

        [Fact]
        public void Transport_IntegerParameters_ShouldBeRegistered()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            parser.Parse(TransportModel);

            Assert.True(manager.Parameters.ContainsKey("N"));
            Assert.Equal(4, manager.Parameters["N"].Value);

            Assert.True(manager.Parameters.ContainsKey("M"));
            Assert.Equal(4, manager.Parameters["M"].Value);
        }

        [Fact]
        public void Transport_Ranges_ShouldSpanOneToFour()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            parser.Parse(TransportModel);

            Assert.True(manager.Ranges.ContainsKey("I"));
            var rangeI = manager.Ranges["I"].GetValues(manager).ToList();
            Assert.Equal(4, rangeI.Count);
            Assert.Equal(1, rangeI.First());
            Assert.Equal(4, rangeI.Last());

            Assert.True(manager.Ranges.ContainsKey("J"));
            Assert.Equal(4, manager.Ranges["J"].GetValues(manager).Count());
        }

        [Fact]
        public void Transport_CostParameter_ShouldBeExternal()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            var result = parser.Parse(TransportModel);

            AssertNoErrors(result);
            Assert.True(manager.Parameters.ContainsKey("c"),
                "External 2D parameter 'c' should be registered");
        }

        [Fact]
        public void Transport_DecisionVariable_ShouldBeNonNegative()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            var result = parser.Parse(TransportModel);

            AssertNoErrors(result);
            Assert.True(manager.IndexedVariables.ContainsKey("x"),
                "Decision variable 'x' should be registered");
            var x = manager.IndexedVariables["x"];
            Assert.Equal(0.0, x.LowerBound);
        }

        [Fact]
        public void Transport_Objective_ShouldBeMinimize()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            var result = parser.Parse(TransportModel);

            AssertNoErrors(result);
            Assert.NotNull(manager.Objective);
            Assert.Equal(ObjectiveSense.Minimize, manager.Objective.Sense);
        }

        [Fact]
        public void Transport_SubjectTo_ShouldProduceTwoForallBlocks()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            var result = parser.Parse(TransportModel);

            AssertNoErrors(result);
            Assert.Equal(2, manager.ForallStatements.Count);
        }

        [Fact]
        public void Transport_SuccessCount_ShouldCoverAllStatements()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            var result = parser.Parse(TransportModel);

            AssertNoErrors(result);
            // N, M, I, J, c, x, minimize, forall_1, forall_2 = 9 statements
            Assert.True(result.SuccessCount >= 9,
                $"Expected at least 9 successful statements, got {result.SuccessCount}. " +
                $"Errors: {string.Join("; ", result.GetErrorMessages())}");
        }

        [Fact]
        public void Transport_CommaSeparatedDimensions_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int N = 4;
                int M = 4;
                range I = 1..N;
                range J = 1..M;
                float c[I,J] = ...;
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.True(manager.Parameters.ContainsKey("c"),
                "2D parameter with comma-separated dimensions should be registered");
        }
    }
}
