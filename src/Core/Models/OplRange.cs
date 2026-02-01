using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models
{
    /// <summary>
    /// Represents an OPL range type (e.g., range Products = 1..100)
    /// </summary>
    public class OplRange
    {
        public string Name { get; set; }
        public Expression StartExpression { get; set; }
        public Expression EndExpression { get; set; }
        
        // Cached evaluated values
        private int? cachedStart;
        private int? cachedEnd;
        
        public OplRange(string name, Expression start, Expression end)
        {
            Name = name;
            StartExpression = start;
            EndExpression = end;
        }
        
        /// <summary>
        /// Gets the start value of the range
        /// </summary>
        public int GetStart(ModelManager modelManager)
        {
            if (!cachedStart.HasValue)
            {
                cachedStart = (int)Math.Round(StartExpression.Evaluate(modelManager));
            }
            return cachedStart.Value;
        }
        
        /// <summary>
        /// Gets the end value of the range
        /// </summary>
        public int GetEnd(ModelManager modelManager)
        {
            if (!cachedEnd.HasValue)
            {
                cachedEnd = (int)Math.Round(EndExpression.Evaluate(modelManager));
            }
            return cachedEnd.Value;
        }
        
        /// <summary>
        /// Gets the size of the range
        /// </summary>
        public int GetSize(ModelManager modelManager)
        {
            int start = GetStart(modelManager);
            int end = GetEnd(modelManager);
            return Math.Max(0, end - start + 1);
        }
        
        /// <summary>
        /// Gets all values in the range as an enumerable
        /// </summary>
        public IEnumerable<int> GetValues(ModelManager modelManager)
        {
            int start = GetStart(modelManager);
            int end = GetEnd(modelManager);
            
            if (start <= end)
            {
                return Enumerable.Range(start, end - start + 1);
            }
            else
            {
                return Enumerable.Empty<int>();
            }
        }
        
        /// <summary>
        /// Checks if a value is within the range
        /// </summary>
        public bool Contains(int value, ModelManager modelManager)
        {
            int start = GetStart(modelManager);
            int end = GetEnd(modelManager);
            return value >= start && value <= end;
        }
        
        /// <summary>
        /// Invalidates cached values (call when parameters change)
        /// </summary>
        public void InvalidateCache()
        {
            cachedStart = null;
            cachedEnd = null;
        }
        
        public override string ToString()
        {
            return $"range {Name} = {StartExpression}..{EndExpression}";
        }
    }
}
