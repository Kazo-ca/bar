// Resizable toolbar modules (US3.6). Dragging the grip on a module's right edge changes the
// module's width live (style only); the final width is sent to ModuleFrame on release, which
// saves it. Loaded as a JS module by ModuleFrame.

const states = new WeakMap();

function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
}

export function attach(frame, grip, dotnet, minWidth, maxWidth) {
    if (!frame || !grip || states.has(frame)) return;
    const state = { grip, dotnet, drag: null };

    state.down = (event) => {
        if (event.button !== 0) return;
        event.preventDefault();
        event.stopPropagation();
        grip.setPointerCapture(event.pointerId);
        state.drag = { id: event.pointerId, x: event.clientX, width: frame.getBoundingClientRect().width };
        frame.classList.add('resizing');
    };
    state.move = (event) => {
        if (!state.drag || event.pointerId !== state.drag.id) return;
        const width = clamp(Math.round(state.drag.width + event.clientX - state.drag.x), minWidth, maxWidth);
        frame.style.width = width + 'px';
    };
    state.up = (event) => {
        if (!state.drag || event.pointerId !== state.drag.id) return;
        const moved = Math.abs(event.clientX - state.drag.x) > 1;
        state.drag = null;
        frame.classList.remove('resizing');
        try { grip.releasePointerCapture(event.pointerId); } catch { /* already released */ }
        if (moved) {
            const width = Math.round(frame.getBoundingClientRect().width);
            dotnet.invokeMethodAsync('OnResized', width).catch(() => { /* component gone */ });
        }
    };

    grip.addEventListener('pointerdown', state.down);
    grip.addEventListener('pointermove', state.move);
    grip.addEventListener('pointerup', state.up);
    grip.addEventListener('pointercancel', state.up);
    states.set(frame, state);
}

export function detach(frame) {
    const state = frame && states.get(frame);
    if (!state) return;
    state.grip.removeEventListener('pointerdown', state.down);
    state.grip.removeEventListener('pointermove', state.move);
    state.grip.removeEventListener('pointerup', state.up);
    state.grip.removeEventListener('pointercancel', state.up);
    states.delete(frame);
}
