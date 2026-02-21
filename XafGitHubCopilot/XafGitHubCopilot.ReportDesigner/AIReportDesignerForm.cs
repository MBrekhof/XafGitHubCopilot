using System.ComponentModel;
using System.IO;
using System.Text;
using DevExpress.AIIntegration.WinForms.Reporting;
using DevExpress.Utils.Behaviors;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.UserDesigner;
using Microsoft.EntityFrameworkCore;

namespace XafGitHubCopilot.ReportDesigner;

/// <summary>
/// Standalone report designer form with AI Prompt-to-Report behavior.
/// Extends <see cref="XRDesignRibbonForm"/> and attaches the behavior
/// using the documented <c>Attach&lt;T&gt;</c> API.
/// </summary>
public sealed class AIReportDesignerForm : XRDesignRibbonForm
{
    private readonly IContainer _components;
    private readonly BehaviorManager _behaviorManager;
    private readonly string _connectionString;

    public AIReportDesignerForm(string connectionString)
    {
        _connectionString = connectionString;
        _components = new Container();
        _behaviorManager = new BehaviorManager(_components);

        Text = "AI Report Designer — XafGitHubCopilot";
        WindowState = FormWindowState.Maximized;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Attach AI Prompt-to-Report behavior to the MDI controller.
        // Using OnLoad to ensure the designer is fully initialized.
        var mdiController = DesignMdiController;
        if (mdiController != null)
        {
            _behaviorManager.Attach<ReportPromptToReportBehavior>(mdiController, behavior =>
            {
                behavior.Properties.RetryAttemptCount = 3;
                behavior.Properties.FixLayoutErrors = true;
            });

            System.Diagnostics.Debug.WriteLine("[AIReportDesignerForm] ReportPromptToReportBehavior attached via Attach<T>");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[AIReportDesignerForm] DesignMdiController is NULL — cannot attach behavior");
        }

        AddDatabaseMenuItems();
    }

    private void AddDatabaseMenuItems()
    {
        var ribbon = RibbonControl;
        if (ribbon == null) return;

        // Add a "Database" ribbon page with Load/Save items.
        var page = new RibbonPage("Database");
        var group = new RibbonPageGroup("Reports");

        var loadItem = new BarButtonItem(ribbon.Manager, "Load from DB");
        loadItem.ItemClick += OnLoadFromDatabase;

        var saveItem = new BarButtonItem(ribbon.Manager, "Save to DB");
        saveItem.ItemClick += OnSaveToDatabase;

        group.ItemLinks.Add(loadItem);
        group.ItemLinks.Add(saveItem);
        page.Groups.Add(group);
        ribbon.Pages.Add(page);
    }

    private void OnLoadFromDatabase(object? sender, ItemClickEventArgs e)
    {
        try
        {
            using var context = CreateDbContext();
            var reports = context.Set<DevExpress.Persistent.BaseImpl.EF.ReportDataV2>()
                .OrderBy(r => r.DisplayName)
                .ToList();

            if (reports.Count == 0)
            {
                MessageBox.Show("No reports found in the database.", "Load Report",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Show a simple selection dialog.
            var names = reports.Select(r => r.DisplayName ?? "(unnamed)").ToArray();
            using var dialog = new Form
            {
                Text = "Load Report from Database",
                Size = new System.Drawing.Size(400, 350),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
            };

            var listBox = new ListBox { Dock = DockStyle.Fill };
            foreach (var name in names) listBox.Items.Add(name);
            if (listBox.Items.Count > 0) listBox.SelectedIndex = 0;

            var okButton = new Button { Text = "Load", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
            dialog.Controls.Add(listBox);
            dialog.Controls.Add(okButton);
            dialog.AcceptButton = okButton;

            if (dialog.ShowDialog(this) == DialogResult.OK && listBox.SelectedIndex >= 0)
            {
                var selectedReport = reports[listBox.SelectedIndex];
                if (selectedReport.Content is { Length: > 0 })
                {
                    var report = new XtraReport();
                    using var stream = new MemoryStream(selectedReport.Content);
                    report.LoadLayoutFromXml(stream);
                    OpenReport(report);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading report:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSaveToDatabase(object? sender, ItemClickEventArgs e)
    {
        try
        {
            var report = ActiveDesignPanel?.Report;
            if (report == null)
            {
                MessageBox.Show("No active report to save.", "Save Report",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Prompt for report name using a simple input dialog.
            var reportName = PromptForReportName(report.DisplayName ?? "New Report");
            if (string.IsNullOrWhiteSpace(reportName)) return;

            using var stream = new MemoryStream();
            report.SaveLayoutToXml(stream);
            var content = stream.ToArray();

            using var context = CreateDbContext();
            var existing = context.Set<DevExpress.Persistent.BaseImpl.EF.ReportDataV2>()
                .FirstOrDefault(r => r.DisplayName == reportName);

            if (existing != null)
            {
                existing.Content = content;
            }
            else
            {
                var reportData = new DevExpress.Persistent.BaseImpl.EF.ReportDataV2
                {
                    DisplayName = reportName,
                    Content = content,
                };
                context.Set<DevExpress.Persistent.BaseImpl.EF.ReportDataV2>().Add(reportData);
            }

            context.SaveChanges();
            MessageBox.Show($"Report '{reportName}' saved successfully.", "Save Report",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving report:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string? PromptForReportName(string defaultName)
    {
        using var dialog = new Form
        {
            Text = "Save Report",
            Size = new System.Drawing.Size(350, 150),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var label = new Label { Text = "Report name:", Dock = DockStyle.Top, Height = 25, Padding = new Padding(5) };
        var textBox = new TextBox { Text = defaultName, Dock = DockStyle.Top };
        var okButton = new Button { Text = "Save", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };

        dialog.Controls.Add(textBox);
        dialog.Controls.Add(label);
        dialog.Controls.Add(okButton);
        dialog.AcceptButton = okButton;

        return dialog.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }

    private DbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<XafGitHubCopilot.Module.BusinessObjects.XafGitHubCopilotEFCoreDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new XafGitHubCopilot.Module.BusinessObjects.XafGitHubCopilotEFCoreDbContext(options);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _behaviorManager?.Dispose();
            _components?.Dispose();
        }
        base.Dispose(disposing);
    }
}
