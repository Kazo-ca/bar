// Host surface negotiation (see IHostSurfaceService).
//
// Each observed region reports the surface size it needs to be fully visible,
// measured from the window edge the host keeps fixed (top, or bottom when the
// page has body.dock-bottom), and receives back a --host-max-height that keeps it
// inside the window, so whatever the host could not give scrolls instead of being
// clipped. Loaded as a JS module by HostSizedRegion; no global script needed.

const regions = new Map();

function px(value) {
    const n = parseFloat(value);
    return Number.isFinite(n) ? n : 0;
}

function measure(state) {
    const { outer, inner } = state;
    if (!outer.isConnected) return;

    const style = getComputedStyle(outer);
    const padV = px(style.paddingTop) + px(style.paddingBottom) + px(style.borderTopWidth) + px(style.borderBottomWidth);
    const padH = px(style.paddingLeft) + px(style.paddingRight) + px(style.borderLeftWidth) + px(style.borderRightWidth);
    const naturalHeight = inner.offsetHeight + padV;
    const naturalWidth = inner.scrollWidth + padH;

    const rect = outer.getBoundingClientRect();
    const viewport = window.innerHeight;
    const fromBottom = document.body.classList.contains('dock-bottom');

    // Room the region has inside the current window, from its anchored edge.
    const room = fromBottom ? rect.bottom : viewport - rect.top;
    outer.style.setProperty('--host-max-height', Math.max(0, Math.floor(room)) + 'px');

    // Surface needed so the region is shown without scrolling. Independent of the
    // current window height for top-anchored regions, so resizing cannot loop.
    const height = Math.ceil(fromBottom ? viewport - rect.bottom + naturalHeight : rect.top + naturalHeight);
    const width = Math.ceil(naturalWidth);
    if (height === state.height && width === state.width) return;
    state.height = height;
    state.width = width;
    state.dotnet.invokeMethodAsync('OnMeasured', width, height).catch(() => { /* circuit gone */ });
}

function schedule(state) {
    if (state.frame) return;
    state.frame = requestAnimationFrame(() => {
        state.frame = 0;
        measure(state);
    });
}

export function observe(id, outer, inner, dotnet) {
    unobserve(id);
    const state = { outer, inner, dotnet, width: -1, height: -1, frame: 0 };
    state.observer = new ResizeObserver(() => schedule(state));
    state.observer.observe(inner);
    state.onResize = () => schedule(state);
    window.addEventListener('resize', state.onResize);
    regions.set(id, state);
    schedule(state);
}

// Host → page: what the host can give at most (CSS px), exposed to the region's CSS.
export function setAvailable(id, width, height) {
    const state = regions.get(id);
    if (!state) return;
    state.outer.style.setProperty('--host-available-width', Math.floor(width) + 'px');
    state.outer.style.setProperty('--host-available-height', Math.floor(height) + 'px');
    schedule(state);
}

export function unobserve(id) {
    const state = regions.get(id);
    if (!state) return;
    state.observer.disconnect();
    window.removeEventListener('resize', state.onResize);
    if (state.frame) cancelAnimationFrame(state.frame);
    regions.delete(id);
}
