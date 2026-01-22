namespace Core.Models
{
    /// <summary>
    /// Represents a set of tuples for complex indexing
    /// </summary>
    public class TupleSet
    {
        public string Name { get; }
        public int Dimension { get; }
        public List<Tuple<int, int>> TwoDimensionalTuples { get; }
        public List<Tuple<int, int, int>> ThreeDimensionalTuples { get; }
        public bool IsExternal { get; }
        
        // Add this property for structured tuple instances
        public List<TupleInstance> Instances { get; }
        
        /// <summary>
        /// Creates a 2D tuple set
        /// </summary>
        public TupleSet(string name, List<Tuple<int, int>> tuples)
        {
            Name = name;
            Dimension = 2;
            TwoDimensionalTuples = tuples ?? new List<Tuple<int, int>>();
            ThreeDimensionalTuples = new List<Tuple<int, int, int>>();
            Instances = new List<TupleInstance>();
            IsExternal = false;
        }
        
        /// <summary>
        /// Creates a 3D tuple set
        /// </summary>
        public TupleSet(string name, List<Tuple<int, int, int>> tuples)
        {
            Name = name;
            Dimension = 3;
            TwoDimensionalTuples = new List<Tuple<int, int>>();
            ThreeDimensionalTuples = tuples ?? new List<Tuple<int, int, int>>();
            Instances = new List<TupleInstance>();
            IsExternal = false;
        }
        
        /// <summary>
        /// Creates an external tuple set (data loaded from file)
        /// </summary>
        public TupleSet(string name, int dimension, bool isExternal = true)
        {
            Name = name;
            Dimension = dimension;
            TwoDimensionalTuples = new List<Tuple<int, int>>();
            ThreeDimensionalTuples = new List<Tuple<int, int, int>>();
            Instances = new List<TupleInstance>();
            IsExternal = isExternal;
        }
        
        public int Count => Dimension == 2 ? TwoDimensionalTuples.Count : ThreeDimensionalTuples.Count;
        
        public bool Contains(int val1, int val2)
        {
            if (Dimension != 2)
                throw new InvalidOperationException("This is not a 2D tuple set");
            return TwoDimensionalTuples.Contains(Tuple.Create(val1, val2));
        }
        
        public bool Contains(int val1, int val2, int val3)
        {
            if (Dimension != 3)
                throw new InvalidOperationException("This is not a 3D tuple set");
            return ThreeDimensionalTuples.Contains(Tuple.Create(val1, val2, val3));
        }
        
        public void AddTuple(int val1, int val2)
        {
            if (Dimension != 2)
                throw new InvalidOperationException("This is not a 2D tuple set");
            if (!TwoDimensionalTuples.Contains(Tuple.Create(val1, val2)))
            {
                TwoDimensionalTuples.Add(Tuple.Create(val1, val2));
            }
        }
        
        public void AddTuple(int val1, int val2, int val3)
        {
            if (Dimension != 3)
                throw new InvalidOperationException("This is not a 3D tuple set");
            if (!ThreeDimensionalTuples.Contains(Tuple.Create(val1, val2, val3)))
            {
                ThreeDimensionalTuples.Add(Tuple.Create(val1, val2, val3));
            }
        }
        
        public IEnumerable<Tuple<int, int>> GetTwoDimensionalTuples()
        {
            if (Dimension != 2)
                throw new InvalidOperationException("This is not a 2D tuple set");
            return TwoDimensionalTuples;
        }
        
        public IEnumerable<Tuple<int, int, int>> GetThreeDimensionalTuples()
        {
            if (Dimension != 3)
                throw new InvalidOperationException("This is not a 3D tuple set");
            return ThreeDimensionalTuples;
        }
        
        public void AddInstance(TupleInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            
            Instances.Add(instance);
        }
        
        public override string ToString()
        {
            if (IsExternal && Count == 0)
            {
                return $"{Name} (external {Dimension}D tuple set - not loaded)";
            }
            
            if (Dimension == 2)
            {
                var tupleStrings = TwoDimensionalTuples.Select(t => $"({t.Item1},{t.Item2})");
                return $"{Name} = {{{string.Join(", ", tupleStrings)}}}";
            }
            else
            {
                var tupleStrings = ThreeDimensionalTuples.Select(t => $"({t.Item1},{t.Item2},{t.Item3})");
                return $"{Name} = {{{string.Join(", ", tupleStrings)}}}";
            }
        }
    }
}