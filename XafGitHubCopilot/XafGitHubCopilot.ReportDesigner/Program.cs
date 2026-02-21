using System.Reflection;
using DevExpress.AIIntegration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using XafGitHubCopilot.Module.Services;

namespace XafGitHubCopilot.ReportDesigner;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        // Register OpenAI client for AI report behaviors.
        var apiKey = configuration["OpenAI:ApiKey"];
        var model = configuration["OpenAI:Model"] ?? "gpt-4o";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            MessageBox.Show(
                "OpenAI API key is not configured.\n\nSet the \"OpenAI:ApiKey\" value in appsettings.json.",
                "Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        IChatClient chatClient = new OpenAI.OpenAIClient(apiKey)
            .GetChatClient(model)
            .AsIChatClient();

        AIExtensionsContainerDesktop.Default.RegisterChatClient(chatClient);

        // Discover entities from the Module assembly for AI context.
        var moduleAssembly = Assembly.GetAssembly(typeof(XafGitHubCopilot.Module.XafGitHubCopilotModule))!;
        var schemaService = new ReflectionSchemaDiscoveryService(moduleAssembly);
        var schema = schemaService.Schema;

        System.Diagnostics.Debug.WriteLine($"[ReportDesigner] Discovered {schema.Entities.Count} entities:");
        foreach (var entity in schema.Entities)
        {
            System.Diagnostics.Debug.WriteLine($"  - {entity.Name} (table: {entity.TableName}) — {entity.Description}");
        }

        // Resolve database path — look for the XAF app's database relative to the output directory.
        var connectionString = configuration["Database:ConnectionString"] ?? "Data Source=XafGitHubCopilot.db";

        Application.Run(new AIReportDesignerForm(connectionString));
    }
}
