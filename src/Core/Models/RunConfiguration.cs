using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Core.Models
{
    /// <summary>
    /// Represents a complete run configuration with model, data, and settings files
    /// </summary>
    public class RunConfiguration
    {
        /// <summary>
        /// Unique identifier for the configuration
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// User-friendly name for the configuration
        /// </summary>
        public string Name { get; set; } = "New Configuration";

        /// <summary>
        /// Optional description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Model file paths (can be multiple files)
        /// </summary>
        public List<string> ModelFiles { get; set; } = new List<string>();

        /// <summary>
        /// Data file paths
        /// </summary>
        public List<string> DataFiles { get; set; } = new List<string>();

        /// <summary>
        /// Settings file path (optional)
        /// </summary>
        public string? SettingsFile { get; set; }

        /// <summary>
        /// When this configuration was created
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Last time this configuration was modified
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// Last time this configuration was run
        /// </summary>
        public DateTime? LastRun { get; set; }

        /// <summary>
        /// Working directory for relative paths
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Validates that all required files exist
        /// </summary>
        public bool ValidateFiles(out List<string> missingFiles)
        {
            missingFiles = new List<string>();

            foreach (var file in ModelFiles)
            {
                if (!File.Exists(file))
                {
                    missingFiles.Add(file);
                }
            }

            foreach (var file in DataFiles)
            {
                if (!File.Exists(file))
                {
                    missingFiles.Add(file);
                }
            }

            if (!string.IsNullOrEmpty(SettingsFile) && !File.Exists(SettingsFile))
            {
                missingFiles.Add(SettingsFile);
            }

            return missingFiles.Count == 0;
        }

        /// <summary>
        /// Creates a deep copy of this configuration
        /// </summary>
        public RunConfiguration Clone()
        {
            return new RunConfiguration
            {
                Id = Guid.NewGuid().ToString(), // New ID for clone
                Name = $"{Name} (Copy)",
                Description = Description,
                ModelFiles = new List<string>(ModelFiles),
                DataFiles = new List<string>(DataFiles),
                SettingsFile = SettingsFile,
                WorkingDirectory = WorkingDirectory,
                Metadata = new Dictionary<string, string>(Metadata)
            };
        }

        public override string ToString()
        {
            return Name;
        }
    }
}