using System;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace XafGitHubCopilot.Module.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCopilotSdk(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.Configure<CopilotOptions>(configuration.GetSection(CopilotOptions.SectionName));
            services.AddSingleton<CopilotChatService>();

            // Register the tools provider (singleton — tools are created lazily on first access).
            services.AddSingleton<CopilotToolsProvider>();

            // Register the IChatClient adapter so DevExpress DxAIChat / AIChatControl
            // can route messages through the GitHub Copilot SDK automatically.
            services.AddChatClient(sp =>
            {
                var service = sp.GetRequiredService<CopilotChatService>();
                var toolsProvider = sp.GetRequiredService<CopilotToolsProvider>();

                // Wire tools and system message into the service.
                service.Tools = toolsProvider.Tools;
                service.SystemMessage = CopilotChatDefaults.SystemPrompt;

                return new CopilotChatClient(service);
            });

            return services;
        }
    }
}
