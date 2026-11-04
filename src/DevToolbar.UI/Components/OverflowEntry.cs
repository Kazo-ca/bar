namespace DevToolbar.UI.Components;

using DevToolbar.Core.Interfaces;

/// <summary>
/// A system zone module shown in the "»" menu (US3.8): what its toolbar button showed, and the
/// plugin whose panel opens when it is chosen (null when the module has no panel).
/// </summary>
public sealed record OverflowEntry(
    string ModuleId,
    string Icon,
    string Label,
    string? Text,
    int Badge,
    string? StatusLevel,
    IPlugin? Plugin);
