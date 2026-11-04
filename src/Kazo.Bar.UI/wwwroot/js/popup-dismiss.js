// Popup dismissal shared by every host (Web, MAUI, WPF).
// Elements marked with [data-popup-root="name"] (trigger + panel) form one popup;
// a pointer down elsewhere closes it, including inside another popup root.
// No overlay is created: clicks on transparent areas of a desktop host never
// reach this page and are handled natively (window deactivation).

let registered = null;

function onPointerDown(event) {
    if (!registered) return;
    const target = event.target;
    const root = target instanceof Element ? target.closest('[data-popup-root]') : null;
    registered.invokeMethodAsync('OnDismiss', 'OutsideClick', root ? root.getAttribute('data-popup-root') : null);
}

function onKeyDown(event) {
    if (!registered || event.key !== 'Escape') return;
    registered.invokeMethodAsync('OnDismiss', 'Escape', null);
}

export function register(dotNetRef) {
    unregister();
    registered = dotNetRef;
    document.addEventListener('pointerdown', onPointerDown, true);
    document.addEventListener('keydown', onKeyDown, true);
}

export function unregister() {
    document.removeEventListener('pointerdown', onPointerDown, true);
    document.removeEventListener('keydown', onKeyDown, true);
    registered = null;
}
