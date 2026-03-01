using System;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
            services.AddSingleton<SchemaDiscoveryService>();
            services.AddSingleton<ActiveViewContext>();

            // Log store + logger provider for the AI log viewer panel.
            services.AddSingleton<CopilotLogStore>();
            services.AddSingleton<ILoggerProvider, CopilotLoggerProvider>();

            // Register the tools provider (singleton — tools are created lazily on first access).
            // Use a factory to handle optional services (INavigationService may not be registered on WinForms).
            services.AddSingleton<CopilotToolsProvider>(sp =>
                new CopilotToolsProvider(
                    sp,
                    sp.GetRequiredService<SchemaDiscoveryService>(),
                    sp.GetService<INavigationService>(),
                    sp.GetService<ActiveViewContext>()));

            // Register the IChatClient adapter so DevExpress DxAIChat / AIChatControl
            // can route messages through the GitHub Copilot SDK automatically.
            services.AddChatClient(sp =>
            {
                var service = sp.GetRequiredService<CopilotChatService>();
                var toolsProvider = sp.GetRequiredService<CopilotToolsProvider>();
                var schemaService = sp.GetRequiredService<SchemaDiscoveryService>();

                // Wire tools and system message into the service.
                service.Tools = toolsProvider.Tools;
                service.SystemMessage = schemaService.GenerateSystemPrompt();

                return new CopilotChatClient(service);
            });

            return services;
        }
    }
}
