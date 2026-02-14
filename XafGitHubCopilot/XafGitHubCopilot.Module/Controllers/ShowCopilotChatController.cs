using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.Persistent.Base;
using XafGitHubCopilot.Module.BusinessObjects;

namespace XafGitHubCopilot.Module.Controllers
{
    /// <summary>
    /// Window controller that:
    /// 1. Intercepts navigation to <c>CopilotChat_ListView</c> and redirects to a DetailView.
    /// 2. Provides a "Copilot Chat" action in the Tools menu.
    /// </summary>
    public class ShowCopilotChatController : WindowController
    {
        private SimpleAction _showCopilotChatAction;

        public ShowCopilotChatController()
        {
            TargetWindowType = WindowType.Main;

            _showCopilotChatAction = new SimpleAction(this, "ShowCopilotChat", PredefinedCategory.View)
            {
                Caption = "New Copilot Chat",
                ImageName = "Actions_EnterGroup",
                ToolTip = "Open the GitHub Copilot AI chat assistant"
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
                OpenCopilotChat(e.ActionArguments.ShowViewParameters);
                e.Handled = true;
            }
        }

        private void ShowCopilotChatAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            OpenCopilotChat(e.ShowViewParameters);
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
