namespace DevToolbar.Plugins.WorkItems;

using DevToolbar.Core.Events;
using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

/// <summary>
/// Plugin for displaying and managing work items (TFS/GitHub Issues).
/// Supports dropdown search/select to change active item (US5.2).
/// Publishes ActiveWorkItemChangedEvent for TimeTracker integration (US5.3).
/// </summary>
public class WorkItemsPlugin : IPlugin, IPluginIndicator
{
    private readonly IWorkItemProvider _provider;
    private readonly EventAggregator _eventAggregator;

    public string UniqueId => "work-items";
    public string Name => "Work Items";
    public string Icon => "📋";
    public bool IsEnabled { get; set; } = true;
    public event Action? OnStateChanged;

    // IPluginIndicator — compact "#ID" pill in the toolbar, full panel in a popover (US5.2)
    public string? IndicatorText => _activeItem != null ? $"#{_activeItem.Id}" : null;
    public string? IndicatorTooltip => _activeItem != null ? $"#{_activeItem.Id} - {_activeItem.Title}" : "No active work item";
    public int BadgeCount => 0;
    public string? StatusLevel => null;

    private WorkItem? _activeItem;
    private IReadOnlyList<WorkItem> _recentItems = Array.Empty<WorkItem>();
    private IReadOnlyList<WorkItem> _searchResults = Array.Empty<WorkItem>();
    private bool _showSearch;
    private string _searchQuery = string.Empty;

    public WorkItemsPlugin(IWorkItemProvider provider, EventAggregator eventAggregator)
    {
        _provider = provider;
        _eventAggregator = eventAggregator;
    }

    public async Task OnProjectChangedAsync(PluginContext context)
    {
        // Load recent items for this project context (empty query = get all recent)
        _recentItems = await _provider.SearchAsync(string.Empty);
        _activeItem = _recentItems.FirstOrDefault();
        _showSearch = false;
        _searchQuery = string.Empty;
        _searchResults = Array.Empty<WorkItem>();

        // Notify TimeTracker of active work item
        _eventAggregator.Publish(new ActiveWorkItemChangedEvent
        {
            WorkItemId = _activeItem?.Id,
            WorkItemTitle = _activeItem?.Title
        });

        OnStateChanged?.Invoke();
    }

    public RenderFragment? Render() => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "plugin-workitems");

        // Active work item display
        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "workitem-active");

        if (_activeItem != null)
        {
            // Clickable link to web (US5.2: "Lien clicable vers le web")
            var webUrl = _provider.GetWebUrl(_activeItem);
            builder.OpenElement(4, "a");
            builder.AddAttribute(5, "href", webUrl);
            builder.AddAttribute(6, "target", "_blank");
            builder.AddAttribute(7, "class", "workitem-id workitem-link");
            builder.AddAttribute(8, "title", $"Open #{_activeItem.Id} in browser");
            builder.AddContent(9, $"#{_activeItem.Id}");
            builder.CloseElement();

            builder.OpenElement(10, "span");
            builder.AddAttribute(11, "class", "workitem-title");
            builder.AddContent(12, _activeItem.Title);
            builder.CloseElement();

            builder.OpenElement(13, "span");
            builder.AddAttribute(14, "class", $"workitem-state workitem-state-{_activeItem.State.ToLowerInvariant().Replace(" ", "-")}");
            builder.AddContent(15, _activeItem.State);
            builder.CloseElement();
        }
        else
        {
            builder.OpenElement(4, "span");
            builder.AddAttribute(5, "class", "workitem-none");
            builder.AddContent(6, "No active work item");
            builder.CloseElement();
        }

        // Toggle search button
        builder.OpenElement(30, "button");
        builder.AddAttribute(31, "class", "workitem-search-toggle");
        builder.AddAttribute(32, "title", "Search work items");
        builder.AddAttribute(33, "onclick", EventCallback.Factory.Create(this, () =>
        {
            _showSearch = !_showSearch;
            if (!_showSearch)
            {
                _searchQuery = string.Empty;
                _searchResults = Array.Empty<WorkItem>();
            }
            OnStateChanged?.Invoke();
        }));
        builder.AddContent(34, _showSearch ? "✕" : "🔍");
        builder.CloseElement();

        builder.CloseElement(); // workitem-active

        // Search dropdown (US5.2)
        if (_showSearch)
        {
            builder.OpenElement(40, "div");
            builder.AddAttribute(41, "class", "workitem-search");

            builder.OpenElement(42, "input");
            builder.AddAttribute(43, "type", "text");
            builder.AddAttribute(44, "class", "workitem-search-input");
            builder.AddAttribute(45, "placeholder", "Search work items...");
            builder.AddAttribute(46, "value", _searchQuery);
            builder.AddAttribute(47, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, async e =>
            {
                _searchQuery = e.Value?.ToString() ?? string.Empty;
                if (_searchQuery.Length >= 1)
                {
                    _searchResults = await _provider.SearchAsync(_searchQuery);
                }
                else
                {
                    _searchResults = _recentItems;
                }
                OnStateChanged?.Invoke();
            }));
            builder.CloseElement(); // input

            // Search results dropdown
            var displayItems = _searchResults.Count > 0 ? _searchResults : 
                              (_searchQuery.Length >= 1 ? Array.Empty<WorkItem>() : (IReadOnlyList<WorkItem>)_recentItems);
            if (displayItems.Count > 0)
            {
                builder.OpenElement(50, "div");
                builder.AddAttribute(51, "class", "workitem-dropdown");

                foreach (var item in displayItems.Take(5))
                {
                    var capturedItem = item;
                    builder.OpenElement(52, "div");
                    builder.AddAttribute(53, "class", $"workitem-dropdown-item{(capturedItem.Id == _activeItem?.Id ? " active" : "")}");
                    builder.AddAttribute(54, "onclick", EventCallback.Factory.Create(this, () =>
                    {
                        _activeItem = capturedItem;
                        _showSearch = false;
                        _searchQuery = string.Empty;
                        _searchResults = Array.Empty<WorkItem>();

                        // Notify TimeTracker of work item change
                        _eventAggregator.Publish(new ActiveWorkItemChangedEvent
                        {
                            WorkItemId = capturedItem.Id,
                            WorkItemTitle = capturedItem.Title
                        });

                        OnStateChanged?.Invoke();
                    }));

                    builder.OpenElement(55, "a");
                    builder.AddAttribute(56, "href", _provider.GetWebUrl(capturedItem));
                    builder.AddAttribute(57, "target", "_blank");
                    builder.AddAttribute(58, "class", "workitem-dropdown-id workitem-link");
                    builder.AddAttribute(59, "onclick", EventCallback.Factory.Create(this, (Microsoft.AspNetCore.Components.Web.MouseEventArgs e) => { /* allow link navigation */ }));
                    builder.AddContent(60, $"#{capturedItem.Id}");
                    builder.CloseElement();

                    builder.OpenElement(58, "span");
                    builder.AddAttribute(59, "class", "workitem-dropdown-title");
                    builder.AddContent(60, capturedItem.Title);
                    builder.CloseElement();

                    builder.OpenElement(61, "span");
                    builder.AddAttribute(62, "class", $"workitem-state workitem-state-{capturedItem.State.ToLowerInvariant().Replace(" ", "-")}");
                    builder.AddContent(63, capturedItem.State);
                    builder.CloseElement();

                    builder.CloseElement(); // workitem-dropdown-item
                }

                builder.CloseElement(); // workitem-dropdown
            }
            else if (_searchQuery.Length >= 1)
            {
                builder.OpenElement(70, "div");
                builder.AddAttribute(71, "class", "workitem-dropdown");
                builder.OpenElement(72, "div");
                builder.AddAttribute(73, "class", "workitem-no-results");
                builder.AddContent(74, "No items found");
                builder.CloseElement();
                builder.CloseElement();
            }

            builder.CloseElement(); // workitem-search
        }

        // Recent items list
        if (_recentItems.Count > 0 && !_showSearch)
        {
            builder.OpenElement(13, "div");
            builder.AddAttribute(14, "class", "workitem-recent");

            builder.OpenElement(15, "span");
            builder.AddAttribute(16, "class", "workitem-recent-label");
            builder.AddContent(17, "Recent:");
            builder.CloseElement();

            foreach (var item in _recentItems.Take(3))
            {
                builder.OpenElement(18, "div");
                builder.AddAttribute(19, "class", "workitem-entry");

                builder.OpenElement(20, "a");
                builder.AddAttribute(21, "href", _provider.GetWebUrl(item));
                builder.AddAttribute(22, "target", "_blank");
                builder.AddAttribute(23, "class", "workitem-link");
                builder.AddContent(24, $"#{item.Id} - {item.Title}");
                builder.CloseElement();

                builder.CloseElement(); // workitem-entry
            }

            builder.CloseElement(); // workitem-recent
        }

        builder.CloseElement(); // plugin-workitems
    };
}
