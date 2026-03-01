using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace XafGitHubCopilot.Module.Services
{
    public sealed class CopilotLoggerProvider : ILoggerProvider
    {
        private static readonly Dictionary<string, string> TrackedCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            [typeof(CopilotToolsProvider).FullName] = "Tools",
            [typeof(CopilotChatService).FullName] = "ChatService",
            ["XafGitHubCopilot.Blazor.Server.Controllers.NavigationExecutorController"] = "NavExecutor",
            ["XafGitHubCopilot.Module.Controllers.ActiveViewTrackingController"] = "ViewTracker",
        };

        private readonly CopilotLogStore _store;

        public CopilotLoggerProvider(CopilotLogStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public ILogger CreateLogger(string categoryName)
        {
            if (TrackedCategories.TryGetValue(categoryName, out var shortName))
                return new CopilotLogger(_store, shortName);

            return NullLogger.Instance;
        }

        public void Dispose() { }

        private sealed class CopilotLogger : ILogger
        {
            private readonly CopilotLogStore _store;
            private readonly string _category;

            public CopilotLogger(CopilotLogStore store, string category)
            {
                _store = store;
                _category = category;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;

                var message = formatter(state, exception);

                // Filter out Copilot SDK internal trace noise — these flood the log
                // with hundreds of "[LoggerTraceSource]" entries per request.
                if (_category == "ChatService" && message.StartsWith("[LoggerTraceSource]", StringComparison.Ordinal))
                    return;

                if (exception != null)
                    message += $" | {exception.GetType().Name}: {exception.Message}";

                _store.Add(new CopilotLogEntry(DateTime.Now, logLevel, _category, message));
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }

        private sealed class NullLogger : ILogger
        {
            public static readonly NullLogger Instance = new();
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }
    }
}
