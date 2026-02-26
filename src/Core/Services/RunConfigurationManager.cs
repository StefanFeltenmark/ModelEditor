using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Core.Models;

namespace Core.Services
{
    /// <summary>
    /// Manages run configurations - saving, loading, and organizing
    /// </summary>
    public class RunConfigurationManager
    {
        private readonly string configurationsDirectory;
        private readonly Dictionary<string, RunConfiguration> configurations;
        
        public RunConfigurationManager(string? baseDirectory = null)
        {
            // Default to user's app data directory
            if (string.IsNullOrEmpty(baseDirectory))
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                baseDirectory = Path.Combine(appData, "OptimizationModeler", "Configurations");
            }

            configurationsDirectory = baseDirectory;
            configurations = new Dictionary<string, RunConfiguration>();

            // Ensure directory exists
            Directory.CreateDirectory(configurationsDirectory);
        }

        /// <summary>
        /// Gets all loaded configurations
        /// </summary>
        public IEnumerable<RunConfiguration> GetAll()
        {
            return configurations.Values.OrderBy(c => c.Name);
        }

        /// <summary>
        /// Gets a configuration by ID
        /// </summary>
        public RunConfiguration? GetById(string id)
        {
            return configurations.TryGetValue(id, out var config) ? config : null;
        }

        /// <summary>
        /// Creates a new configuration
        /// </summary>
        public RunConfiguration Create(string name)
        {
            var config = new RunConfiguration
            {
                Name = name,
                CreatedDate = DateTime.Now,
                LastModified = DateTime.Now
            };

            configurations[config.Id] = config;
            return config;
        }

        /// <summary>
        /// Saves a configuration to disk
        /// </summary>
        public void Save(RunConfiguration configuration)
        {
            configuration.LastModified = DateTime.Now;
            configurations[configuration.Id] = configuration;

            var filePath = GetConfigurationFilePath(configuration.Id);
            var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Loads all configurations from disk
        /// </summary>
        public void LoadAll()
        {
            configurations.Clear();

            if (!Directory.Exists(configurationsDirectory))
            {
                return;
            }

            var files = Directory.GetFiles(configurationsDirectory, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var config = JsonSerializer.Deserialize<RunConfiguration>(json);
                    
                    if (config != null)
                    {
                        configurations[config.Id] = config;
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue loading other configs
                    Console.WriteLine($"Error loading configuration from {file}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Deletes a configuration
        /// </summary>
        public bool Delete(string id)
        {
            if (!configurations.ContainsKey(id))
            {
                return false;
            }

            configurations.Remove(id);

            var filePath = GetConfigurationFilePath(id);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return true;
        }

        /// <summary>
        /// Renames a configuration
        /// </summary>
        public bool Rename(string id, string newName)
        {
            if (!configurations.TryGetValue(id, out var config))
            {
                return false;
            }

            config.Name = newName;
            config.LastModified = DateTime.Now;
            Save(config);

            return true;
        }

        /// <summary>
        /// Duplicates a configuration
        /// </summary>
        public RunConfiguration? Duplicate(string id)
        {
            if (!configurations.TryGetValue(id, out var original))
            {
                return null;
            }

            var clone = original.Clone();
            configurations[clone.Id] = clone;
            Save(clone);

            return clone;
        }

        /// <summary>
        /// Exports a configuration to a specific file
        /// </summary>
        public void Export(RunConfiguration configuration, string filePath)
        {
            var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Imports a configuration from a file
        /// </summary>
        public RunConfiguration? Import(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<RunConfiguration>(json);

            if (config != null)
            {
                // Assign new ID to avoid conflicts
                config.Id = Guid.NewGuid().ToString();
                configurations[config.Id] = config;
                Save(config);
            }

            return config;
        }

        /// <summary>
        /// Gets recent configurations (by last run date)
        /// </summary>
        public IEnumerable<RunConfiguration> GetRecent(int count = 5)
        {
            return configurations.Values
                .Where(c => c.LastRun.HasValue)
                .OrderByDescending(c => c.LastRun)
                .Take(count);
        }

        private string GetConfigurationFilePath(string id)
        {
            return Path.Combine(configurationsDirectory, $"{id}.json");
        }
    }
}