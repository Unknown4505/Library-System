(() => {
    'use strict';
    const forms = document.querySelectorAll('[data-supplier-form]');
    function errorFor(input) {
        const value = input.value;
        if (input.required && !value.trim()) return 'Trường này không được để trống hoặc chỉ chứa khoảng trắng.';
        if (input.maxLength > 0 && value.length > input.maxLength) return `Tối đa ${input.maxLength} ký tự.`;
        if (input.type === 'email' && value && input.validity.typeMismatch) return 'Email không đúng định dạng; bạn có thể để trống.';
        // VN phone prefixes/length/normalization are BLOCKED pending the approved Backend policy.
        return '';
    }
    function validate(input) {
        const message = errorFor(input);
        input.classList.toggle('is-invalid', !!message);
        input.setAttribute('aria-invalid', String(!!message));
        const target = document.getElementById(input.getAttribute('aria-describedby'));
        if (target) target.textContent = message;
        return !message;
    }
    function resetBusy(form) {
        delete form.dataset.submitting;
        form.removeAttribute('aria-busy');
        form.querySelectorAll('button').forEach(button => button.disabled = false);
        form.querySelector('[data-submit-status]').textContent = '';
    }
    forms.forEach(form => {
        // Native constraints still work without JavaScript; JS provides persistent inline errors.
        form.noValidate = true;
        const fields = form.querySelectorAll('[aria-describedby]');
        fields.forEach(input => input.addEventListener('input', () => validate(input)));
        form.addEventListener('submit', event => {
            if (form.dataset.submitting) { event.preventDefault(); return; }
            let valid = true;
            fields.forEach(input => { if (!validate(input)) valid = false; });
            if (!valid) { event.preventDefault(); form.querySelector('.is-invalid')?.focus(); return; }
            form.dataset.submitting = 'true';
            form.setAttribute('aria-busy', 'true');
            form.querySelectorAll('button').forEach(button => button.disabled = true);
            form.querySelector('[data-submit-status]').textContent = 'Đang lưu nhà cung cấp…';
        });
        const modal = form.closest('.modal');
        modal.addEventListener('hide.bs.modal', event => { if (form.dataset.submitting) event.preventDefault(); });
        modal.addEventListener('shown.bs.modal', () => {
            (form.querySelector('.is-invalid') || form.querySelector('[data-form-error]:not(.d-none)') || fields[0])?.focus();
        });
        if (modal.dataset.reopen === 'true') bootstrap.Modal.getOrCreateInstance(modal).show();
    });
    window.addEventListener('pageshow', () => forms.forEach(resetBusy));

    document.querySelectorAll('[data-supplier-edit]').forEach(button => button.addEventListener('click', () => {
        const form = document.getElementById('editSupplierForm');
        if (!form) return;
        resetBusy(form);
        form.action = button.dataset.url;
        const values = { Name: button.dataset.name, PhoneNumber: button.dataset.phone, ContactName: button.dataset.contact, Email: button.dataset.email, Address: button.dataset.address };
        Object.entries(values).forEach(([name, value]) => {
            const input = form.elements.namedItem(name);
            input.value = value || '';
            input.classList.remove('is-invalid');
            input.setAttribute('aria-invalid', 'false');
        });
        form.querySelectorAll('[data-field-error]').forEach(el => el.textContent = '');
        form.querySelector('[data-form-error]').classList.add('d-none');
        form.querySelector('input[type="checkbox"]').checked = button.dataset.active === 'true';
        bootstrap.Modal.getOrCreateInstance(form.closest('.modal')).show();
    }));
    document.querySelectorAll('[data-supplier-delete]').forEach(button => button.addEventListener('click', () => {
        showConfirmModal(button.dataset.url, `Bạn có chắc chắn muốn vô hiệu hóa nhà cung cấp "${button.dataset.name}" không?`, 'Vô hiệu hóa', 'btn-warning');
    }));
    document.getElementById('supplierSearchInput')?.addEventListener('input', event => {
        const query = event.target.value.toLowerCase().trim();
        let count = 0;
        document.querySelectorAll('.supplier-row').forEach(row => {
            const visible = ['name', 'contact', 'phone', 'email'].some(key => (row.dataset[key] || '').toLowerCase().includes(query));
            row.style.display = visible ? '' : 'none';
            if (visible) count++;
        });
        document.getElementById('supplierCountBadge').textContent = `${count} đối tác`;
    });
})();
