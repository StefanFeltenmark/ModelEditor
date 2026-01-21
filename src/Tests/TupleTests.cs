using Xunit;
using Core;
using Core.Models;

namespace Tests
{
    public class TupleTests : TestBase
    {
        [Fact]
        public void Parse_SimpleTupleSchema_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Product {
                    string name;
                    float cost;
                    float price;
                }
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Single(manager.TupleSchemas);
            
            var schema = manager.TupleSchemas["Product"];
            Assert.NotNull(schema);
            Assert.Equal(3, schema.Fields.Count);
            Assert.Equal(VariableType.String, schema.Fields["name"]);
            Assert.Equal(VariableType.Float, schema.Fields["cost"]);
            Assert.Equal(VariableType.Float, schema.Fields["price"]);
        }

        [Fact]
        public void Parse_TupleSet_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Product {
                    string name;
                    float cost;
                }
                
                {Product} products = ...;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Single(manager.TupleSets);
            
            var tupleSet = manager.TupleSets["products"];
            Assert.NotNull(tupleSet);
            Assert.Equal("Product", tupleSet.SchemaName);
            Assert.True(tupleSet.IsExternal);
        }

        [Fact]
        public void Parse_TupleFieldAccess_InExpression_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            
            // Setup tuple schema
            var schema = new TupleSchema("Product");
            schema.AddField("cost", VariableType.Float);
            schema.AddField("price", VariableType.Float);
            manager.AddTupleSchema(schema);
            
            // Setup tuple set with data
            var tupleSet = new TupleSet("products", "Product", false);
            var instance1 = new TupleInstance("Product");
            instance1.SetValue("cost", 10.0);
            instance1.SetValue("price", 15.0);
            tupleSet.AddInstance(instance1);
            
            var instance2 = new TupleInstance("Product");
            instance2.SetValue("cost", 20.0);
            instance2.SetValue("price", 30.0);
            tupleSet.AddInstance(instance2);
            
            manager.AddTupleSet(tupleSet);
            
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..2;
                var float x[I];
                
                revenue: sum(i in I) products[i].price * x[i] <= 1000;
            ";

            // Act
            var result = parser.Parse(input);
            parser.ExpandIndexedEquations(result);

            // Assert
            AssertNoErrors(result);
            Assert.Single(manager.Equations);
            
            var equation = manager.Equations[0];
            var (coefficients, constant) = equation.Evaluate(manager);
            
            // products[1].price * x1 + products[2].price * x2
            Assert.Equal(15.0, coefficients["x1"]); // price of product 1
            Assert.Equal(30.0, coefficients["x2"]); // price of product 2
        }

        [Fact]
        public void Parse_ComplexTupleModel_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Product {
                    string name;
                    float cost;
                    float price;
                    int minProd;
                }
                
                int n = 3;
                range Products = 1..n;
                {Product} productData = ...;
                
                dvar float+ production[Products];
                
                maximize sum(p in Products) (productData[p].price - productData[p].cost) * production[p];
                
                forall(p in Products)
                    minProduction:
                        production[p] >= productData[p].minProd;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Single(manager.TupleSchemas);
            Assert.Single(manager.TupleSets);
            Assert.NotNull(manager.Objective);
            Assert.Single(manager.IndexedEquationTemplates);
        }
    }
}