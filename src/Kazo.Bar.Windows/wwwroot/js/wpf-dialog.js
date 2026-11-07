// Dialog windows of the WPF host (DialogWindow, wwwroot/dialog.html).
//
// Mouse wheel: the WPF BlazorWebView renders through WebView2CompositionControl, whose forwarded
// wheel input did not scroll the page in dialog windows. DialogWindow therefore takes the wheel
// itself and calls kazoDialogWheel, which scrolls the first element under the pointer that can
// still move in that direction (else the page).

(function () {
    var SCROLLABLE = /(auto|scroll|overlay)/;

    function canScroll(el, deltaX, deltaY, isRoot) {
        var style = window.getComputedStyle(el);
        if (deltaY !== 0 && (isRoot || SCROLLABLE.test(style.overflowY)) && el.scrollHeight > el.clientHeight) {
            if (deltaY < 0 ? el.scrollTop > 0 : el.scrollTop + el.clientHeight < el.scrollHeight - 1) return true;
        }
        if (deltaX !== 0 && (isRoot || SCROLLABLE.test(style.overflowX)) && el.scrollWidth > el.clientWidth) {
            if (deltaX < 0 ? el.scrollLeft > 0 : el.scrollLeft + el.clientWidth < el.scrollWidth - 1) return true;
        }
        return false;
    }

    /**
     * Scrolls what is under the pointer.
     * @param {number} x - Pointer X in CSS px, relative to the page.
     * @param {number} y - Pointer Y in CSS px, relative to the page.
     * @param {number} deltaX - Pixels to scroll right (negative: left).
     * @param {number} deltaY - Pixels to scroll down (negative: up).
     * @returns {boolean} true when something scrolled.
     */
    window.kazoDialogWheel = function (x, y, deltaX, deltaY) {
        var el = document.elementFromPoint(x, y);
        for (; el && el !== document.body && el !== document.documentElement; el = el.parentElement) {
            if (canScroll(el, deltaX, deltaY, false)) {
                el.scrollBy(deltaX, deltaY);
                return true;
            }
        }
        var root = document.scrollingElement || document.documentElement;
        if (root && canScroll(root, deltaX, deltaY, true)) {
            root.scrollBy(deltaX, deltaY);
            return true;
        }
        return false;
    };
})();
