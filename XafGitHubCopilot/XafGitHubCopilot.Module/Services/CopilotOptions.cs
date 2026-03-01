using System;

namespace XafGitHubCopilot.Module.Services
{
    public sealed class CopilotOptions
    {
        public const string SectionName = "Copilot";

        public string Model { get; set; } = "claude-sonnet-4.6";
        public string? GithubToken { get; set; }
        public string? CliPath { get; set; }
        public bool UseLoggedInUser { get; set; } = true;
        public bool Streaming { get; set; } = true;
    }
}
