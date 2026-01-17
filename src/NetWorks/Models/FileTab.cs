namespace ModelEditorApp.Models
{
    /// <summary>
    /// Represents a single file tab with its editor and metadata
    /// </summary>
    public class FileTab
    {
        public RichTextBox Editor { get; }
        public string FilePath { get; set; }
        public bool HasUnsavedChanges { get; set; }
        public FileType Type { get; set; }
        public string DisplayName => GetDisplayName();

        public FileTab(FileType type = FileType.Model)
        {
            Editor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11F),
                WordWrap = false,
                BorderStyle = BorderStyle.None
            };
            
            FilePath = string.Empty;
            HasUnsavedChanges = false;
            Type = type;
        }

        private string GetDisplayName()
        {
            string name = string.IsNullOrEmpty(FilePath) 
                ? "Untitled" 
                : Path.GetFileName(FilePath);
            
            string modified = HasUnsavedChanges ? "*" : "";
            return $"{name}{modified}";
        }

        public string GetFileExtension()
        {
            return Type == FileType.Model ? ".mod" : ".dat";
        }
    }

    public enum FileType
    {
        Model,
        Data
    }
}