using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses tuple set declarations
    /// Examples:
    ///   tupleset Routes = {(1,2), (1,3), (2,3)};
    ///   tupleset Arcs = {(i,j) | i in I, j in J, i < j};
    ///   tupleset Connections = ...;
    /// </summary>
    public class TupleSetParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;

        public TupleSetParser(ModelManager manager, ExpressionEvaluator eval)
        {
            modelManager = manager;
            evaluator = eval;
        }

        

     

      

      
    }
}