// Va-bank JavaScript Interop — Theme, PWA, utilities
window.vabank = {
    // ── Theme Management ────────────────────────────────
    theme: {
        get() {
            return localStorage.getItem('vb-theme') || 'light';
        },
        set(theme) {
            localStorage.setItem('vb-theme', theme);
            document.documentElement.setAttribute('data-theme', theme);
            // Update meta theme-color
            const meta = document.querySelector('meta[name="theme-color"]');
            if (meta) {
                meta.content = theme === 'dark' ? '#0D0D0D' : '#1A1A1A';
            }
        },
        toggle() {
            const current = this.get();
            const next = current === 'dark' ? 'light' : 'dark';
            this.set(next);
            return next;
        },
        init() {
            const saved = localStorage.getItem('vb-theme');
            if (saved) {
                this.set(saved);
            } else if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
                this.set('dark');
            }
            // Listen for system changes
            window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', e => {
                if (!localStorage.getItem('vb-theme')) {
                    this.set(e.matches ? 'dark' : 'light');
                }
            });
        }
    },

    // ── PWA Install ─────────────────────────────────────
    pwa: {
        deferredPrompt: null,
        init() {
            window.addEventListener('beforeinstallprompt', (e) => {
                e.preventDefault();
                vabank.pwa.deferredPrompt = e;
            });
        },
        canInstall() {
            return vabank.pwa.deferredPrompt !== null;
        },
        async install() {
            const prompt = vabank.pwa.deferredPrompt;
            if (!prompt) return false;
            prompt.prompt();
            const result = await prompt.userChoice;
            vabank.pwa.deferredPrompt = null;
            return result.outcome === 'accepted';
        }
    },

    // ── LocalStorage ────────────────────────────────────
    storage: {
        get(key) {
            return localStorage.getItem(key);
        },
        set(key, value) {
            localStorage.setItem(key, value);
        },
        remove(key) {
            localStorage.removeItem(key);
        }
    },

    // ── Scroll ──────────────────────────────────────────
    scrollToBottom(elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    },

    scrollToTop() {
        window.scrollTo({ top: 0, behavior: 'smooth' });
    },

    downloadFile(base64Data, fileName) {
        const link = document.createElement('a');
        link.href = 'data:application/octet-stream;base64,' + base64Data;
        link.download = fileName;
        link.click();
    }
};

// Initialize theme on page load (before Blazor boots)
vabank.theme.init();
vabank.pwa.init();
