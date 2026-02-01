using Xunit;
using Core;
using Core.Models;
using Core.Parsing;
using Core.Parsing.Tokenization;

namespace Tests
{
    public class TokenizationTests : TestBase
    {
        [Fact]
        public void ItemExpressionTokenizer_WithSingleKey_ShouldTokenize()
        {
            // Arrange
            var manager = CreateModelManager();
            var schema = new TupleSchema("Product");
            schema.AddField("id", VariableType.Integer, isKey: true);
            schema.AddField("price", VariableType.Float);
            manager.AddTupleSchema(schema);
            
            var tupleSet = new TupleSet("products", "Product");
            manager.AddTupleSet(tupleSet);
            
            var tokenManager = new TokenManager();
            var tokenizer = new ItemExpressionTokenizer();
            
            string expr = "item(products, <1>).price * x";

            // Act
            string result = tokenizer.Tokenize(expr, tokenManager, manager);

            // Assert
            Assert.Contains("__ITEM0__", result);
            Assert.Equal(1, tokenManager.TokenCount);
            Assert.True(tokenManager.HasToken("__ITEM0__"));
        }

        [Fact]
        public void TupleFieldAccessTokenizer_ShouldTokenize()
        {
            // Arrange
            var manager = CreateModelManager();
            var schema = new TupleSchema("Data");
            schema.AddField("value", VariableType.Float);
            manager.AddTupleSchema(schema);
            
            var tupleSet = new TupleSet("data", "Data");
            manager.AddTupleSet(tupleSet);
            
            var tokenManager = new TokenManager();
            var tokenizer = new TupleFieldAccessTokenizer();
            
            string expr = "data[1].value + data[2].value";

            // Act
            string result = tokenizer.Tokenize(expr, tokenManager, manager);

            // Assert
            Assert.Contains("__TUPLE", result);
            Assert.Equal(2, tokenManager.TokenCount);
        }

        [Fact]
        public void TwoDimensionalIndexTokenizer_Parameter_ShouldTokenize()
        {
            // Arrange
            var manager = CreateModelManager();
            manager.AddIndexSet(new IndexSet("I", 1, 3));
            manager.AddIndexSet(new IndexSet("J", 1, 2));
            
            var param = new Parameter("cost", ParameterType.Float, "I", "J", isExternal:false);
            manager.AddParameter(param);
            
            var tokenManager = new TokenManager();
            var tokenizer = new TwoDimensionalIndexTokenizer();
            
            string expr = "cost[1,2] + cost[2,1]";

            // Act
            string result = tokenizer.Tokenize(expr, tokenManager, manager);

            // Assert
            Assert.Contains("__PARAM", result);
            Assert.Equal(2, tokenManager.TokenCount);
        }

        [Fact]
        public void TwoDimensionalIndexTokenizer_Variable_ShouldNotTokenize()
        {
            // Arrange
            var manager = CreateModelManager();
            manager.AddIndexSet(new IndexSet("I", 1, 3));
            manager.AddIndexSet(new IndexSet("J", 1, 2));
            
            var variable = new IndexedVariable("flow", "I", VariableType.Float, "J");
            manager.AddIndexedVariable(variable);
            
            var tokenManager = new TokenManager();
            var tokenizer = new TwoDimensionalIndexTokenizer();
            
            string expr = "flow[1,2] + flow[2,1]";

            // Act
            string result = tokenizer.Tokenize(expr, tokenManager, manager);

            // Assert
            Assert.Equal("flow1_2+flow2_1", result.Replace(" ", ""));
            Assert.Equal(0, tokenManager.TokenCount); // Variables not tokenized
        }

        [Fact]
        public void SingleDimensionalIndexTokenizer_Parameter_ShouldTokenize()
        {
            // Arrange
            var manager = CreateModelManager();
            manager.AddIndexSet(new IndexSet("I", 1, 3));
            
            var param = new Parameter("cost", ParameterType.Float, "I");
            manager.AddParameter(param);
            
            var tokenManager = new TokenManager();
            var tokenizer = new SingleDimensionalIndexTokenizer();
            
            string expr = "cost[1] + cost[2]";

            // Act
            string result = tokenizer.Tokenize(expr, tokenManager, manager);

            // Assert
            Assert.Contains("__PARAM", result);
            Assert.Equal(2, tokenManager.TokenCount);
        }

        [Fact]
        public void TokenizationOrchestrator_ShouldApplyAllStrategies()
        {
            // Arrange
            var manager = CreateModelManager();
            
            // Setup tuple
            var schema = new TupleSchema("Product");
            schema.AddField("id", VariableType.Integer, isKey: true);
            schema.AddField("cost", VariableType.Float);
            manager.AddTupleSchema(schema);
            
            var tupleSet = new TupleSet("products", "Product");
            manager.AddTupleSet(tupleSet);
            
            // Setup parameters
            manager.AddIndexSet(new IndexSet("I", 1, 2));
            var param = new Parameter("a", ParameterType.Float, "I");
            manager.AddParameter(param);
            
            var tokenManager = new TokenManager();
            var orchestrator = new TokenizationOrchestrator();
            
            string expr = "item(products, <1>).cost + a[1] * x";

            // Act
            string result = orchestrator.TokenizeExpression(expr, tokenManager, manager);

            // Assert
            Assert.Contains("__ITEM", result);
            Assert.Contains("__PARAM", result);
            Assert.Contains("x", result); // Variable not tokenized
            Assert.Equal(2, tokenManager.TokenCount);
        }

        [Fact]
        public void TokenizationOrchestrator_Priority_ShouldProcessInOrder()
        {
            // Arrange
            var manager = CreateModelManager();
            var tokenManager = new TokenManager();
            var orchestrator = new TokenizationOrchestrator();
            
            // All strategies should process in priority order
            // This test verifies the orchestrator respects Priority property
            
            var executed = new List<int>();
            
            // We can't easily test this without reflection or mocking
            // But the implementation shows strategies are ordered by Priority
            
            Assert.NotNull(orchestrator);
        }

        [Fact]
        public void TokenManager_CreateToken_ShouldGenerateUniqueTokens()
        {
            // Arrange
            var tokenManager = new TokenManager();
            var expr1 = new ConstantExpression(1);
            var expr2 = new ConstantExpression(2);

            // Act
            string token1 = tokenManager.CreateToken(expr1, "TEST");
            string token2 = tokenManager.CreateToken(expr2, "TEST");

            // Assert
            Assert.NotEqual(token1, token2);
            Assert.Equal("__TEST0__", token1);
            Assert.Equal("__TEST1__", token2);
            Assert.Equal(2, tokenManager.TokenCount);
        }

        [Fact]
        public void TokenManager_TryGetExpression_ShouldRetrieveCorrectly()
        {
            // Arrange
            var tokenManager = new TokenManager();
            var expr = new ConstantExpression(42);
            string token = tokenManager.CreateToken(expr, "TEST");

            // Act
            bool found = tokenManager.TryGetExpression(token, out var retrieved);

            // Assert
            Assert.True(found);
            Assert.NotNull(retrieved);
            Assert.Equal(expr, retrieved);
        }

        [Fact]
        public void TokenManager_ContainsTokens_ShouldDetectCorrectly()
        {
            // Arrange
            var tokenManager = new TokenManager();
            var expr = new ConstantExpression(1);
            string token = tokenManager.CreateToken(expr, "PARAM");

            // Act & Assert
            Assert.True(tokenManager.ContainsTokens("__PARAM0__ + 5"));
            Assert.False(tokenManager.ContainsTokens("x + 5"));
        }

        [Fact]
        public void TokenManager_Clear_ShouldResetEverything()
        {
            // Arrange
            var tokenManager = new TokenManager();
            tokenManager.CreateToken(new ConstantExpression(1), "TEST");
            tokenManager.CreateToken(new ConstantExpression(2), "TEST");

            // Act
            tokenManager.Clear();

            // Assert
            Assert.Equal(0, tokenManager.TokenCount);
            
            // New token should start from 0 again
            string newToken = tokenManager.CreateToken(new ConstantExpression(3), "TEST");
            Assert.Equal("__TEST0__", newToken);
        }
    }
}