// Va-bank JS interop helpers for Blazor WASM
window.vabank = {
    // Copy text to clipboard
    copyToClipboard: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    },

    // Scroll element into view
    scrollToElement: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    },

    // Scroll to bottom of element (for chat)
    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) el.scrollTop = el.scrollHeight;
    },

    // Get viewport width
    getViewportWidth: function () {
        return window.innerWidth;
    },

    // Focus input element
    focusElement: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) el.focus();
    }
};
