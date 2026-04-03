using Powel.Optimal.Domain.Infrastructure.Interfaces;

namespace Core.Solving
{
    /// <summary>
    /// Minimal IProxyLogger implementation that buffers solver log messages.
    /// </summary>
    internal class CplexAdapterLogger : IProxyLogger
    {
        private readonly List<string> _messages = new();
        public IReadOnlyList<string> Messages => _messages;

        public void Debug(string message) { }
        public void Debug(string message, Exception exception) { }
        public void Info(string message) => _messages.Add(message);
        public void Info(string message, Exception e) => _messages.Add(message);
        public void Warn(string message) => _messages.Add($"WARN: {message}");
        public void Warn(string message, Exception e) => _messages.Add($"WARN: {message}");
        public void Error(string message) => _messages.Add($"ERROR: {message}");
        public void Error(string message, Exception e) => _messages.Add($"ERROR: {message}");
        public void Fatal(string message) => _messages.Add($"FATAL: {message}");
        public void Fatal(string message, Exception e) => _messages.Add($"FATAL: {message}");
        public void InitializeTask(Guid logId, string title) { }
    }
}
