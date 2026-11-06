namespace Kazo.Bar.Core.Interfaces;

using Microsoft.AspNetCore.Components;

/// <summary>
/// Optional contract for a plugin that fills the project pill, the first (blue) button of the
/// toolbar (US5.6). The Git plugin uses it: the pill shows the repository, its state, the
/// branch and the worktree, and the pill's menu lists the repositories and worktrees under the
/// projects. Only the first enabled plugin implementing it is used.
/// </summary>
public interface IProjectPillContent
{
    /// <summary>What the pill shows in place of the project name. Null keeps the project name.</summary>
    RenderFragment? RenderPillInfo();

    /// <summary>
    /// Extra section of the pill's menu, below the projects. <paramref name="closeMenu"/> closes
    /// the menu after a choice. Null adds nothing.
    /// </summary>
    RenderFragment? RenderPillMenu(Func<Task> closeMenu);

    /// <summary>Called when the pill's menu opens (e.g. to read worktrees again).</summary>
    Task OnPillMenuOpeningAsync();

    /// <summary>Tooltip added to the pill (e.g. repository path), or null.</summary>
    string? PillTooltip { get; }
}
