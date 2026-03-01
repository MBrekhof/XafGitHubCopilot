using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.Persistent.Base;
using Microsoft.Extensions.DependencyInjection;
using XafGitHubCopilot.Module.BusinessObjects;
using XafGitHubCopilot.Module.Services;

namespace XafGitHubCopilot.Module.Controllers
{
    /// <summary>
    /// Window controller that:
    /// 1. Intercepts navigation to <c>CopilotChat_ListView</c> and redirects to the side panel (Blazor)
    ///    or DetailView (WinForms).
    /// 2. Provides a "Copilot Chat" action in the View menu.
    /// </summary>
    public class ShowCopilotChatController : WindowController
    {
        private SimpleAction _showCopilotChatAction;

        public ShowCopilotChatController()
        {
            TargetWindowType = WindowType.Main;

            _showCopilotChatAction = new SimpleAction(this, "ShowCopilotChat", PredefinedCategory.View)
            {
                Caption = "AI Assistant",
                ImageName = "Actions_EnterGroup",
                ToolTip = "Toggle the AI assistant panel"
            };
            _showCopilotChatAction.Execute += ShowCopilotChatAction_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            var navController = Frame.GetController<ShowNavigationItemController>();
            if (navController != null)
            {
                navController.CustomShowNavigationItem += OnCustomShowNavigationItem;
            }
        }

        protected override void OnDeactivated()
        {
            var navController = Frame.GetController<ShowNavigationItemController>();
            if (navController != null)
            {
                navController.CustomShowNavigationItem -= OnCustomShowNavigationItem;
            }
            base.OnDeactivated();
        }

        private void OnCustomShowNavigationItem(object sender, CustomShowNavigationItemEventArgs e)
        {
            if (e.ActionArguments.SelectedChoiceActionItem?.Data is ViewShortcut shortcut
                && shortcut.ViewId == "CopilotChat_ListView")
            {
                // On Blazor, toggle the side panel. On WinForms, open a DetailView.
                if (TryToggleSidePanel())
                {
                    e.Handled = true;
                }
                else
                {
                    OpenCopilotChat(e.ActionArguments.ShowViewParameters);
                    e.Handled = true;
                }
            }
        }

        private void ShowCopilotChatAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            if (!TryToggleSidePanel())
            {
                OpenCopilotChat(e.ShowViewParameters);
            }
        }

        /// <summary>
        /// Attempts to toggle the side panel via <see cref="INavigationService"/>.
        /// Returns true if a navigation service is available (Blazor), false otherwise (WinForms).
        /// </summary>
        private bool TryToggleSidePanel()
        {
            var navService = Application.ServiceProvider.GetService<INavigationService>();
            if (navService != null)
            {
                navService.ToggleSidePanel();
                return true;
            }
            return false;
        }

        private void OpenCopilotChat(ShowViewParameters showViewParameters)
        {
            var objectSpace = Application.CreateObjectSpace(typeof(CopilotChat));
            var chatObject = objectSpace.CreateObject<CopilotChat>();
            var detailView = Application.CreateDetailView(objectSpace, chatObject);
            detailView.ViewEditMode = DevExpress.ExpressApp.Editors.ViewEditMode.View;
            showViewParameters.CreatedView = detailView;
        }
    }
}
