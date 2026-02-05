    namespace Core.Models
{
    /// <summary>
    /// Represents an index set with a name and range
    /// </summary>
    public class IndexSet
    {
        public string Name { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }

        

        public IndexSet(string name, int startIndex, int endIndex)
        {
            Name = name;
            StartIndex = startIndex;
            EndIndex = endIndex;
        
        }

        public int Count => EndIndex - StartIndex + 1;

        public bool Contains(int index)
        {
            return index >= StartIndex && index <= EndIndex;
        }

        public IEnumerable<int> GetIndices()
        {
            for (int i = StartIndex; i <= EndIndex; i++)
            {
                yield return i;
            }
        }

        /// <summary>
        /// Gets the zero-based position of an index value
        /// </summary>
        public int GetPosition(int indexValue)
        {
            if (!Contains(indexValue))
            {
                return -1;
            }
        
            return indexValue - StartIndex;
        }
    
        /// <summary>
        /// Gets the index value at a zero-based position
        /// </summary>
        public int GetValueAtPosition(int position)
        {
            if (position < 0 || position >= Count)
            {
                throw new IndexOutOfRangeException($"Position {position} is out of range [0..{Count-1}]");
            }
        
            return StartIndex + position;
        }

        public override string ToString()
        {
            return $"{Name} = {StartIndex}..{EndIndex}";
        }
    }
}