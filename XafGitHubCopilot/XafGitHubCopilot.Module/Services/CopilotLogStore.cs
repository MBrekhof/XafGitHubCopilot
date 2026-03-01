using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace XafGitHubCopilot.Module.Services
{
    public record CopilotLogEntry(DateTime Timestamp, LogLevel Level, string Category, string Message);

    public sealed class CopilotLogStore
    {
        private const int MaxEntries = 500;
        private readonly LinkedList<CopilotLogEntry> _entries = new();
        private readonly object _lock = new();

        public event Action<CopilotLogEntry> OnNewEntry;

        public void Add(CopilotLogEntry entry)
        {
            lock (_lock)
            {
                _entries.AddLast(entry);
                while (_entries.Count > MaxEntries)
                    _entries.RemoveFirst();
            }

            OnNewEntry?.Invoke(entry);
        }

        public List<CopilotLogEntry> GetEntries()
        {
            lock (_lock)
            {
                return new List<CopilotLogEntry>(_entries);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
            }
        }
    }
}
