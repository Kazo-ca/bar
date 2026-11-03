// Quick actions overflow (US3.4). The toolbar shell is as wide as its content, so the room
// left for the actions is computed from the window: shell max width minus the other zones.
// Reports that width and the measured width of each rendered action (data-action-index) to
// ActionDeck, which decides how many fit. Loaded as a JS module by ActionDeck.

const states = new WeakMap();

function px(value) {
    const n = parseFloat(value);
    return Number.isFinite(n) ? n : 0;
}

function outerWidth(el) {
    const s = getComputedStyle(el);
    return el.getBoundingClientRect().width + px(s.marginLeft) + px(s.marginRight);
}

function available(host) {
    const shell = host.closest('.toolbar-shell');
    const zone = host.closest('.module-actions');
    if (!shell || !zone) return Math.floor(host.clientWidth);

    const shellStyle = getComputedStyle(shell);
    const shellMax = Math.min(px(shellStyle.maxWidth) || Infinity, window.innerWidth - 16);
    let others = 0;
    for (const child of shell.children) {
        if (child !== zone && getComputedStyle(child).position !== 'absolute') others += outerWidth(child);
    }
    const zoneStyle = getComputedStyle(zone);
    const zonePadding = px(zoneStyle.paddingLeft) + px(zoneStyle.paddingRight);
    return Math.max(0, Math.floor(shellMax - others - zonePadding));
}

function report(state) {
    const { host, dotnet } = state;
    if (!host.isConnected) return;
    const widths = [];
    for (const item of host.querySelectorAll('[data-action-index]')) {
        widths.push([Number(item.dataset.actionIndex), Math.ceil(item.getBoundingClientRect().width)]);
    }
    const width = available(host);
    const key = width + '|' + widths.join(';');
    if (key === state.last) return;
    state.last = key;
    dotnet.invokeMethodAsync('OnMeasured', width, widths.map(w => w[0]), widths.map(w => w[1]))
        .catch(() => { /* component gone */ });
}

export function observe(host, dotnet) {
    if (!host || states.has(host)) return;
    const state = { host, dotnet, last: null };
    const schedule = () => requestAnimationFrame(() => report(state));
    state.resize = new ResizeObserver(schedule);
    const shell = host.closest('.toolbar-shell');
    for (const child of (shell ? shell.children : [host])) state.resize.observe(child);
    state.mutation = new MutationObserver(schedule);
    state.mutation.observe(host, { childList: true, subtree: true });
    state.onWindow = schedule;
    window.addEventListener('resize', schedule);
    states.set(host, state);
    schedule();
}

export function unobserve(host) {
    const state = host && states.get(host);
    if (!state) return;
    state.resize.disconnect();
    state.mutation.disconnect();
    window.removeEventListener('resize', state.onWindow);
    states.delete(host);
}
