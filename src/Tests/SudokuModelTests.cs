using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Unit tests for the Sudoku model (Data/Sudoku.mod + Sudoku.dat).
    ///
    /// The model encodes a 9×9 Sudoku puzzle as an ILP:
    ///   - z[i,j,k] = 1 if cell (i,j) contains digit k
    ///   - Given[i,j,k] = 1 if that assignment matches a clue (used in the objective)
    ///
    /// Constraints:
    ///   - Each digit k appears exactly once in every row    (9×9 = 81 constraints)
    ///   - Each digit k appears exactly once in every column (9×9 = 81 constraints)
    ///   - Each digit k appears exactly once in every 3×3 box (3×3×9 = 81 constraints)
    ///   Total: 243 constraints
    /// </summary>
    public class SudokuModelTests : TestBase
    {
        // Sudoku.mod — verbatim from Data/Sudoku.mod
        private const string SudokuModel = @"
range I = 1..9;
range J = 1..9;
range K = 1..9;
range L = 1..3;
range M = 1..3;

dvar bool z[I,J,K];

int Given[I,J,K] = ...;

maximize sum(i in I) sum(j in J) sum(k in K) Given[i,j,k]*z[i,j,k];

subject to
{
    forall(i in I, k in K)
       sum(j in J) z[i,j,k] == 1;

    forall(j in J, k in K)
       sum(i in I) z[i,j,k] == 1;

    forall(l in L, m in M, k in K)
        sum(i in I: i > (l-1)*3 && i <= l*3) sum(j in J: j > (m-1)*3 && j <= m*3) z[i,j,k] == 1;
}
";

        // Sudoku.dat — verbatim from Data/Sudoku.dat
        // Each row is a 9-element list; 0 = empty cell, 1-9 = given digit.
        private const string SudokuData = @"
Given = [[0, 3, 0, 0, 0, 0, 0, 0, 0]
[0, 0, 0, 1, 9, 5, 0, 0, 0]
[0, 0, 8, 0, 0, 0, 0, 6, 0]
[8, 0, 0, 0, 6, 0, 0, 0, 0]
[4, 0, 0, 8, 0, 0, 0, 0, 1]
[0, 0, 0, 0, 2, 0, 0, 0, 1]
[0, 6, 0, 0, 0, 0, 2, 8, 0]
[0, 0, 0, 4, 1, 9, 0, 0, 5]
[0, 0, 0, 0, 0, 0, 0, 7, 0]
];
";

        // Variant without an external parameter so expansion tests can run stand-alone.
        // The three forall blocks are identical to SudokuModel — this isolates constraint
        // expansion from the 3D-array data-loading limitation.
        private const string SudokuModelNoExternal = @"
range I = 1..9;
range J = 1..9;
range K = 1..9;
range L = 1..3;
range M = 1..3;

dvar bool z[I,J,K];

subject to
{
    forall(i in I, k in K)
       sum(j in J) z[i,j,k] == 1;

    forall(j in J, k in K)
       sum(i in I) z[i,j,k] == 1;

    forall(l in L, m in M, k in K)
        sum(i in I: i > (l-1)*3 && i <= l*3) sum(j in J: j > (m-1)*3 && j <= m*3) z[i,j,k] == 1;
}
";

        // ── 1. Parsing (model text only) ────────────────────────────────────────

        [Fact]
        public void Sudoku_ModelParsing_ShouldSucceedWithoutErrors()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            var result = parser.Parse(SudokuModel);

            AssertNoErrors(result);
        }

        [Fact]
        public void Sudoku_Ranges_ShouldBeCorrect()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            parser.Parse(SudokuModel);

            // Primary digit sets: 1..9
            foreach (var name in new[] { "I", "J", "K" })
            {
                Assert.True(manager.IndexSets.ContainsKey(name), $"Range '{name}' should be registered");
                Assert.Equal(1, manager.IndexSets[name].StartIndex);
                Assert.Equal(9, manager.IndexSets[name].EndIndex);
            }

            // Box subdivision sets: 1..3
            foreach (var name in new[] { "L", "M" })
            {
                Assert.True(manager.IndexSets.ContainsKey(name), $"Range '{name}' should be registered");
                Assert.Equal(1, manager.IndexSets[name].StartIndex);
                Assert.Equal(3, manager.IndexSets[name].EndIndex);
            }
        }

        [Fact]
        public void Sudoku_DecisionVariable_ShouldBeBoolean()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            parser.Parse(SudokuModel);

            Assert.True(manager.IndexedVariables.ContainsKey("z"), "Variable 'z' should be registered");
            Assert.Equal(VariableType.Boolean, manager.IndexedVariables["z"].Type);
        }

        [Fact]
        public void Sudoku_Objective_ShouldBeMaximize()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            parser.Parse(SudokuModel);

            Assert.NotNull(manager.Objective);
            Assert.Equal(ObjectiveSense.Maximize, manager.Objective.Sense);
        }

        [Fact]
        public void Sudoku_ModelParsing_ShouldRegisterThreeForallBlocks()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            parser.Parse(SudokuModel);

            Assert.Equal(3, manager.ForallStatements.Count);
        }

        // ── 2. Expansion (model + data via ModelParsingService) ─────────────────

        [Fact]
        public void Sudoku_WithData_ShouldExpandTo243Constraints()
        {
            // 81 row + 81 column + 81 box = 243 constraints.
            var (manager, _) = ParseAndExpand(SudokuModelNoExternal);

            Assert.Equal(243, manager.Equations.Count);
        }

        [Fact]
        public void Sudoku_AllConstraints_ShouldBeEquality()
        {
            var (manager, _) = ParseAndExpand(SudokuModelNoExternal);

            Assert.All(manager.Equations, eq =>
                Assert.Equal(RelationalOperator.Equal, eq.Operator));
        }

        [Fact]
        public void Sudoku_AllConstraints_ShouldHaveRhsOfOne()
        {
            var (manager, _) = ParseAndExpand(SudokuModelNoExternal);

            Assert.All(manager.Equations, eq =>
                Assert.Equal(1.0, eq.Constant.Evaluate(manager), precision: 9));
        }

        [Fact]
        public void Sudoku_AllConstraints_ShouldEachCoverNineVariables()
        {
            // Every row / column / box constraint sums exactly 9 binary variables.
            var (manager, _) = ParseAndExpand(SudokuModelNoExternal);

            Assert.All(manager.Equations, eq =>
                Assert.Equal(9, eq.Coefficients.Count));
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses model text and expands all forall/indexed templates.
        /// Returns the manager and the expansion result for optional error inspection.
        /// </summary>
        private static (ModelManager manager, ParseSessionResult expansion) ParseAndExpand(string modelText)
        {
            var manager = new ModelManager();
            var parser = new EquationParser(manager);
            var parseResult = parser.Parse(modelText);
            Assert.False(parseResult.HasErrors,
                $"Parse errors: {string.Join("; ", parseResult.GetErrorMessages())}");

            var expansion = new ParseSessionResult();
            parser.ExpandAllTemplates(expansion);
            return (manager, expansion);
        }
    }
}
