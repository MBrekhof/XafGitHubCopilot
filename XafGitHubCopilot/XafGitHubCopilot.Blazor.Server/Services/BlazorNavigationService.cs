using System.Collections.Concurrent;
using XafGitHubCopilot.Module.Services;

namespace XafGitHubCopilot.Blazor.Server.Services
{
    /// <summary>
    /// Blazor implementation of <see cref="INavigationService"/>.
    /// AI tools enqueue navigation/filter requests; the <see cref="NavigationExecutorController"/>
    /// polls and executes them on the XAF UI thread.
    /// </summary>
    public sealed class BlazorNavigationService : INavigationService
    {
        private readonly ConcurrentQueue<NavigationRequest> _navQueue = new();
        private readonly ConcurrentQueue<FilterRequest> _filterQueue = new();
        private volatile bool _refreshRequested;

        /// <summary>Fired when a navigation request is enqueued.</summary>
        public event Action OnNavigationRequested;

        /// <summary>Fired when a filter request is enqueued.</summary>
        public event Action OnFilterRequested;

        /// <summary>Fired when a view refresh is requested.</summary>
        public event Action OnRefreshRequested;

        /// <summary>Fired when the side panel toggle is requested.</summary>
        public event Action OnSidePanelToggleRequested;

        public void NavigateToListView(string entityName)
        {
            _navQueue.Enqueue(new NavigationRequest(entityName, null));
            OnNavigationRequested?.Invoke();
        }

        public void NavigateToDetailView(string entityName, string keyValue)
        {
            _navQueue.Enqueue(new NavigationRequest(entityName, keyValue));
            OnNavigationRequested?.Invoke();
        }

        public void ToggleSidePanel()
        {
            OnSidePanelToggleRequested?.Invoke();
        }

        public void FilterActiveList(string criteriaString)
        {
            _filterQueue.Enqueue(new FilterRequest(criteriaString));
            OnFilterRequested?.Invoke();
        }

        public void ClearActiveListFilter()
        {
            _filterQueue.Enqueue(new FilterRequest(null));
            OnFilterRequested?.Invoke();
        }

        public void RefreshActiveView()
        {
            _refreshRequested = true;
            OnRefreshRequested?.Invoke();
        }

        /// <summary>Dequeue the next pending navigation request, if any.</summary>
        public bool TryDequeueNavigation(out NavigationRequest request) =>
            _navQueue.TryDequeue(out request);

        /// <summary>Dequeue the next pending filter request, if any.</summary>
        public bool TryDequeueFilter(out FilterRequest request) =>
            _filterQueue.TryDequeue(out request);

        /// <summary>Consume the pending refresh flag.</summary>
        public bool ConsumeRefresh()
        {
            if (!_refreshRequested) return false;
            _refreshRequested = false;
            return true;
        }
    }

    public sealed record NavigationRequest(string EntityName, string KeyValue);

    /// <summary>
    /// Represents a pending filter request. Null CriteriaString means clear the filter.
    /// </summary>
    public sealed record FilterRequest(string CriteriaString);
}
