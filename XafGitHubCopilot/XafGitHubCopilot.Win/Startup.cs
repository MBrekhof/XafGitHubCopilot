using System.Configuration;
using DevExpress.EntityFrameworkCore.Security;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ApplicationBuilder;
using DevExpress.ExpressApp.Design;
using DevExpress.ExpressApp.EFCore;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.Win;
using DevExpress.ExpressApp.Win.ApplicationBuilder;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using DevExpress.XtraEditors;
using Microsoft.EntityFrameworkCore;
using DevExpress.AIIntegration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XafGitHubCopilot.Module.Services;

namespace XafGitHubCopilot.Win
{
    public class ApplicationBuilder : IDesignTimeApplicationFactory
    {
        public static WinApplication BuildApplication(string connectionString)
        {
            // Build configuration from appsettings.json so we can register CopilotChatService.
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .Build();

            var builder = WinApplication.CreateBuilder();
            // Register logging (required by CopilotChatService).
            builder.Services.AddLogging();
            // Register the GitHub Copilot SDK chat service for DI.
            builder.Services.AddCopilotSdk(configuration);
            // Enable DevExpress AI infrastructure (required by AIChatControl).
            builder.Services.AddDevExpressAI();

            // Register the CopilotChatClient with the DevExpress desktop AI container
            // so that AIChatControl (Blazor Hybrid) can resolve IChatClient.
            var copilotOptions = Options.Create(
                configuration.GetSection(CopilotOptions.SectionName).Get<CopilotOptions>() ?? new CopilotOptions());
            var loggerFactory = LoggerFactory.Create(lb => { });
            var copilotService = new CopilotChatService(copilotOptions, loggerFactory.CreateLogger<CopilotChatService>());

            // System message is set after Build() when SchemaDiscoveryService is available.

            var copilotChatClient = new CopilotChatClient(copilotService);

            // Register CopilotChatClient as keyed service for the chat UI (AIChatControl uses ChatClientServiceKey).
            builder.Services.AddKeyedSingleton<IChatClient>("copilot", copilotChatClient);

            // Register with DevExpress desktop AI container for AIChatControl resolution.
            AIExtensionsContainerDesktop.Default.RegisterChatClient(copilotChatClient);
            // Register 3rd-party IoC containers (like Autofac, Dryloc, etc.)
            // builder.UseServiceProviderFactory(new DryIocServiceProviderFactory());
            // builder.UseServiceProviderFactory(new AutofacServiceProviderFactory());

            builder.UseApplication<XafGitHubCopilotWindowsFormsApplication>();
            builder.Modules
                .AddCloning()
                .AddConditionalAppearance()
                .AddFileAttachments()
                .AddReports(options =>
                {
                    options.EnableInplaceReports = true;
                    options.ReportDataType = typeof(DevExpress.Persistent.BaseImpl.EF.ReportDataV2);
                    options.ReportStoreMode = DevExpress.ExpressApp.ReportsV2.ReportStoreModes.XML;
                })
                .AddValidation(options =>
                {
                    options.AllowValidationDetailsAccess = false;
                })
                .Add<XafGitHubCopilot.Module.XafGitHubCopilotModule>()
                .Add<XafGitHubCopilotWinModule>();
            builder.ObjectSpaceProviders
                .AddSecuredEFCore(options =>
                {
                    options.PreFetchReferenceProperties();
                })
                    .WithDbContext<XafGitHubCopilot.Module.BusinessObjects.XafGitHubCopilotEFCoreDbContext>((application, options) =>
                    {
                        // Uncomment this code to use an in-memory database. This database is recreated each time the server starts. With the in-memory database, you don't need to make a migration when the data model is changed.
                        // Do not use this code in production environment to avoid data loss.
                        // We recommend that you refer to the following help topic before you use an in-memory database: https://docs.microsoft.com/en-us/ef/core/testing/in-memory
                        //options.UseInMemoryDatabase();
                        options.UseConnectionString(connectionString);
                    })
                .AddNonPersistent();
            builder.Security
                .UseIntegratedMode(options =>
                {
                    options.Lockout.Enabled = true;

                    options.RoleType = typeof(PermissionPolicyRole);
                    options.UserType = typeof(XafGitHubCopilot.Module.BusinessObjects.ApplicationUser);
                    options.UserLoginInfoType = typeof(XafGitHubCopilot.Module.BusinessObjects.ApplicationUserLoginInfo);
                    options.Events.OnSecurityStrategyCreated += securityStrategy =>
                    {
                        // Use the 'PermissionsReloadMode.NoCache' option to load the most recent permissions from the database once
                        // for every DbContext instance when secured data is accessed through this instance for the first time.
                        // Use the 'PermissionsReloadMode.CacheOnFirstAccess' option to reduce the number of database queries.
                        // In this case, permission requests are loaded and cached when secured data is accessed for the first time
                        // and used until the current user logs out.
                        // See the following article for more details: https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.Security.SecurityStrategy.PermissionsReloadMode.
                        ((SecurityStrategy)securityStrategy).PermissionsReloadMode = PermissionsReloadMode.NoCache;
                    };
                })
                .AddPasswordAuthentication();
            builder.AddBuildStep(application =>
            {
                application.ConnectionString = connectionString;
#if DEBUG
                if(System.Diagnostics.Debugger.IsAttached && application.CheckCompatibilityType == CheckCompatibilityType.DatabaseSchema) {
                    application.DatabaseUpdateMode = DatabaseUpdateMode.UpdateDatabaseAlways;
                }
#endif
            });
            var winApplication = builder.Build();

            // Wire Copilot tools now that the DI container + INonSecuredObjectSpaceFactory are available.
            try
            {
                var schemaService = winApplication.ServiceProvider.GetRequiredService<SchemaDiscoveryService>();
                copilotService.SystemMessage = schemaService.GenerateSystemPrompt();
                var toolsProvider = new CopilotToolsProvider(winApplication.ServiceProvider, schemaService);
                copilotService.Tools = toolsProvider.Tools;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Copilot tools not available: {ex.Message}");
            }

            return winApplication;
        }

        XafApplication IDesignTimeApplicationFactory.Create()
            => BuildApplication(XafApplication.DesignTimeConnectionString);
    }
}
