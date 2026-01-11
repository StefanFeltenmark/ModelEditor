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

        public override string ToString()
        {
            return $"{Name} = {StartIndex}..{EndIndex}";
        }
    }
}