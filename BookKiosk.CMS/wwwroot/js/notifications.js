(() => {
    'use strict';
    if (window.cmsNotifications) {
        window.cmsNotifications.init(document);
        return;
    }
    const initialized = new WeakSet();
    function init(root = document) {
        root.querySelectorAll('[data-flash-alert="success"]').forEach(element => {
            if (initialized.has(element)) return;
            initialized.add(element);
            const timer = window.setTimeout(() => {
                if (!element.isConnected) return;
                if (window.bootstrap?.Alert) window.bootstrap.Alert.getOrCreateInstance(element).close();
                else element.remove();
            }, 5000);
            element.addEventListener('close.bs.alert', () => window.clearTimeout(timer), { once: true });
        });
    }
    window.showToast = (message, type = 'success') => {
        const element = document.getElementById('liveToast');
        if (!element || !window.bootstrap?.Toast) return;
        // Cancel the old success timer before showing a persistent error/warning.
        window.bootstrap.Toast.getInstance(element)?.dispose();
        element.className = 'toast align-items-center text-white border-0 shadow-lg';
        const kind = type === 'error' ? 'danger' : type;
        const colors = { success: 'bg-success', danger: 'bg-danger', warning: 'bg-warning', info: 'bg-primary' };
        element.classList.add(colors[kind] || 'bg-primary');
        if (kind === 'warning') {
            element.classList.remove('text-white');
            element.classList.add('text-dark');
        }
        element.querySelector('[data-bs-dismiss="toast"]')?.classList.toggle('btn-close-white', kind !== 'warning');
        const text = document.getElementById('toastMessage');
        const icon = document.getElementById('toastIcon');
        if (text) text.textContent = message == null ? '' : String(message);
        if (icon) {
            const icons = { success: 'fa-check-circle', danger: 'fa-exclamation-circle', warning: 'fa-exclamation-triangle' };
            icon.className = `fas ${icons[kind] || 'fa-info-circle'} fs-5 me-2`;
        }
        new window.bootstrap.Toast(element, { delay: 5000, autohide: kind === 'success' }).show();
    };
    window.cmsNotifications = { init };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => init(), { once: true });
    else init();
})();
