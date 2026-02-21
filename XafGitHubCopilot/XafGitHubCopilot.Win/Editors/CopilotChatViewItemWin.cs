using System;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.Utils;
using DevExpress.AIIntegration.Blazor.Chat;
using DevExpress.AIIntegration.Blazor.Chat.WebView;
using DevExpress.AIIntegration.WinForms.Chat;
using Microsoft.Extensions.AI;
using XafGitHubCopilot.Module.Editors;
using XafGitHubCopilot.Module.Services;

namespace XafGitHubCopilot.Win.Editors
{
    /// <summary>
    /// WinForms ViewItem that hosts the DevExpress <see cref="AIChatControl"/>.
    /// Messages are routed automatically through the registered <c>IChatClient</c>
    /// (backed by the GitHub Copilot SDK).
    /// </summary>
    [ViewItem(typeof(IModelCopilotChatViewItem))]
    public class CopilotChatViewItemWin : ViewItem
    {
        private AIChatControl _chatControl;

        public CopilotChatViewItemWin(IModelViewItem model, Type objectType)
            : base(objectType, model.Id)
        {
        }

        protected override object CreateControlCore()
        {
            _chatControl = new AIChatControl
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                UseStreaming = DefaultBoolean.True,
                ShowHeader = DefaultBoolean.True,
                HeaderText = CopilotChatDefaults.HeaderText,
                EmptyStateText = CopilotChatDefaults.EmptyStateText,
                ContentFormat = ResponseContentFormat.Markdown,
                ChatClientServiceKey = "copilot"
            };

            // Markdown rendering via shared helper
            _chatControl.MarkdownConvert += OnMarkdownConvert;

            // Prompt suggestions from centralized definitions
            _chatControl.SetPromptSuggestions(
                CopilotChatDefaults.PromptSuggestions
                    .Select(s => new PromptSuggestion(title: s.Title, text: s.Text, prompt: s.Prompt))
                    .ToList());

            // System prompt is set on CopilotChatService via SchemaDiscoveryService (no UI-level injection needed).

            return _chatControl;
        }

        private void OnMarkdownConvert(object sender, AIChatControlMarkdownConvertEventArgs e)
        {
            var html = CopilotChatDefaults.ConvertMarkdownToHtml(e.MarkdownText);
            e.HtmlText = (Microsoft.AspNetCore.Components.MarkupString)html;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _chatControl != null)
            {
                _chatControl.MarkdownConvert -= OnMarkdownConvert;
                _chatControl.Dispose();
                _chatControl = null;
            }
            base.Dispose(disposing);
        }
    }
}
