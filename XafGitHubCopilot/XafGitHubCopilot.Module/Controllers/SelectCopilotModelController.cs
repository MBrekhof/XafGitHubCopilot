using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using XafGitHubCopilot.Module.Services;

namespace XafGitHubCopilot.Module.Controllers
{
    /// <summary>
    /// Window controller that provides a <see cref="SingleChoiceAction"/> to switch
    /// the GitHub Copilot model used by <see cref="CopilotChatService"/> at runtime.
    /// </summary>
    public class SelectCopilotModelController : WindowController
    {
        private SingleChoiceAction _selectModelAction;

        /// <summary>
        /// Models available through the GitHub Copilot SDK.
        /// Add or remove entries here as new models become available.
        /// </summary>
        private static readonly string[] AvailableModels = new[]
        {
            "gpt-4o",
            "gpt-4o-mini",
            "gpt-4.1",
            "gpt-4.1-mini",
            "gpt-4.1-nano",
            "gpt-5",
            "o3-mini",
            "o4-mini",
            "claude-sonnet-4",
            "gemini-2.5-pro",
        };

        public SelectCopilotModelController()
        {
            TargetWindowType = WindowType.Main;

            _selectModelAction = new SingleChoiceAction(this, "SelectCopilotModel", PredefinedCategory.View)
            {
                Caption = "Copilot Model",
                ImageName = "ModelEditor_Class",
                ToolTip = "Select the AI model for Copilot Chat",
                ItemType = SingleChoiceActionItemType.ItemIsMode,
            };

            foreach (var model in AvailableModels)
            {
                _selectModelAction.Items.Add(new ChoiceActionItem(model, model));
            }

            _selectModelAction.Execute += SelectModelAction_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();

            // Highlight the currently configured model.
            var service = Application.ServiceProvider.GetService<CopilotChatService>();
            if (service != null)
            {
                var currentModel = service.CurrentModel;
                var item = _selectModelAction.Items.FirstOrDefault(i => (string)i.Data == currentModel);
                if (item != null)
                {
                    _selectModelAction.SelectedItem = item;
                }
            }
        }

        private void SelectModelAction_Execute(object sender, SingleChoiceActionExecuteEventArgs e)
        {
            var selectedModel = (string)e.SelectedChoiceActionItem.Data;

            var service = Application.ServiceProvider.GetService<CopilotChatService>();
            if (service != null)
            {
                service.CurrentModel = selectedModel;
                _selectModelAction.SelectedItem = e.SelectedChoiceActionItem;
            }
        }
    }
}
