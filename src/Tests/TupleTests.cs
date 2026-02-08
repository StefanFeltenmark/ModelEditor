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
            Assert.Equal("products", tupleSet.Name);
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
            var tupleSet = new TupleSet("products", "Product", "I", false);
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
            
            // Setup tuple schema BEFORE parsing
            var schema = new TupleSchema("Product");
            schema.AddField("name", VariableType.String);
            schema.AddField("cost", VariableType.Float);
            schema.AddField("price", VariableType.Float);
            schema.AddField("minProd", VariableType.Integer);
            manager.AddTupleSchema(schema);
            
            // Setup tuple set with actual data
            var tupleSet = new TupleSet("productData", "Product", "Products", false);
            
            var instance1 = new TupleInstance("Product");
            instance1.SetValue("name", "Product1");
            instance1.SetValue("cost", 10.0);
            instance1.SetValue("price", 15.0);
            instance1.SetValue("minProd", 5);
            tupleSet.AddInstance(instance1);
            
            var instance2 = new TupleInstance("Product");
            instance2.SetValue("name", "Product2");
            instance2.SetValue("cost", 20.0);
            instance2.SetValue("price", 30.0);
            instance2.SetValue("minProd", 10);
            tupleSet.AddInstance(instance2);
            
            var instance3 = new TupleInstance("Product");
            instance3.SetValue("name", "Product3");
            instance3.SetValue("cost", 15.0);
            instance3.SetValue("price", 25.0);
            instance3.SetValue("minProd", 8);
            tupleSet.AddInstance(instance3);
            
            manager.AddTupleSet(tupleSet);
            
            var parser = CreateParser(manager);
            string input = @"
                int n = 3;
                range Products = 1..n;
                
                var float production[Products];
                
                maximize sum(p in Products) (productData[p].price - productData[p].cost) * production[p];
                
                subject to{
                forall(p in Products)
                    minProduction:
                        production[p] >= productData[p].minProd;
                }
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            if (result.HasErrors)
            {
                var errors = string.Join("\n", result.GetErrorMessages());
                Assert.True(false, $"Parsing failed with errors:\n{errors}");
            }
            
            Assert.Single(manager.TupleSets); // productData
            Assert.True(manager.IndexedVariables.ContainsKey("production"));
            
            // Verify the tuple set has the right data
            var loadedTupleSet = manager.TupleSets["productData"];
            Assert.Equal(3, loadedTupleSet.Instances.Count);
            Assert.Equal("Products", loadedTupleSet.IndexSetName);
            Assert.True(loadedTupleSet.IsIndexed);
        }

        [Fact(Skip = "Requires full tuple field access and dvar implementation")]
        public void Parse_ComplexTupleModel_WithTupleFieldAccessInObjective()
        {
            // This test will be enabled once tuple field access is fully implemented
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
                
                subject to{
                forall(p in Products)
                    minProduction:
                        production[p] >= productData[p].minProd;
                }
            ";

            var result = parser.Parse(input);
            
            AssertNoErrors(result);
            Assert.Single(manager.TupleSchemas);
            Assert.Single(manager.TupleSets);
            Assert.NotNull(manager.Objective);
            Assert.Single(manager.IndexedEquationTemplates);
        }
    }
}