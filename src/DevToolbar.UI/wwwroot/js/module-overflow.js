// System zone overflow (US3.8, rule 5). Measures the room the system zone's modules would have
// if every other zone were at its smallest (quick actions reduced to "…", resizable modules at
// their min width), and the width of each module of the zone (modules already in the "»" menu
// stay rendered, invisible and out of the flow, so they can be measured). Reports both to
// MainLayout, which decides how many modules stay (ModuleOverflow.VisibleCount).

const states = new WeakMap();
const ACTIONS_MIN = 38; // the "…" button of the quick actions (US3.4)

function px(value) {
    const n = parseFloat(value);
    return Number.isFinite(n) ? n : 0;
}

function margins(style) {
    return px(style.marginLeft) + px(style.marginRight);
}

function boxExtras(style) {
    return px(style.paddingLeft) + px(style.paddingRight) + px(style.borderLeftWidth) + px(style.borderRightWidth);
}

/** Smallest width a zone of the shell can take. */
function minimumWidth(child) {
    const style = getComputedStyle(child);
    if (child.classList.contains('module-actions')) return boxExtras(style) + margins(style) + ACTIONS_MIN;
    if (child.classList.contains('resizable') && px(style.minWidth) > 0) return px(style.minWidth) + margins(style);
    return child.getBoundingClientRect().width + margins(style);
}

function measure(shell) {
    const system = shell.querySelector(':scope > .module-system');
    if (!system) return null;
    const shellStyle = getComputedStyle(shell);
    const shellMax = Math.min(px(shellStyle.maxWidth) || Infinity, window.innerWidth - 16);

    let base = 0;
    for (const child of shell.children) {
        if (child === system || getComputedStyle(child).position === 'absolute') continue;
        base += minimumWidth(child);
    }

    const systemStyle = getComputedStyle(system);
    const gap = px(systemStyle.columnGap) || px(systemStyle.gap);
    base += boxExtras(systemStyle) + margins(systemStyle);
    let fixedCount = 0;
    const ids = [];
    const widths = [];
    for (const child of system.children) {
        if (child.dataset.moduleId) {
            ids.push(child.dataset.moduleId);
            widths.push(Math.ceil(child.getBoundingClientRect().width));
        } else if (!child.classList.contains('module-overflow') && getComputedStyle(child).position !== 'absolute') {
            base += child.getBoundingClientRect().width;
            fixedCount++;
        }
    }
    // Gaps between the fixed buttons; each module and the "»" button add their own gap (C# side).
    base += Math.max(0, fixedCount - 1) * gap;
    return { room: Math.max(0, Math.floor(shellMax - base)), ids, widths, gap: Math.round(gap) };
}

function report(state) {
    if (!state.shell.isConnected) return;
    const m = measure(state.shell);
    if (!m) return;
    const key = m.room + '|' + m.ids.join(',') + '|' + m.widths.join(',');
    if (key === state.last) return;
    state.last = key;
    state.dotnet.invokeMethodAsync('OnSystemZoneMeasured', m.room, m.ids, m.widths, m.gap)
        .catch(() => { /* component gone */ });
}

export function observe(shell, dotnet) {
    if (!shell || states.has(shell)) return;
    const state = { shell, dotnet, last: null, frame: 0 };
    const schedule = () => {
        if (state.frame) return;
        state.frame = requestAnimationFrame(() => { state.frame = 0; report(state); });
    };
    state.resize = new ResizeObserver(schedule);
    const watch = () => {
        state.resize.disconnect();
        for (const child of shell.children) state.resize.observe(child);
        const system = shell.querySelector(':scope > .module-system');
        if (system) for (const child of system.children) state.resize.observe(child);
    };
    watch();
    state.mutation = new MutationObserver(records => {
        if (records.some(r => r.type === 'childList')) watch();
        schedule();
    });
    state.mutation.observe(shell, { childList: true, subtree: true, characterData: true });
    state.onWindow = schedule;
    window.addEventListener('resize', schedule);
    states.set(shell, state);
    schedule();
}

export function unobserve(shell) {
    const state = shell && states.get(shell);
    if (!state) return;
    state.resize.disconnect();
    state.mutation.disconnect();
    window.removeEventListener('resize', state.onWindow);
    if (state.frame) cancelAnimationFrame(state.frame);
    states.delete(shell);
}
