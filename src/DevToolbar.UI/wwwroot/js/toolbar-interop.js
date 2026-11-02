// DevToolbar JS Interop helpers

window.DevToolbar = {
    /**
     * Downloads a string as a CSV file.
     * @param {string} filename - The filename for the download.
     * @param {string} csvContent - The CSV content to download.
     */
    downloadCsv: function (filename, csvContent) {
        var blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        var link = document.createElement('a');
        var url = URL.createObjectURL(blob);
        link.href = url;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    },

    /**
     * Calls dotNetRef.invokeMethodAsync('Dismiss') when the user clicks outside
     * `element` or presses Escape. Used by dropdowns and popovers.
     * @returns {number} handle to pass to removeDismissListener.
     */
    addDismissListener: function (element, dotNetRef) {
        var id = ++DevToolbar._dismissSeq;
        var dismiss = function () {
            DevToolbar.removeDismissListener(id);
            dotNetRef.invokeMethodAsync('Dismiss');
        };
        var onPointer = function (e) {
            if (element && !element.contains(e.target)) dismiss();
        };
        var onKey = function (e) {
            if (e.key === 'Escape') dismiss();
        };
        document.addEventListener('pointerdown', onPointer, true);
        document.addEventListener('keydown', onKey, true);
        DevToolbar._dismissListeners[id] = { onPointer: onPointer, onKey: onKey };
        return id;
    },

    removeDismissListener: function (id) {
        var l = DevToolbar._dismissListeners[id];
        if (!l) return;
        document.removeEventListener('pointerdown', l.onPointer, true);
        document.removeEventListener('keydown', l.onKey, true);
        delete DevToolbar._dismissListeners[id];
    },

    _dismissSeq: 0,
    _dismissListeners: {}
};
