using System.Text.Json;
using ModelEditorApp.Models;

namespace ModelEditorApp.Services
{
    /// <summary>
    /// Manages saving and loading application session state
    /// </summary>
    public class SessionManager
    {
        private readonly string sessionFilePath;
        private const string SESSION_FILE_NAME = "session.json";

        public SessionManager()
        {
            // Store session file in user's AppData folder
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ModelEditor");
            
            Directory.CreateDirectory(appDataPath);
            sessionFilePath = Path.Combine(appDataPath, SESSION_FILE_NAME);
        }

        /// <summary>
        /// Saves the current session state
        /// </summary>
        public bool SaveSession(SessionState sessionState)
        {
            try
            {
                sessionState.LastSaved = DateTime.Now;
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };

                string json = JsonSerializer.Serialize(sessionState, options);
                File.WriteAllText(sessionFilePath, json);
                
                return true;
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Failed to save session: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads the saved session state
        /// </summary>
        public SessionState? LoadSession()
        {
            try
            {
                if (!File.Exists(sessionFilePath))
                {
                    return null;
                }

                string json = File.ReadAllText(sessionFilePath);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var sessionState = JsonSerializer.Deserialize<SessionState>(json, options);
                return sessionState;
            }
            catch (Exception ex)
            {
                // If session file is corrupted, delete it and start fresh
                System.Diagnostics.Debug.WriteLine($"Failed to load session: {ex.Message}");
                
                try
                {
                    if (File.Exists(sessionFilePath))
                    {
                        File.Delete(sessionFilePath);
                    }
                }
                catch { }
                
                return null;
            }
        }

        /// <summary>
        /// Clears the saved session
        /// </summary>
        public void ClearSession()
        {
            try
            {
                if (File.Exists(sessionFilePath))
                {
                    File.Delete(sessionFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear session: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a session file exists
        /// </summary>
        public bool SessionExists()
        {
            return File.Exists(sessionFilePath);
        }
    }
}