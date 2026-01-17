    namespace ModelEditorApp.Models
{
    /// <summary>
    /// Represents the state of the application session
    /// </summary>
    public class SessionState
    {
        public List<TabState> Tabs { get; set; } = new List<TabState>();
        public int ActiveTabIndex { get; set; }
        public DateTime LastSaved { get; set; }
    }

    /// <summary>
    /// Represents the state of a single tab
    /// </summary>
    public class TabState
    {
        public string FilePath { get; set; } = string.Empty;
        public FileType FileType { get; set; }
        public bool HasUnsavedChanges { get; set; }
        public string Content { get; set; } = string.Empty; // Only for unsaved content
    }
}