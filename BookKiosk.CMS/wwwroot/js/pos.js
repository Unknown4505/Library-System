/**
 * ====================================================================
 * POS BÁN TẠI QUẦY (PHÂN HỆ G) - CLIENT JAVASCRIPT
 * Module hóa: Scan, Cart, Member, Promo, Checkout, Timer, Poll, Clock
 * Tuân thủ nghiêm ngặt Thin Client: Không tự tính tiền giảm/thực trả
 * ====================================================================
 */

(function (window, document) {
    'use strict';

    // Namespace toàn cục
    const PosApp = {};

    // Helper trích xuất Anti-Forgery Token
    function getAntiForgeryToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    // Helper định dạng tiền VNĐ chuẩn hiển thị
    function formatMoneyVnd(amount) {
        if (typeof amount !== 'number') amount = Number(amount) || 0;
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
    }

    // Helper định dạng ngày giờ VN
    function formatDateTime(dtStr) {
        if (!dtStr) return '--/--/---- --:--';
        try {
            const dt = new Date(dtStr);
            return dt.toLocaleString('vi-VN', {
                hour: '2-digit', minute: '2-digit', second: '2-digit',
                day: '2-digit', month: '2-digit', year: 'numeric'
            });
        } catch {
            return dtStr;
        }
    }

    // ====================================================================
    // 1. SCAN MODULE: Súng quét mã vạch & Quản lý Focus
    // ====================================================================
    PosApp.ScanModule = (function () {
        let keyBuffer = '';
        let lastKeyTime = Date.now();
        const SCANNER_THRESHOLD_MS = 50;

        function init() {
            window.addEventListener('keydown', handleGlobalKeydown);

            const devInput = document.getElementById('devBarcodeInput');
            const devBtn = document.getElementById('btnDevBarcodeScan');
            if (devInput && devBtn) {
                devBtn.addEventListener('click', function () {
                    const code = devInput.value.trim();
                    if (code) {
                        PosApp.CartModule.addByBarcode(code);
                        devInput.value = '';
                    }
                });

                devInput.addEventListener('keydown', function (e) {
                    if (e.key === 'Enter') {
                        e.preventDefault();
                        devBtn.click();
                    }
                });
            }
        }

        function handleGlobalKeydown(e) {
            // Phím tắt F9: Chỉ tạo đơn hàng ở trạng thái Draft
            if (e.key === 'F9') {
                e.preventDefault();
                const draftPanel = document.getElementById('posDraftPanel');
                const checkoutBtn = document.getElementById('btnCheckoutOrder');
                if (draftPanel && !draftPanel.classList.contains('d-none') && checkoutBtn && !checkoutBtn.disabled) {
                    checkoutBtn.click();
                }
                return;
            }

            const activeTag = document.activeElement ? document.activeElement.tagName : '';
            const isModalOpen = document.querySelector('.modal.show');
            if (isModalOpen || ['INPUT', 'SELECT', 'TEXTAREA'].includes(activeTag)) {
                return;
            }

            const currentTime = Date.now();
            const timeDiff = currentTime - lastKeyTime;
            lastKeyTime = currentTime;

            if (e.key === 'Enter') {
                if (keyBuffer.length >= 3) {
                    e.preventDefault();
                    const barcode = keyBuffer;
                    keyBuffer = '';
                    PosApp.CartModule.addByBarcode(barcode);
                } else {
                    keyBuffer = '';
                }
                return;
            }

            if (e.key.length === 1) {
                if (timeDiff > SCANNER_THRESHOLD_MS && keyBuffer.length > 0) {
                    keyBuffer = '';
                }
                keyBuffer += e.key;
            }
        }

        function refocusScanner() {
            const activeTag = document.activeElement ? document.activeElement.tagName : '';
            const isModalOpen = document.querySelector('.modal.show');
            if (!isModalOpen && !['INPUT', 'SELECT', 'TEXTAREA'].includes(activeTag)) {
                const devInput = document.getElementById('devBarcodeInput');
                if (devInput) {
                    devInput.focus();
                }
            }
        }

        return {
            init: init,
            refocusScanner: refocusScanner
        };
    })();

    // ====================================================================
    // 2. CART MODULE: Quản lý giỏ hàng & Tính Tạm tính Preview
    // ====================================================================
    PosApp.CartModule = (function () {
        let cartItems = [];

        function init() {
            const btnClear = document.getElementById('btnClearCart');
            if (btnClear) {
                btnClear.addEventListener('click', function () {
                    if (cartItems.length === 0) return;
                    if (confirm('Bạn có chắc chắn muốn xóa toàn bộ sản phẩm trong giỏ hàng hiện tại?')) {
                        clearCart();
                        if (typeof showToast === 'function') {
                            showToast('Đã xóa sạch giỏ hàng.', 'info');
                        }
                    }
                });
            }

            const btnSearch = document.getElementById('btnSearchBooks');
            const searchKeyword = document.getElementById('posSearchKeyword');
            const searchCategory = document.getElementById('posSearchCategory');
            const btnClearSearch = document.getElementById('btnClearSearch');
            const btnCloseSearchResults = document.getElementById('btnCloseSearchResults');

            if (btnSearch && searchKeyword) {
                btnSearch.addEventListener('click', performBookSearch);
                searchKeyword.addEventListener('keydown', function (e) {
                    if (e.key === 'Enter') {
                        e.preventDefault();
                        performBookSearch();
                    }
                });
            }

            if (btnClearSearch) {
                btnClearSearch.addEventListener('click', function () {
                    searchKeyword.value = '';
                    searchCategory.value = '';
                    hideSearchResults();
                });
            }

            if (btnCloseSearchResults) {
                btnCloseSearchResults.addEventListener('click', hideSearchResults);
            }
        }

        async function addByBarcode(barcode) {
            barcode = barcode.trim();
            if (!barcode) return;

            if (typeof showLoading === 'function') showLoading('Đang tra cứu mã vạch...');

            try {
                const response = await fetch(`/Pos/ScanBarcode?barcode=${encodeURIComponent(barcode)}`, {
                    headers: { 'Accept': 'application/json' }
                });
                const result = await response.json();

                if (result.success && result.data) {
                    addItemFromApi(result.data);
                } else {
                    if (typeof showToast === 'function') {
                        showToast(result.message || `Không tìm thấy sách với mã vạch: ${barcode}`, 'warning');
                    }
                }
            } catch (err) {
                console.error('Lỗi khi quét barcode:', err);
                if (typeof showToast === 'function') {
                    showToast('Lỗi kết nối khi quét mã vạch.', 'danger');
                }
            } finally {
                if (typeof hideLoading === 'function') hideLoading();
                PosApp.ScanModule.refocusScanner();
            }
        }

        function addItemFromApi(book) {
            if (!book.isActive) {
                if (typeof showToast === 'function') {
                    showToast(`Sách "${book.title}" đã ngừng kinh doanh.`, 'warning');
                }
                return;
            }

            if (book.availableStock <= 0) {
                if (typeof showToast === 'function') {
                    showToast(`Sách "${book.title}" hiện đã hết hàng trong kho.`, 'danger');
                }
                return;
            }

            const existing = cartItems.find(i => i.bookId === book.bookId);
            if (existing) {
                if (existing.quantity + 1 > book.availableStock) {
                    if (typeof showToast === 'function') {
                        showToast(`Không thể thêm! Tồn kho khả dụng chỉ còn ${book.availableStock} cuốn.`, 'warning');
                    }
                    return;
                }
                existing.quantity += 1;
            } else {
                cartItems.push({
                    bookId: book.bookId,
                    barcode: book.barcode,
                    title: book.title,
                    unitPrice: Number(book.sellingPrice) || 0,
                    availableStock: Number(book.availableStock) || 0,
                    quantity: 1
                });
            }

            renderCart();
            if (typeof showToast === 'function') {
                showToast(`Đã thêm: ${book.title}`, 'success');
            }
        }

        function updateQuantity(bookId, delta) {
            const item = cartItems.find(i => i.bookId === bookId);
            if (!item) return;

            const targetQty = item.quantity + delta;

            if (targetQty <= 0) {
                if (confirm(`Bạn có muốn xóa cuốn "${item.title}" khỏi giỏ hàng?`)) {
                    removeItem(bookId);
                }
                return;
            }

            if (targetQty > item.availableStock) {
                if (typeof showToast === 'function') {
                    showToast(`Không thể vượt quá tồn kho khả dụng (${item.availableStock} cuốn)!`, 'warning');
                }
                return;
            }

            item.quantity = targetQty;
            renderCart();
        }

        function removeItem(bookId) {
            cartItems = cartItems.filter(i => i.bookId !== bookId);
            renderCart();
        }

        function clearCart() {
            cartItems = [];
            renderCart();
        }

        function calculateSubTotal() {
            return cartItems.reduce((sum, item) => sum + (item.unitPrice * item.quantity), 0);
        }

        function renderCart() {
            const tbody = document.getElementById('posCartTableBody');
            const emptyRow = document.getElementById('cartEmptyRow');
            const badgeCount = document.getElementById('cartItemCountBadge');
            const subTotalDisplay = document.getElementById('posSubTotalPrice');
            const checkoutBtn = document.getElementById('btnCheckoutOrder');

            if (!tbody) return;

            const oldRows = tbody.querySelectorAll('.cart-item-row');
            oldRows.forEach(r => r.remove());

            if (cartItems.length === 0) {
                if (emptyRow) emptyRow.classList.remove('d-none');
                if (badgeCount) badgeCount.textContent = '0';
                if (subTotalDisplay) subTotalDisplay.textContent = '0 ₫';
                if (checkoutBtn) checkoutBtn.disabled = true;
                PosApp.PromoModule.suggestBestPromo(0);
                return;
            }

            if (emptyRow) emptyRow.classList.add('d-none');

            let totalQty = 0;

            cartItems.forEach((item, index) => {
                totalQty += item.quantity;
                const lineTotal = item.unitPrice * item.quantity;

                const tr = document.createElement('tr');
                tr.className = 'cart-item-row';
                tr.dataset.bookId = item.bookId;

                // 1. STT
                const tdStt = document.createElement('td');
                tdStt.className = 'text-center fw-semibold text-muted';
                tdStt.textContent = (index + 1).toString();
                tr.appendChild(tdStt);

                // 2. Tên sách & Mã vạch
                const tdTitle = document.createElement('td');
                tdTitle.className = 'cart-item-title-col';
                const titleDiv = document.createElement('div');
                titleDiv.className = 'fw-bold text-dark text-truncate';
                titleDiv.style.maxWidth = '250px';
                titleDiv.title = item.title;
                titleDiv.textContent = item.title;

                const barcodeSpan = document.createElement('span');
                barcodeSpan.className = 'badge bg-light text-secondary border font-monospace small';
                barcodeSpan.textContent = item.barcode;

                tdTitle.appendChild(titleDiv);
                tdTitle.appendChild(barcodeSpan);
                tr.appendChild(tdTitle);

                // 3. Đơn giá
                const tdPrice = document.createElement('td');
                tdPrice.className = 'text-end fw-semibold text-dark';
                tdPrice.textContent = formatMoneyVnd(item.unitPrice);
                tr.appendChild(tdPrice);

                // 4. Số lượng
                const tdQty = document.createElement('td');
                tdQty.className = 'text-center';

                const qtyGroup = document.createElement('div');
                qtyGroup.className = 'd-inline-flex align-items-center gap-1';

                const btnMinus = document.createElement('button');
                btnMinus.type = 'button';
                btnMinus.className = 'btn btn-sm btn-outline-secondary qty-btn';
                btnMinus.textContent = '-';
                btnMinus.addEventListener('click', () => updateQuantity(item.bookId, -1));

                const qtySpan = document.createElement('span');
                qtySpan.className = 'qty-input fw-bold';
                qtySpan.textContent = item.quantity.toString();

                const btnPlus = document.createElement('button');
                btnPlus.type = 'button';
                btnPlus.className = 'btn btn-sm btn-outline-secondary qty-btn';
                btnPlus.textContent = '+';
                btnPlus.addEventListener('click', () => updateQuantity(item.bookId, 1));

                qtyGroup.appendChild(btnMinus);
                qtyGroup.appendChild(qtySpan);
                qtyGroup.appendChild(btnPlus);
                tdQty.appendChild(qtyGroup);
                tr.appendChild(tdQty);

                // 5. Tồn kho
                const tdStock = document.createElement('td');
                tdStock.className = 'text-center';
                const stockBadge = document.createElement('span');
                if (item.availableStock <= 3) {
                    stockBadge.className = 'badge badge-stock-low small';
                } else {
                    stockBadge.className = 'badge badge-stock-ample small';
                }
                stockBadge.textContent = item.availableStock.toString();
                tdStock.appendChild(stockBadge);
                tr.appendChild(tdStock);

                // 6. Thành tiền
                const tdLineTotal = document.createElement('td');
                tdLineTotal.className = 'text-end fw-bold text-primary cart-item-linetotal-col';
                tdLineTotal.textContent = formatMoneyVnd(lineTotal);
                tr.appendChild(tdLineTotal);

                // 7. Xóa
                const tdAction = document.createElement('td');
                tdAction.className = 'text-center';
                const btnRemove = document.createElement('button');
                btnRemove.type = 'button';
                btnRemove.className = 'btn btn-sm btn-link text-danger p-0';
                btnRemove.title = 'Xóa dòng';
                btnRemove.innerHTML = '<i class="fas fa-times-circle fs-6"></i>';
                btnRemove.addEventListener('click', () => removeItem(item.bookId));
                tdAction.appendChild(btnRemove);
                tr.appendChild(tdAction);

                tbody.appendChild(tr);
            });

            const subTotal = calculateSubTotal();
            if (badgeCount) badgeCount.textContent = totalQty.toString();
            if (subTotalDisplay) subTotalDisplay.textContent = formatMoneyVnd(subTotal);
            if (checkoutBtn) checkoutBtn.disabled = false;

            PosApp.PromoModule.suggestBestPromo(subTotal);
        }

        async function performBookSearch() {
            const keyword = document.getElementById('posSearchKeyword').value.trim();
            const categoryId = document.getElementById('posSearchCategory').value;

            if (!keyword && !categoryId) {
                if (typeof showToast === 'function') {
                    showToast('Vui lòng nhập từ khóa hoặc chọn danh mục để tìm sách.', 'info');
                }
                return;
            }

            if (typeof showLoading === 'function') showLoading('Đang tìm kiếm sách...');

            try {
                let url = `/Pos/SearchBooks?keyword=${encodeURIComponent(keyword)}&page=1&pageSize=12`;
                if (categoryId) url += `&categoryId=${encodeURIComponent(categoryId)}`;

                const response = await fetch(url, { headers: { 'Accept': 'application/json' } });
                const result = await response.json();

                if (result.success && result.data && result.data.items) {
                    renderSearchResults(result.data.items);
                } else {
                    if (typeof showToast === 'function') {
                        showToast(result.message || 'Không tìm thấy sách phù hợp.', 'info');
                    }
                }
            } catch (err) {
                console.error('Lỗi tìm kiếm sách:', err);
                if (typeof showToast === 'function') {
                    showToast('Lỗi kết nối khi tìm kiếm sách.', 'danger');
                }
            } finally {
                if (typeof hideLoading === 'function') hideLoading();
            }
        }

        function renderSearchResults(books) {
            const container = document.getElementById('posSearchResultsContainer');
            const list = document.getElementById('posSearchResultsList');
            const countSpan = document.getElementById('searchResultCount');

            if (!container || !list) return;

            list.innerHTML = '';
            if (countSpan) countSpan.textContent = books.length.toString();

            if (books.length === 0) {
                list.innerHTML = '<div class="col-12 text-center py-3 text-muted small">Không tìm thấy sách nào phù hợp.</div>';
            } else {
                books.forEach(book => {
                    const col = document.createElement('div');
                    col.className = 'col-md-6 col-lg-4';

                    const card = document.createElement('div');
                    card.className = 'card h-100 border shadow-sm p-2';

                    const titleDiv = document.createElement('div');
                    titleDiv.className = 'fw-bold text-dark small text-truncate mb-1';
                    titleDiv.textContent = book.title;
                    titleDiv.title = book.title;

                    const priceAndStock = document.createElement('div');
                    priceAndStock.className = 'd-flex justify-content-between align-items-center mb-2';

                    const priceSpan = document.createElement('span');
                    priceSpan.className = 'fw-bold text-primary small';
                    priceSpan.textContent = formatMoneyVnd(book.sellingPrice);

                    const stockSpan = document.createElement('span');
                    stockSpan.className = book.availableStock > 0 ? 'badge bg-light text-dark border small' : 'badge bg-danger small';
                    stockSpan.textContent = book.availableStock > 0 ? `Còn ${book.availableStock}` : 'Hết hàng';

                    priceAndStock.appendChild(priceSpan);
                    priceAndStock.appendChild(stockSpan);

                    const btnAdd = document.createElement('button');
                    btnAdd.type = 'button';
                    btnAdd.className = 'btn btn-xs btn-outline-primary w-100 fw-semibold';
                    btnAdd.textContent = '+ Thêm vào đơn';
                    if (!book.isActive || book.availableStock <= 0) {
                        btnAdd.disabled = true;
                        btnAdd.className = 'btn btn-xs btn-secondary w-100';
                        btnAdd.textContent = 'Hết hàng';
                    } else {
                        btnAdd.addEventListener('click', () => {
                            addItemFromApi(book);
                        });
                    }

                    card.appendChild(titleDiv);
                    card.appendChild(priceAndStock);
                    card.appendChild(btnAdd);
                    col.appendChild(card);
                    list.appendChild(col);
                });
            }

            container.classList.remove('d-none');
        }

        function hideSearchResults() {
            const container = document.getElementById('posSearchResultsContainer');
            if (container) container.classList.add('d-none');
        }

        function getItems() {
            return cartItems.map(i => ({ bookId: i.bookId, quantity: i.quantity }));
        }

        return {
            init: init,
            addByBarcode: addByBarcode,
            addItemFromApi: addItemFromApi,
            clearCart: clearCart,
            calculateSubTotal: calculateSubTotal,
            getItems: getItems
        };
    })();

    // ====================================================================
    // 3. MEMBER MODULE: Tra cứu SĐT, Dùng điểm & Đăng ký nhanh
    // ====================================================================
    PosApp.MemberModule = (function () {
        let currentMember = null;

        function init() {
            const btnLookup = document.getElementById('btnLookupMember');
            const phoneInput = document.getElementById('posMemberPhoneInput');
            const btnRemove = document.getElementById('btnRemoveSelectedMember');
            const pointsInput = document.getElementById('posPointsToUseInput');
            const btnUseMax = document.getElementById('btnUseMaxPoints');

            if (btnLookup && phoneInput) {
                btnLookup.addEventListener('click', lookupMember);
                phoneInput.addEventListener('keydown', function (e) {
                    if (e.key === 'Enter') {
                        e.preventDefault();
                        lookupMember();
                    }
                });
            }

            if (btnRemove) {
                btnRemove.addEventListener('click', removeSelectedMember);
            }

            if (pointsInput) {
                pointsInput.addEventListener('input', handlePointsInputChange);
            }

            if (btnUseMax) {
                btnUseMax.addEventListener('click', function () {
                    if (!currentMember) return;
                    const maxAllowed = Math.min(100, currentMember.points || 0);
                    pointsInput.value = maxAllowed;
                    handlePointsInputChange();
                });
            }

            const btnOpenModal = document.getElementById('btnOpenQuickMemberModal');
            const btnSubmitQuick = document.getElementById('btnSubmitQuickMember');

            if (btnOpenModal) {
                btnOpenModal.addEventListener('click', function () {
                    const modalEl = document.getElementById('quickMemberModal');
                    if (modalEl) {
                        const alertBox = document.getElementById('quickMemberErrorAlert');
                        if (alertBox) alertBox.classList.add('d-none');
                        document.getElementById('quickMemberPhone').value = phoneInput.value.trim();
                        document.getElementById('quickMemberName').value = '';
                        const modal = new bootstrap.Modal(modalEl);
                        modal.show();
                    }
                });
            }

            if (btnSubmitQuick) {
                btnSubmitQuick.addEventListener('click', submitQuickRegister);
            }
        }

        async function lookupMember() {
            const phoneInput = document.getElementById('posMemberPhoneInput');
            const phone = phoneInput ? phoneInput.value.trim() : '';

            if (!phone || !/^(03|05|07|08|09)\d{8}$/.test(phone)) {
                if (typeof showToast === 'function') {
                    showToast('Vui lòng nhập số điện thoại di động hợp lệ (10 số, bắt đầu bằng 03, 05, 07, 08, 09).', 'warning');
                }
                return;
            }

            if (typeof showLoading === 'function') showLoading('Đang tra cứu thành viên...');

            try {
                const response = await fetch(`/Pos/LookupMember?phone=${encodeURIComponent(phone)}`, {
                    headers: { 'Accept': 'application/json' }
                });
                const result = await response.json();

                if (result.success && result.data) {
                    setSelectedMember(result.data);
                    if (typeof showToast === 'function') {
                        showToast(`Đã tìm thấy thành viên: ${result.data.fullName}`, 'success');
                    }
                } else {
                    if (typeof showToast === 'function') {
                        showToast(result.message || 'Không tìm thấy thành viên với số điện thoại này.', 'info');
                    }
                }
            } catch (err) {
                console.error('Lỗi tra cứu thành viên:', err);
                if (typeof showToast === 'function') {
                    showToast('Lỗi kết nối khi tra cứu thành viên.', 'danger');
                }
            } finally {
                if (typeof hideLoading === 'function') hideLoading();
                PosApp.ScanModule.refocusScanner();
            }
        }

        function setSelectedMember(member) {
            currentMember = member;

            const formGroup = document.getElementById('memberLookupFormGroup');
            const selectedBox = document.getElementById('selectedMemberBox');
            const nameSpan = document.getElementById('selectedMemberName');
            const phoneSpan = document.getElementById('selectedMemberPhone');
            const pointsSpan = document.getElementById('selectedMemberPoints');
            const pointsInput = document.getElementById('posPointsToUseInput');
            const btnUseMax = document.getElementById('btnUseMaxPoints');

            if (nameSpan) nameSpan.textContent = member.fullName;
            if (phoneSpan) phoneSpan.textContent = member.phoneNumber;
            if (pointsSpan) pointsSpan.textContent = (member.points || 0).toLocaleString('vi-VN');

            if (formGroup) formGroup.classList.add('d-none');
            if (selectedBox) selectedBox.classList.remove('d-none');

            if (pointsInput) {
                pointsInput.disabled = false;
                pointsInput.max = Math.min(100, member.points || 0).toString();
                pointsInput.value = '0';
            }
            if (btnUseMax) btnUseMax.disabled = false;

            handlePointsInputChange();
        }

        function removeSelectedMember() {
            currentMember = null;

            const formGroup = document.getElementById('memberLookupFormGroup');
            const selectedBox = document.getElementById('selectedMemberBox');
            const pointsInput = document.getElementById('posPointsToUseInput');
            const btnUseMax = document.getElementById('btnUseMaxPoints');

            if (formGroup) formGroup.classList.remove('d-none');
            if (selectedBox) selectedBox.classList.add('d-none');

            if (pointsInput) {
                pointsInput.value = '0';
                pointsInput.disabled = true;
            }
            if (btnUseMax) btnUseMax.disabled = true;

            handlePointsInputChange();
            PosApp.ScanModule.refocusScanner();
        }

        function handlePointsInputChange() {
            const pointsInput = document.getElementById('posPointsToUseInput');
            const previewSpan = document.getElementById('pointsDeductionPreview');
            if (!pointsInput || !previewSpan) return;

            let pts = parseInt(pointsInput.value, 10);
            if (isNaN(pts) || pts < 0) pts = 0;

            const maxAllowed = currentMember ? Math.min(100, currentMember.points || 0) : 0;
            if (pts > maxAllowed) {
                pts = maxAllowed;
                pointsInput.value = pts;
            }

            previewSpan.textContent = `${pts} điểm = ${formatMoneyVnd(pts * 1000)}`;
        }

        async function submitQuickRegister() {
            const phone = document.getElementById('quickMemberPhone').value.trim();
            const name = document.getElementById('quickMemberName').value.trim();
            const alertBox = document.getElementById('quickMemberErrorAlert');

            if (!phone || !/^(03|05|07|08|09)\d{8}$/.test(phone)) {
                if (alertBox) {
                    alertBox.textContent = 'Số điện thoại không đúng định dạng di động 10 số!';
                    alertBox.classList.remove('d-none');
                }
                return;
            }

            if (!name) {
                if (alertBox) {
                    alertBox.textContent = 'Họ và tên thành viên không được để trống!';
                    alertBox.classList.remove('d-none');
                }
                return;
            }

            if (typeof showLoading === 'function') showLoading('Đang đăng ký thành viên...');

            try {
                const response = await fetch('/Pos/QuickRegisterMember', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': getAntiForgeryToken()
                    },
                    body: JSON.stringify({ phoneNumber: phone, fullName: name })
                });

                const result = await response.json();

                if (result.success && result.data) {
                    const modalEl = document.getElementById('quickMemberModal');
                    const modal = bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();

                    setSelectedMember(result.data);
                    if (typeof showToast === 'function') {
                        showToast(`Đăng ký thành công thành viên: ${result.data.fullName}`, 'success');
                    }
                } else {
                    if (alertBox) {
                        alertBox.textContent = result.message || 'Đăng ký thành viên thất bại.';
                        alertBox.classList.remove('d-none');
                    }
                }
            } catch (err) {
                console.error('Lỗi đăng ký nhanh thành viên:', err);
                if (alertBox) {
                    alertBox.textContent = 'Lỗi kết nối khi đăng ký thành viên.';
                    alertBox.classList.remove('d-none');
                }
            } finally {
                if (typeof hideLoading === 'function') hideLoading();
                PosApp.ScanModule.refocusScanner();
            }
        }

        function getMemberId() {
            return currentMember ? currentMember.memberId : null;
        }

        function getPointsToUse() {
            const pointsInput = document.getElementById('posPointsToUseInput');
            if (!pointsInput || pointsInput.disabled) return 0;
            return parseInt(pointsInput.value, 10) || 0;
        }

        return {
            init: init,
            getMemberId: getMemberId,
            getPointsToUse: getPointsToUse
        };
    })();

    // ====================================================================
    // 4. PROMO MODULE: Chọn Khuyến mãi Hóa đơn & Gợi ý KM tốt nhất
    // ====================================================================
    PosApp.PromoModule = (function () {
        function init() {
            const selectEl = document.getElementById('posPromotionSelect');
            if (selectEl) {
                selectEl.addEventListener('change', function () {
                    PosApp.ScanModule.refocusScanner();
                });
            }
        }

        function suggestBestPromo(currentSubTotal) {
            const selectEl = document.getElementById('posPromotionSelect');
            const badgeEl = document.getElementById('promoSuggestionBadge');
            const nameEl = document.getElementById('promoSuggestedName');

            if (!selectEl || !badgeEl || !nameEl) return;

            let bestOption = null;
            let maxDiscount = 0;

            Array.from(selectEl.options).forEach(opt => {
                if (!opt.value) return;
                const minVal = parseFloat(opt.getAttribute('data-min-value')) || 0;
                const discount = parseFloat(opt.getAttribute('data-discount')) || 0;

                if (currentSubTotal >= minVal && discount > maxDiscount) {
                    maxDiscount = discount;
                    bestOption = opt;
                }
            });

            if (bestOption) {
                nameEl.textContent = bestOption.text;
                badgeEl.classList.remove('d-none');
            } else {
                badgeEl.classList.add('d-none');
            }
        }

        function getSelectedPromotionId() {
            const selectEl = document.getElementById('posPromotionSelect');
            if (!selectEl || !selectEl.value) return null;
            return parseInt(selectEl.value, 10) || null;
        }

        return {
            init: init,
            suggestBestPromo: suggestBestPromo,
            getSelectedPromotionId: getSelectedPromotionId
        };
    })();

    // ====================================================================
    // 5. TIMER MODULE: Đếm ngược thời gian thanh toán (JS dùng performance.now())
    // ====================================================================
    PosApp.TimerModule = (function () {
        let timerTimeoutId = null;
        let initialRemainingSeconds = 240;
        let startPerformanceTime = 0;
        let isRunning = false;

        function start(remainingSeconds, expiresAtUtc, serverTimeUtc) {
            stop();

            // Nếu server đã tính remainingSeconds thì ưu tiên dùng trực tiếp
            if (typeof remainingSeconds === 'number' && remainingSeconds >= 0) {
                initialRemainingSeconds = remainingSeconds;
            } else if (expiresAtUtc) {
                const targetMs = new Date(expiresAtUtc).getTime();
                const serverOffset = serverTimeUtc ? (new Date(serverTimeUtc).getTime() - Date.now()) : 0;
                const diffMs = targetMs - (Date.now() + serverOffset);
                initialRemainingSeconds = Math.max(0, Math.floor(diffMs / 1000));
            } else {
                initialRemainingSeconds = 240; // 4 phút mặc định
            }

            startPerformanceTime = performance.now();
            isRunning = true;
            renderTimer(initialRemainingSeconds);

            function scheduleNextTick() {
                if (!isRunning) return;

                const elapsedSeconds = (performance.now() - startPerformanceTime) / 1000;
                const currentRemaining = Math.max(0, Math.ceil(initialRemainingSeconds - elapsedSeconds));

                renderTimer(currentRemaining);

                if (currentRemaining <= 0) {
                    stop();
                    handleTimeoutExpiry();
                } else {
                    timerTimeoutId = setTimeout(scheduleNextTick, 250);
                }
            }

            timerTimeoutId = setTimeout(scheduleNextTick, 250);
        }

        function stop() {
            isRunning = false;
            if (timerTimeoutId) {
                clearTimeout(timerTimeoutId);
                timerTimeoutId = null;
            }
        }

        function renderTimer(seconds) {
            const timerSpan = document.getElementById('pendingCountdownTimer');
            const timerBadge = document.getElementById('pendingTimerBadge');
            if (!timerSpan) return;

            const mins = Math.floor(seconds / 60);
            const secs = seconds % 60;
            const formatted = `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
            timerSpan.textContent = formatted;

            if (timerBadge) {
                if (seconds <= 60) {
                    timerBadge.className = 'badge bg-danger text-white font-monospace fs-6';
                } else {
                    timerBadge.className = 'badge bg-warning text-dark font-monospace fs-6';
                }
            }
        }

        async function handleTimeoutExpiry() {
            console.warn('[POS Timer] Đơn hàng đã về 00:00. Kích hoạt quy trình kiểm tra hết hạn...');
            PosApp.CheckoutModule.handleOrderTimeoutExpired();
        }

        return {
            start: start,
            stop: stop
        };
    })();

    // ====================================================================
    // 6. POLL MODULE: Polling trạng thái đơn hàng (3s/lần cho nhánh QR)
    // ====================================================================
    PosApp.PollModule = (function () {
        let pollInterval = null;
        let pollingOrderId = null;

        function startPolling(orderId) {
            stopPolling();
            pollingOrderId = orderId;

            pollInterval = setInterval(async function () {
                if (!pollingOrderId) return;

                try {
                    const response = await fetch(`/Pos/PollStatus?orderId=${pollingOrderId}`, {
                        headers: { 'Accept': 'application/json' }
                    });
                    const result = await response.json();

                    if (result.success && result.data) {
                        const statusData = result.data;
                        const statusStr = (statusData.status || statusData.statusString || '').toString().toLowerCase();

                        if (statusStr === 'paid' || statusData.isPaid) {
                            console.log('[POS Poll] Đơn hàng đã thanh toán thành công (Webhook Paid)!');
                            stopPolling();
                            PosApp.CheckoutModule.onOrderPaidByWebhook(pollingOrderId);
                        } else if (statusStr === 'cancelled' || statusData.isCancelled) {
                            console.log('[POS Poll] Đơn hàng đã bị hủy.');
                            stopPolling();
                            PosApp.CheckoutModule.onOrderCancelledRemotely();
                        }
                    }
                } catch (err) {
                    console.error('[POS Poll] Lỗi khi polling trạng thái đơn:', err);
                }
            }, 3000);
        }

        function stopPolling() {
            if (pollInterval) {
                clearInterval(pollInterval);
                pollInterval = null;
            }
            pollingOrderId = null;
        }

        return {
            startPolling: startPolling,
            stopPolling: stopPolling
        };
    })();

    // ====================================================================
    // 7. CHECKOUT MODULE: Vòng đời đơn hàng, Thanh toán & Hóa đơn K80
    // ====================================================================
    PosApp.CheckoutModule = (function () {
        let currentOrder = null;
        let isSubmitting = false;

        function init() {
            // Nút Tạo đơn hàng (F9)
            const btnCheckout = document.getElementById('btnCheckoutOrder');
            if (btnCheckout) {
                btnCheckout.addEventListener('click', createCounterOrder);
            }

            // Radio chuyển đổi hình thức thanh toán (Tiền mặt / QR)
            const radioCash = document.getElementById('payMethodCash');
            const radioQR = document.getElementById('payMethodQR');
            if (radioCash && radioQR) {
                radioCash.addEventListener('change', () => switchPaymentBranch('Cash'));
                radioQR.addEventListener('change', () => switchPaymentBranch('QR'));
            }

            // Tiền khách đưa (P2 Cash Change Calculator)
            const cashInput = document.getElementById('cashGivenInput');
            if (cashInput) {
                cashInput.addEventListener('input', calculateCashChange);
            }

            // Các nút gợi ý tiền nhanh
            const quickCashBtns = document.querySelectorAll('.btn-quick-cash');
            quickCashBtns.forEach(btn => {
                btn.addEventListener('click', function () {
                    if (!currentOrder) return;
                    const type = btn.getAttribute('data-type');
                    const amount = btn.getAttribute('data-amount');

                    if (type === 'exact') {
                        cashInput.value = Math.round(currentOrder.totalAmount);
                    } else if (amount) {
                        cashInput.value = amount;
                    }
                    calculateCashChange();
                });
            });

            // Nút Xác nhận thanh toán tiền mặt
            const btnConfirmCash = document.getElementById('btnConfirmCashPayment');
            if (btnConfirmCash) {
                btnConfirmCash.addEventListener('click', confirmCashPayment);
            }

            // Nút Mở modal Hủy đơn
            const btnOpenCancel = document.getElementById('btnOpenCancelModal');
            const btnConfirmCancel = document.getElementById('btnConfirmCancelOrder');
            if (btnOpenCancel) {
                btnOpenCancel.addEventListener('click', function () {
                    const modalEl = document.getElementById('cancelOrderModal');
                    if (modalEl) {
                        const modal = new bootstrap.Modal(modalEl);
                        modal.show();
                    }
                });
            }
            if (btnConfirmCancel) {
                btnConfirmCancel.addEventListener('click', executeCancelOrder);
            }

            // Nút In hóa đơn K80 (CMS-POS-PRINT-01: Chặn in khi chưa có hóa đơn chính thức)
            const btnPrint = document.getElementById('btnPrintReceipt');
            if (btnPrint) {
                btnPrint.addEventListener('click', function () {
                    const receiptArea = document.getElementById('receiptPrintArea');
                    const isOfficial = receiptArea && receiptArea.getAttribute('data-receipt-official') === 'true';
                    if (!isOfficial) {
                        if (typeof showToast === 'function') {
                            showToast('Chưa có hóa đơn chính thức - không dùng làm chứng từ.', 'warning');
                        }
                        return;
                    }
                    window.print();
                });
            }

            // Nút Tạo đơn mới sau khi in bill: Reset DOM và marker official
            const btnNewOrder = document.getElementById('btnReceiptNewOrder');
            if (btnNewOrder) {
                btnNewOrder.addEventListener('click', function () {
                    clearReceiptDom();
                    resetToDraft();
                });
            }

            // Reset marker và DOM khi đóng modal hóa đơn
            const receiptModalEl = document.getElementById('receiptModal');
            if (receiptModalEl) {
                receiptModalEl.addEventListener('hidden.bs.modal', function () {
                    clearReceiptDom();
                });
            }

            // CMS-POS-PRINT-01: Chặn mọi window.print() và dọn sạch DOM trước khi in nếu chưa chính thức
            window.addEventListener('beforeprint', function () {
                const receiptArea = document.getElementById('receiptPrintArea');
                const isOfficial = receiptArea && receiptArea.getAttribute('data-receipt-official') === 'true';
                if (!isOfficial) {
                    clearReceiptDom();
                }
            });

            const nativePrint = window.print.bind(window);
            window.print = function () {
                const receiptArea = document.getElementById('receiptPrintArea');
                const isOfficial = receiptArea && receiptArea.getAttribute('data-receipt-official') === 'true';
                if (!isOfficial) {
                    console.warn('In bị chặn: Chưa có hóa đơn chính thức từ máy chủ.');
                    if (typeof showToast === 'function') {
                        showToast('Chưa có hóa đơn chính thức - không dùng làm chứng từ.', 'warning');
                    }
                    return;
                }
                nativePrint();
            };

            // Nút Thử tải lại hóa đơn chính thức (CMS-POS-02)
            const btnRetryReceipt = document.getElementById('btnRetryFetchReceipt');
            if (btnRetryReceipt) {
                btnRetryReceipt.addEventListener('click', function () {
                    if (currentOrder) {
                        fetchOfficialReceipt(currentOrder.orderId);
                    }
                });
            }

            // Nút Thử lại & Bỏ qua cảnh báo EndpointUnavailable
            const btnRetry = document.getElementById('btnRetryEndpoint');
            const btnDismiss = document.getElementById('btnDismissEndpointWarning');
            if (btnRetry) {
                btnRetry.addEventListener('click', function () {
                    hideEndpointWarning();
                    createCounterOrder();
                });
            }
            if (btnDismiss) {
                btnDismiss.addEventListener('click', hideEndpointWarning);
            }

            // Nút Thử lại & Hủy đơn khi gặp lỗi QR
            const btnRetryQr = document.getElementById('btnRetryQrCheck');
            const btnCancelQr = document.getElementById('btnCancelFromQrError');
            if (btnRetryQr) {
                btnRetryQr.addEventListener('click', function () {
                    switchPaymentBranch('QR');
                });
            }
            if (btnCancelQr) {
                btnCancelQr.addEventListener('click', function () {
                    const btnOpenCancel = document.getElementById('btnOpenCancelModal');
                    if (btnOpenCancel) btnOpenCancel.click();
                });
            }
        }

        async function createCounterOrder() {
            if (isSubmitting) return;

            const items = PosApp.CartModule.getItems();
            if (items.length === 0) {
                if (typeof showToast === 'function') {
                    showToast('Giỏ hàng đang trống. Vui lòng thêm sách trước khi tạo đơn.', 'warning');
                }
                return;
            }

            isSubmitting = true;
            const checkoutBtn = document.getElementById('btnCheckoutOrder');
            const checkoutText = document.getElementById('btnCheckoutText');
            if (checkoutBtn) checkoutBtn.disabled = true;
            if (checkoutText) checkoutText.textContent = 'ĐANG TẠO ĐƠN...';

            if (typeof showLoading === 'function') showLoading('Đang kết nối Backend giữ kho sách...');

            const payload = {
                memberId: PosApp.MemberModule.getMemberId(),
                promotionId: PosApp.PromoModule.getSelectedPromotionId(),
                pointsToUse: PosApp.MemberModule.getPointsToUse(),
                paymentMethod: 'Cash',
                items: items
            };

            try {
                const response = await fetch('/Pos/Checkout', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': getAntiForgeryToken()
                    },
                    body: JSON.stringify(payload)
                });

                // Kiểm tra nếu HTTP pipeline trả lỗi mạng/500/unauthorized cấp middleware
                if (!response.ok) {
                    let errMsg = 'Lỗi máy chủ (' + response.status + '). Vui lòng thử lại.';
                    if (response.status === 401 || response.status === 403) {
                        errMsg = 'Phiên làm việc đã hết hạn hoặc không có quyền thực hiện. Vui lòng đăng nhập lại.';
                    }
                    if (typeof showToast === 'function') {
                        showToast(errMsg, 'danger');
                    }
                    return;
                }

                const result = await response.json();

                if (result.isEndpointUnavailable) {
                    showEndpointWarning(result.message);
                    return;
                }

                // Xử lý JSON bọc từ PosController.FormatApiResponse (statusCode nằm trong JSON body)
                if (result.success && result.data) {
                    hideEndpointWarning();
                    transitionToPending(result.data);
                } else {
                    const statusCode = result.statusCode || 400;
                    let toastType = 'danger';

                    if (statusCode === 401 || statusCode === 403) {
                        toastType = 'warning';
                    } else if (statusCode === 409) {
                        toastType = 'warning';
                    }

                    if (typeof showToast === 'function') {
                        showToast(result.message || 'Tạo đơn hàng thất bại từ Backend.', toastType);
                    }
                }
            } catch (err) {
                console.error('Lỗi khi tạo đơn hàng:', err);
                if (typeof showToast === 'function') {
                    showToast('Lỗi mạng hoặc không thể kết nối Backend.', 'danger');
                }
            } finally {
                isSubmitting = false;
                if (checkoutBtn) checkoutBtn.disabled = false;
                if (checkoutText) checkoutText.textContent = 'TẠO ĐƠN HÀNG (F9)';
                if (typeof hideLoading === 'function') hideLoading();
                PosApp.ScanModule.refocusScanner();
            }
        }

        function updateCartBreakdownFromPending(items) {
            if (!items || !Array.isArray(items) || items.length === 0) return;

            const tbody = document.getElementById('posCartTableBody');
            if (!tbody) return;

            // Kiểm tra xem đơn hàng có dữ liệu chiết khấu V7.1 hay không
            const hasV71Discount = items.some(function (i) {
                return (Number(i.discountPerUnit) > 0 || Number(i.lineDiscountAmount) > 0);
            });

            // Nếu đơn không có khuyến mãi V7.1, giữ nguyên hiển thị niêm yết cũ
            if (!hasV71Discount) return;

            // Xây dựng bản đồ dòng giỏ hàng theo data-book-id để ghép chính xác theo BookId (Backend OrderBy BookId)
            const rowMap = new Map();
            tbody.querySelectorAll('.cart-item-row[data-book-id]').forEach(function (row) {
                rowMap.set(Number(row.dataset.bookId), row);
            });

            // Duyệt từng item từ Backend và tìm hàng theo bookId
            items.forEach(function (serverItem) {
                const tr = rowMap.get(Number(serverItem.bookId));
                if (!tr) return; // Nếu không tìm thấy dòng sách trong giỏ thì bỏ qua an toàn

                const discountPerUnit = Number(serverItem.discountPerUnit) || 0;
                const lineDiscount = Number(serverItem.lineDiscountAmount) || 0;
                const netLineTotal = Number(serverItem.netLineTotal) || (serverItem.lineTotal - lineDiscount);

                // 1. Cập nhật cột Tên sách: nếu có giảm theo cuốn thì thêm badge chiết khấu
                if (discountPerUnit > 0) {
                    const tdTitle = tr.querySelector('.cart-item-title-col') || tr.children[1];
                    if (tdTitle && !tdTitle.querySelector('.promo-unit-badge')) {
                        const promoBadge = document.createElement('div');
                        promoBadge.className = 'badge bg-success-subtle text-success border border-success-subtle small mt-1 promo-unit-badge';
                        promoBadge.textContent = 'Giảm -' + formatMoneyVnd(discountPerUnit) + '/cuốn';
                        tdTitle.appendChild(promoBadge);
                    }
                }

                // 2. Cập nhật cột Thành tiền: xác định bằng class selector ổn định (.cart-item-linetotal-col)
                const tdLineTotal = tr.querySelector('.cart-item-linetotal-col') || tr.children[5];
                if (tdLineTotal) {
                    while (tdLineTotal.firstChild) {
                        tdLineTotal.removeChild(tdLineTotal.firstChild);
                    }

                    if (lineDiscount > 0) {
                        const originalSpan = document.createElement('div');
                        originalSpan.className = 'text-decoration-line-through text-muted small';
                        originalSpan.textContent = formatMoneyVnd(serverItem.lineTotal);
                        tdLineTotal.appendChild(originalSpan);

                        const netSpan = document.createElement('div');
                        netSpan.className = 'fw-bold text-danger fs-6';
                        netSpan.textContent = formatMoneyVnd(netLineTotal);
                        tdLineTotal.appendChild(netSpan);
                    } else {
                        const normalSpan = document.createElement('span');
                        normalSpan.className = 'fw-bold text-primary';
                        normalSpan.textContent = formatMoneyVnd(serverItem.lineTotal);
                        tdLineTotal.appendChild(normalSpan);
                    }
                }
            });
        }

        function transitionToPending(orderData) {
            currentOrder = orderData;
            currentOrder.paymentMethod = 'Cash';

            // 1. Khóa giỏ hàng sang chế độ Read-only
            const cartCard = document.getElementById('posCartCard');
            const scanCard = document.getElementById('posScanSearchCard');
            if (cartCard) cartCard.classList.add('pos-cart-readonly');
            if (scanCard) scanCard.classList.add('pos-cart-readonly');

            // 2. Chuyển đổi panel hiển thị Cột Phải
            const draftPanel = document.getElementById('posDraftPanel');
            const pendingPanel = document.getElementById('posPendingPanel');
            if (draftPanel) draftPanel.classList.add('d-none');
            if (pendingPanel) pendingPanel.classList.remove('d-none');

            // 3. Bind bảng tiền CHÍNH THỨC từ Backend
            document.getElementById('pendingOrderCode').textContent = orderData.orderCode;
            document.getElementById('pendingSubTotal').textContent = formatMoneyVnd(orderData.subTotal);

            const discountRow = document.getElementById('pendingDiscountRow');
            const discountAmount = document.getElementById('pendingDiscountAmount');
            const promoNameEl = document.getElementById('pendingPromoName');
            if (promoNameEl) {
                promoNameEl.textContent = orderData.promotionName ? ' (' + orderData.promotionName + ')' : '';
            }

            if (orderData.discountAmount > 0) {
                discountAmount.textContent = '-' + formatMoneyVnd(orderData.discountAmount);
                discountRow.classList.remove('d-none');
            } else {
                discountRow.classList.add('d-none');
            }

            const pointsRow = document.getElementById('pendingPointsRow');
            const pointsAmount = document.getElementById('pendingPointsAmount');
            if (orderData.pointsUsedAmount > 0) {
                pointsAmount.textContent = '-' + formatMoneyVnd(orderData.pointsUsedAmount);
                pointsRow.classList.remove('d-none');
            } else {
                pointsRow.classList.add('d-none');
            }

            document.getElementById('pendingTotalAmount').textContent = formatMoneyVnd(orderData.totalAmount);

            // 4. Cập nhật chi tiết giảm giá từng dòng trong giỏ hàng (V7.1 ProductDiscount)
            if (orderData.items && orderData.items.length > 0) {
                updateCartBreakdownFromPending(orderData.items);
            }

            // 5. Khởi tạo tính tiền thối lại (P2)
            const cashInput = document.getElementById('cashGivenInput');
            if (cashInput) cashInput.value = '';
            calculateCashChange();

            // 6. Khởi động Timer đếm ngược (RemainingSeconds từ server, đồng hồ JS dùng performance.now)
            PosApp.TimerModule.start(orderData.remainingSeconds, orderData.expiresAt, orderData.serverTime);

            // 7. Cảnh báo beforeunload khi có đơn Pending
            window.onbeforeunload = function () {
                return 'Đơn hàng đang chờ thanh toán. Bạn có chắc chắn muốn rời khỏi trang?';
            };

            // Mặc định chọn Tiền mặt
            document.getElementById('payMethodCash').checked = true;
            switchPaymentBranch('Cash');

            if (typeof showToast === 'function') {
                showToast(`Đã tạo đơn ${orderData.orderCode} (Pending). Vui lòng thanh toán!`, 'success');
            }
        }

        function switchPaymentBranch(method) {
            if (!currentOrder) return;
            currentOrder.paymentMethod = method;

            const cashBranch = document.getElementById('cashPaymentBranch');
            const qrBranch = document.getElementById('qrPaymentBranch');

            if (method === 'Cash') {
                if (cashBranch) cashBranch.classList.remove('d-none');
                if (qrBranch) qrBranch.classList.add('d-none');
                PosApp.PollModule.stopPolling();
            } else if (method === 'QR') {
                if (cashBranch) cashBranch.classList.add('d-none');
                if (qrBranch) qrBranch.classList.remove('d-none');

                const qrBox = document.getElementById('qrContentBox');
                const qrErrorBox = document.getElementById('qrErrorBox');
                const qrImg = document.getElementById('pendingQrImage');
                const qrDes = document.getElementById('pendingQrDes');

                // CMS-POS-01: Chỉ nhận sepayQrCodeUrl không rỗng và có giao thức https.
                // Ngoại lệ ảnh mock chỉ được phép khi server truyền cờ (IsDevelopment && Cms:UseMockApi)
                // và đường dẫn phải so khớp CHÍNH XÁC '/images/mock-qr.svg' (không nhận theo tiền tố).
                const rawQrUrl = (currentOrder && currentOrder.sepayQrCodeUrl) ? currentOrder.sepayQrCodeUrl.trim() : '';
                const allowMockQr = (window.POS_CONFIG && window.POS_CONFIG.allowMockQr === true) ||
                                    (document.getElementById('posWrapper')?.dataset?.allowMockQr === 'true');
                const isInternalMockQr = allowMockQr && (rawQrUrl === '/images/mock-qr.svg');
                const isValidHttps = rawQrUrl.length > 0 && (rawQrUrl.toLowerCase().startsWith('https://') || isInternalMockQr);

                if (isValidHttps) {
                    if (qrBox) qrBox.classList.remove('d-none');
                    if (qrErrorBox) qrErrorBox.classList.add('d-none');
                    if (qrImg) qrImg.src = rawQrUrl;
                    if (qrDes) qrDes.textContent = currentOrder.orderCode;

                    // Bắt đầu Polling 3s/lần
                    PosApp.PollModule.startPolling(currentOrder.orderId);
                } else {
                    // Không có URL hợp lệ: chặn hiển thị QR, dừng polling và hiển thị thông báo lỗi
                    if (qrBox) qrBox.classList.add('d-none');
                    if (qrErrorBox) qrErrorBox.classList.remove('d-none');
                    PosApp.PollModule.stopPolling();

                    if (typeof showToast === 'function') {
                        showToast('Không thể hiển thị mã QR: Máy chủ chưa cung cấp liên kết thanh toán hợp lệ.', 'danger');
                    }
                }
            }
        }

        function calculateCashChange() {
            if (!currentOrder) return;
            const cashInput = document.getElementById('cashGivenInput');
            const changeSpan = document.getElementById('cashChangeAmount');
            if (!cashInput || !changeSpan) return;

            const given = parseFloat(cashInput.value) || 0;
            const diff = given - currentOrder.totalAmount;

            if (diff >= 0) {
                changeSpan.textContent = formatMoneyVnd(diff);
                changeSpan.className = 'text-success fs-6';
            } else {
                changeSpan.textContent = `Thiếu ${formatMoneyVnd(Math.abs(diff))}`;
                changeSpan.className = 'text-danger fs-6';
            }
        }

        async function confirmCashPayment() {
            if (!currentOrder || isSubmitting) return;

            isSubmitting = true;
            const btn = document.getElementById('btnConfirmCashPayment');
            const btnCancel = document.getElementById('btnOpenCancelModal');
            const cashInput = document.getElementById('cashGivenInput');
            
            // Chuyển sang trạng thái CONFIRMING: Khóa thao tác tránh click trùng
            if (btn) {
                btn.disabled = true;
                btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>ĐANG XÁC NHẬN...';
            }
            if (btnCancel) btnCancel.disabled = true;
            if (cashInput) cashInput.disabled = true;

            if (typeof showLoading === 'function') showLoading('Đang xác nhận thu tiền mặt...');

            try {
                // docs 04 §3.3: POST không body
                const response = await fetch(`/Pos/ConfirmPayment?orderId=${currentOrder.orderId}`, {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': getAntiForgeryToken()
                    }
                });

                const result = await response.json();

                // 1. Trạng thái ENDPOINT_UNAVAILABLE (Backend chưa có route / không wrapper)
                if (result.isEndpointUnavailable) {
                    showEndpointWarning(result.message);
                    return;
                }

                // 2. Thành công -> Xuất hóa đơn
                if (result.success && result.data) {
                    onPaymentSuccess(result.data);
                    return;
                }

                // 3. Xử lý lỗi 409 Conflict: Đơn đã Paid qua SePay hoặc đã bị worker 4 phút Cancel
                if (result.statusCode === 409) {
                    console.warn('[POS Confirm] Gặp mã lỗi 409 Conflict. Kiểm tra lại trạng thái thực tế từ Backend...');
                    try {
                        const pollResp = await fetch(`/Pos/PollStatus?orderId=${currentOrder.orderId}`, {
                            headers: { 'Accept': 'application/json' }
                        });
                        const pollResult = await pollResp.json();
                        if (pollResult.success && pollResult.data) {
                            const st = (pollResult.data.status || pollResult.data.statusString || '').toString().toLowerCase();
                            if (st === 'paid' || pollResult.data.isPaid) {
                                if (typeof showToast === 'function') {
                                    showToast('Đơn hàng đã được thanh toán thành công!', 'success');
                                }
                                fetchReceiptAndShow(currentOrder.orderId, 'Cash');
                                return;
                            } else if (st === 'cancelled' || pollResult.data.isCancelled) {
                                PosApp.TimerModule.stop();
                                PosApp.PollModule.stopPolling();
                                window.onbeforeunload = null;
                                reopenCartAfterCancellation('Đơn hàng đã bị hủy tự động do hết hạn (Worker 4 phút). Kho sách đã được nhả về giỏ hàng.');
                                return;
                            }
                        }
                    } catch (pollErr) {
                        console.error('Lỗi khi thăm dò trạng thái sau 409:', pollErr);
                        // Lỗi mạng không bao giờ báo "đã thanh toán"
                        if (typeof showToast === 'function') {
                            showToast('Lỗi kết nối khi kiểm tra trạng thái đơn hàng. Vui lòng bấm thử lại.', 'warning');
                        }
                        return;
                    }
                }

                // Các lỗi nghiệp vụ khác
                if (typeof showToast === 'function') {
                    showToast(result.message || 'Xác nhận thanh toán thất bại.', 'danger');
                }
            } catch (err) {
                console.error('Lỗi mạng khi xác nhận thanh toán:', err);
                // Giữ nguyên trạng thái Pending để thu ngân có thể bấm thử lại — Lỗi mạng không bao giờ báo "đã thanh toán"
                if (typeof showToast === 'function') {
                    showToast('Lỗi kết nối khi xác nhận thanh toán. Đơn hàng được giữ nguyên để bạn thử lại.', 'warning');
                }
            } finally {
                isSubmitting = false;
                if (btn) {
                    btn.disabled = false;
                    btn.innerHTML = '<i class="fas fa-check-circle me-2"></i>XÁC NHẬN ĐÃ THU TIỀN MẶT';
                }
                if (btnCancel) btnCancel.disabled = false;
                if (cashInput) cashInput.disabled = false;
                if (typeof hideLoading === 'function') hideLoading();
            }
        }

        function onOrderPaidByWebhook(orderId) {
            // Khi webhook SePay chốt đơn tự động
            if (!currentOrder || currentOrder.orderId !== orderId) return;

            // Vô hiệu hóa nút nhận tiền mặt ngay lập tức
            const btn = document.getElementById('btnConfirmCashPayment');
            if (btn) btn.disabled = true;

            if (typeof showToast === 'function') {
                showToast(`Khách đã chuyển khoản thành công cho đơn ${currentOrder.orderCode}!`, 'success');
            }

            // CMS-POS-02: Không gọi POST /Pos/ConfirmPayment (write) cho đơn QR đã Paid.
            // Thử đọc receipt từ endpoint đọc; nếu chưa có thì hiển thị trạng thái chưa tải được hóa đơn
            fetchReceiptAndShow(orderId, 'QR');
        }

        async function fetchReceiptAndShow(orderId, method) {
            if (typeof showLoading === 'function') showLoading('Đang tải hóa đơn bán lẻ...');
            try {
                // Thử đọc hóa đơn chính thức qua GET /Orders/Receipt/{id} (read operation)
                const response = await fetch(`/Orders/Receipt/${orderId}`, {
                    headers: { 'Accept': 'application/json' }
                });

                const result = await response.json();
                if (result.success && result.data) {
                    onPaymentSuccess(result.data, true);
                    return;
                }
            } catch (err) {
                console.warn('Endpoint đọc hóa đơn chưa có hoặc lỗi mạng (Issue #34):', err);
            } finally {
                if (typeof hideLoading === 'function') hideLoading();
            }

            // CMS-POS-02: Tuyệt đối không tự dựng hóa đơn giả ở client (không tự tạo completedAt, không tự tính pointsEarned)
            // Hiển thị trạng thái "Đã thanh toán, chưa tải được hóa đơn chính thức" và khóa nút in
            showPaidWithoutReceipt(currentOrder, method);
        }

        async function fetchOfficialReceipt(orderId) {
            if (!orderId) return;
            if (typeof showLoading === 'function') showLoading('Đang tải lại hóa đơn chính thức từ máy chủ...');
            try {
                const response = await fetch(`/Orders/Receipt/${orderId}`, {
                    headers: { 'Accept': 'application/json' }
                });
                const result = await response.json();
                if (result.success && result.data) {
                    onPaymentSuccess(result.data, true);
                    if (typeof showToast === 'function') {
                        showToast('Đã tải hóa đơn chính thức thành công!', 'success');
                    }
                } else {
                    if (typeof showToast === 'function') {
                        showToast(result.message || 'Chưa thể tải hóa đơn chính thức từ máy chủ (Chờ Issue #34).', 'warning');
                    }
                }
            } catch (err) {
                console.error('Lỗi khi tải lại hóa đơn:', err);
                if (typeof showToast === 'function') {
                    showToast('Lỗi kết nối khi tải lại hóa đơn.', 'danger');
                }
            } finally {
                if (typeof hideLoading === 'function') hideLoading();
            }
        }

        function showPaidWithoutReceipt(order, method) {
            if (!order) return;
            PosApp.TimerModule.stop();
            PosApp.PollModule.stopPolling();
            window.onbeforeunload = null;

            // CMS-POS-PRINT-01: Tuyệt đối KHÔNG đổ dữ liệu giỏ hàng, dòng sách, số tiền vào #receiptPrintArea khi chưa có hóa đơn chính thức
            onPaymentSuccess(null, false);
        }

        function clearReceiptDom() {
            const receiptArea = document.getElementById('receiptPrintArea');
            if (receiptArea) receiptArea.setAttribute('data-receipt-official', 'false');

            const unofficialNotice = document.getElementById('receiptUnofficialNotice');
            if (unofficialNotice) unofficialNotice.classList.remove('d-none');

            const orderCodeEl = document.getElementById('receiptOrderCode');
            if (orderCodeEl) orderCodeEl.textContent = '--';

            const createdAtEl = document.getElementById('receiptCreatedAt');
            if (createdAtEl) createdAtEl.textContent = '--/--/---- --:--';

            const completedAtEl = document.getElementById('receiptCompletedAt');
            if (completedAtEl) completedAtEl.textContent = '--/--/---- --:--';

            const cashierEl = document.getElementById('receiptCashierName');
            if (cashierEl) cashierEl.textContent = '--';

            const memberEl = document.getElementById('receiptMemberName');
            if (memberEl) memberEl.textContent = '--';

            const pointsRow = document.getElementById('receiptPointsEarnedRow');
            if (pointsRow) pointsRow.classList.add('d-none');

            const tbody = document.getElementById('receiptItemsBody');
            if (tbody) tbody.innerHTML = '';

            const subTotalEl = document.getElementById('receiptSubTotal');
            if (subTotalEl) subTotalEl.textContent = '--';

            const discountRow = document.getElementById('receiptDiscountRow');
            if (discountRow) discountRow.classList.add('d-none');

            const pointsUsedRow = document.getElementById('receiptPointsUsedRow');
            if (pointsUsedRow) pointsUsedRow.classList.add('d-none');

            const totalEl = document.getElementById('receiptTotalAmount');
            if (totalEl) totalEl.textContent = '--';

            const paymentMethodEl = document.getElementById('receiptPaymentMethod');
            if (paymentMethodEl) paymentMethodEl.textContent = '--';
        }

        function onPaymentSuccess(receiptData, isOfficial = true) {
            PosApp.TimerModule.stop();
            PosApp.PollModule.stopPolling();
            window.onbeforeunload = null;

            renderReceipt(receiptData, isOfficial);

            const receiptModalEl = document.getElementById('receiptModal');
            if (receiptModalEl) {
                const modal = new bootstrap.Modal(receiptModalEl);
                modal.show();
            }
        }

        function renderReceipt(receipt, isOfficial = true) {
            // CMS-POS-02 & CMS-POS-PRINT-01: Phân biệt hóa đơn chính thức vs chưa có hóa đơn chính thức
            const receiptArea = document.getElementById('receiptPrintArea');
            const alertPending = document.getElementById('receiptPendingAlert');
            const btnRetryReceipt = document.getElementById('btnRetryFetchReceipt');
            const btnPrint = document.getElementById('btnPrintReceipt');
            const unofficialNotice = document.getElementById('receiptUnofficialNotice');

            if (isOfficial && receipt) {
                if (receiptArea) receiptArea.setAttribute('data-receipt-official', 'true');
                if (alertPending) alertPending.classList.add('d-none');
                if (btnRetryReceipt) btnRetryReceipt.classList.add('d-none');
                if (unofficialNotice) unofficialNotice.classList.add('d-none');
                if (btnPrint) {
                    btnPrint.disabled = false;
                    btnPrint.title = '';
                }

                // Thông tin cửa hàng (lấy từ Backend / appsettings)
                const storeNameEl = document.getElementById('receiptStoreName');
                const storeAddressEl = document.getElementById('receiptStoreAddress');
                const storeContactEl = document.getElementById('receiptStoreContact');
                if (storeNameEl && receipt.storeName) storeNameEl.textContent = receipt.storeName;
                if (storeAddressEl && receipt.storeAddress) storeAddressEl.textContent = receipt.storeAddress;
                if (storeContactEl) {
                    const contactParts = [];
                    if (receipt.storeHotline) contactParts.push(`Hotline: ${receipt.storeHotline}`);
                    if (receipt.storeWebsite) contactParts.push(`Web: ${receipt.storeWebsite}`);
                    if (contactParts.length > 0) storeContactEl.textContent = contactParts.join(' - ');
                }

                document.getElementById('receiptOrderCode').textContent = receipt.orderCode || '--';
                document.getElementById('receiptCreatedAt').textContent = formatDateTime(receipt.createdAt);
                document.getElementById('receiptCompletedAt').textContent = receipt.completedAt ? formatDateTime(receipt.completedAt) : '--/--/---- --:--';
                document.getElementById('receiptCashierName').textContent = receipt.cashierName || 'Thu ngân';
                document.getElementById('receiptMemberName').textContent = receipt.memberDisplayName || 'Khách vãng lai';

                const pointsRow = document.getElementById('receiptPointsEarnedRow');
                const pointsSpan = document.getElementById('receiptPointsEarned');
                if (receipt.pointsEarned && receipt.pointsEarned > 0) {
                    pointsSpan.textContent = `+${receipt.pointsEarned} điểm`;
                    pointsRow.classList.remove('d-none');
                } else {
                    pointsRow.classList.add('d-none');
                }

                // Render dòng sách (DOM an toàn, tránh innerHTML với text ngoài)
                const tbody = document.getElementById('receiptItemsBody');
                tbody.innerHTML = '';

                const items = receipt.items || [];
                items.forEach(item => {
                    const tr = document.createElement('tr');

                    const tdTitle = document.createElement('td');
                    tdTitle.textContent = item.title || `Sách #${item.bookId}`;

                    if (item.lineDiscountAmount && item.lineDiscountAmount > 0) {
                        const discountBadge = document.createElement('div');
                        discountBadge.className = 'small text-success';
                        discountBadge.textContent = `(-${formatMoneyVnd(item.discountPerUnit || 0)}/cuốn)`;
                        tdTitle.appendChild(discountBadge);
                    }

                    const tdQty = document.createElement('td');
                    tdQty.className = 'text-center';
                    tdQty.textContent = (item.quantity || 1).toString();

                    const tdPrice = document.createElement('td');
                    tdPrice.className = 'text-end';
                    tdPrice.textContent = formatMoneyVnd(item.unitPrice || 0);

                    const tdTotal = document.createElement('td');
                    if (item.lineDiscountAmount && item.lineDiscountAmount > 0) {
                        tdTotal.className = 'text-end';
                        const delEl = document.createElement('del');
                        delEl.className = 'text-muted small d-block';
                        delEl.textContent = formatMoneyVnd(item.lineTotal || (item.unitPrice * item.quantity));
                        const netEl = document.createElement('span');
                        netEl.className = 'fw-bold text-success';
                        netEl.textContent = formatMoneyVnd(item.netLineTotal);
                        tdTotal.appendChild(delEl);
                        tdTotal.appendChild(netEl);
                    } else {
                        tdTotal.className = 'text-end fw-bold';
                        tdTotal.textContent = formatMoneyVnd(item.lineTotal || (item.unitPrice * item.quantity));
                    }

                    tr.appendChild(tdTitle);
                    tr.appendChild(tdQty);
                    tr.appendChild(tdPrice);
                    tr.appendChild(tdTotal);
                    tbody.appendChild(tr);
                });

                const unassignedNotice = document.getElementById('receiptUnassignedNotice');
                if (unassignedNotice) {
                    const isProductDiscount = receipt.promotionType === 2 || receipt.promotionType === 'ProductDiscount';
                    const hasAnyLineDiscount = (receipt.items || []).some(i => i.lineDiscountAmount && i.lineDiscountAmount > 0);
                    if (isProductDiscount && !hasAnyLineDiscount && receipt.discountAmount > 0) {
                        unassignedNotice.classList.remove('d-none');
                    } else {
                        unassignedNotice.classList.add('d-none');
                    }
                }

                // Totals
                document.getElementById('receiptSubTotal').textContent = formatMoneyVnd(receipt.subTotal);

                const discountRow = document.getElementById('receiptDiscountRow');
                const discountSpan = document.getElementById('receiptDiscountAmount');
                const discountLabel = document.getElementById('receiptDiscountLabel');
                if (receipt.discountAmount > 0) {
                    discountSpan.textContent = '-' + formatMoneyVnd(receipt.discountAmount);
                    let labelText = 'Giảm khuyến mãi:';
                    if (receipt.promotionName) {
                        let promoTypeStr = '';
                        if (receipt.promotionType === 1 || receipt.promotionType === 'OrderDiscount') {
                            promoTypeStr = ' (Hóa đơn)';
                        } else if (receipt.promotionType === 2 || receipt.promotionType === 'ProductDiscount') {
                            promoTypeStr = ' (Từng sách)';
                        }
                        labelText = `KM - ${receipt.promotionName}${promoTypeStr}:`;
                    }
                    if (discountLabel) discountLabel.textContent = labelText;
                    discountRow.classList.remove('d-none');
                } else {
                    discountRow.classList.add('d-none');
                }

                const pointsUsedRow = document.getElementById('receiptPointsUsedRow');
                const pointsUsedSpan = document.getElementById('receiptPointsUsedAmount');
                if (receipt.pointsUsedAmount > 0) {
                    pointsUsedSpan.textContent = '-' + formatMoneyVnd(receipt.pointsUsedAmount);
                    pointsUsedRow.classList.remove('d-none');
                } else {
                    pointsUsedRow.classList.add('d-none');
                }

                document.getElementById('receiptTotalAmount').textContent = formatMoneyVnd(receipt.totalAmount);
                document.getElementById('receiptPaymentMethod').textContent = receipt.paymentMethod === 'QR' ? 'Chuyển khoản QR' : 'Tiền mặt';
            } else {
                if (receiptArea) receiptArea.setAttribute('data-receipt-official', 'false');
                if (alertPending) alertPending.classList.remove('d-none');
                if (btnRetryReceipt) btnRetryReceipt.classList.remove('d-none');
                if (unofficialNotice) unofficialNotice.classList.remove('d-none');
                if (btnPrint) {
                    btnPrint.disabled = true;
                    btnPrint.title = 'Chưa thể in: Dữ liệu hóa đơn chính thức chưa được tải từ máy chủ.';
                }

                // CMS-POS-PRINT-01: Tuyệt đối không đổ dữ liệu đơn vào #receiptPrintArea
                clearReceiptDom();
            }
        }

        async function executeCancelOrder() {
            if (!currentOrder || isSubmitting) return;

            isSubmitting = true;
            const btn = document.getElementById('btnConfirmCancelOrder');
            if (btn) btn.disabled = true;

            if (typeof showLoading === 'function') showLoading('Đang hủy đơn hàng và nhả kho...');

            try {
                // docs 04 §3.3: POST không body
                const response = await fetch(`/Pos/CancelOrder?orderId=${currentOrder.orderId}`, {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': getAntiForgeryToken()
                    }
                });

                const result = await response.json();

                if (result.isEndpointUnavailable) {
                    showEndpointWarning(result.message);
                    return;
                }

                if (result.success) {
                    // Đóng modal hủy
                    const modalEl = document.getElementById('cancelOrderModal');
                    const modal = bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();

                    PosApp.TimerModule.stop();
                    PosApp.PollModule.stopPolling();
                    window.onbeforeunload = null;

                    // Mở lại giỏ hàng cho thu ngân sửa tiếp
                    reopenCartAfterCancellation('Đơn hàng đã được hủy thành công, kho sách đã được nhả.');
                } else {
                    if (typeof showToast === 'function') {
                        showToast(result.message || 'Hủy đơn hàng thất bại.', 'danger');
                    }
                }
            } catch (err) {
                console.error('Lỗi khi hủy đơn hàng:', err);
                if (typeof showToast === 'function') {
                    showToast('Lỗi kết nối khi hủy đơn hàng.', 'danger');
                }
            } finally {
                isSubmitting = false;
                if (btn) btn.disabled = false;
                if (typeof hideLoading === 'function') hideLoading();
            }
        }

        async function handleOrderTimeoutExpired() {
            if (!currentOrder) return;

            // 1. Khóa các thao tác thanh toán
            const btnConfirmCash = document.getElementById('btnConfirmCashPayment');
            if (btnConfirmCash) btnConfirmCash.disabled = true;

            if (typeof showToast === 'function') {
                showToast('Hết thời gian thanh toán (4:00)! Đang kiểm tra trạng thái máy chủ...', 'warning');
            }

            try {
                // 2. Thăm dò trạng thái server 1 lần cuối
                const checkResp = await fetch(`/Pos/PollStatus?orderId=${currentOrder.orderId}`, {
                    headers: { 'Accept': 'application/json' }
                });
                const checkResult = await checkResp.json();

                if (checkResult.success && checkResult.data) {
                    const st = (checkResult.data.status || checkResult.data.statusString || '').toLowerCase();
                    if (st === 'paid' || checkResult.data.isPaid) {
                        // Khách đã trả tiền ngay giây cuối -> sang hóa đơn
                        onOrderPaidByWebhook(currentOrder.orderId);
                        return;
                    }
                }

                // 3. Nếu vẫn Pending -> Gọi cancel (docs 04 §3.3: POST không body)
                const cancelResp = await fetch(`/Pos/CancelOrder?orderId=${currentOrder.orderId}`, {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': getAntiForgeryToken()
                    }
                });

                const cancelResult = await cancelResp.json();

                if (cancelResult.success) {
                    PosApp.TimerModule.stop();
                    PosApp.PollModule.stopPolling();
                    window.onbeforeunload = null;

                    reopenCartAfterCancellation('Đơn hàng đã hết hạn thanh toán (4 phút), kho sách đã được mở khóa tự động.');
                }
            } catch (err) {
                console.error('Lỗi trong quy trình timeout:', err);
            }
        }

        function onOrderCancelledRemotely() {
            PosApp.TimerModule.stop();
            PosApp.PollModule.stopPolling();
            window.onbeforeunload = null;
            reopenCartAfterCancellation('Đơn hàng đã bị hủy bởi hệ thống hoặc phiên làm việc khác. Kho sách đã được nhả.');
        }

        function reopenCartAfterCancellation(msg) {
            currentOrder = null;

            // Mở khóa giỏ hàng
            const cartCard = document.getElementById('posCartCard');
            const scanCard = document.getElementById('posScanSearchCard');
            if (cartCard) cartCard.classList.remove('pos-cart-readonly');
            if (scanCard) scanCard.classList.remove('pos-cart-readonly');

            // Quay về Draft panel
            const draftPanel = document.getElementById('posDraftPanel');
            const pendingPanel = document.getElementById('posPendingPanel');
            if (draftPanel) draftPanel.classList.remove('d-none');
            if (pendingPanel) pendingPanel.classList.add('d-none');

            if (typeof showToast === 'function') {
                showToast(msg, 'warning');
            }
            PosApp.ScanModule.refocusScanner();
        }

        function resetToDraft() {
            currentOrder = null;
            PosApp.CartModule.clearCart();
            PosApp.MemberModule.init();

            const cartCard = document.getElementById('posCartCard');
            const scanCard = document.getElementById('posScanSearchCard');
            if (cartCard) cartCard.classList.remove('pos-cart-readonly');
            if (scanCard) scanCard.classList.remove('pos-cart-readonly');

            const draftPanel = document.getElementById('posDraftPanel');
            const pendingPanel = document.getElementById('posPendingPanel');
            if (draftPanel) draftPanel.classList.remove('d-none');
            if (pendingPanel) pendingPanel.classList.add('d-none');

            PosApp.ScanModule.refocusScanner();
        }

        function showEndpointWarning(message) {
            const box = document.getElementById('endpointUnavailableBox');
            const msgEl = document.getElementById('endpointUnavailableMessage');
            if (box) {
                if (msgEl && message) msgEl.textContent = message;
                box.classList.remove('d-none');
                box.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }

        function hideEndpointWarning() {
            const box = document.getElementById('endpointUnavailableBox');
            if (box) box.classList.add('d-none');
        }

        return {
            init: init,
            createCounterOrder: createCounterOrder,
            handleOrderTimeoutExpired: handleOrderTimeoutExpired,
            onOrderPaidByWebhook: onOrderPaidByWebhook,
            onOrderCancelledRemotely: onOrderCancelledRemotely
        };
    })();

    // ====================================================================
    // 8. CLOCK & KHỞI TẠO HỆ THỐNG
    // ====================================================================
    PosApp.ClockModule = (function () {
        function init() {
            updateClock();
            setInterval(updateClock, 1000);
        }

        function updateClock() {
            const clockEl = document.getElementById('posLiveClock');
            if (!clockEl) return;
            const now = new Date();
            clockEl.textContent = now.toLocaleTimeString('vi-VN');
        }

        return { init: init };
    })();

    // Tự động khởi chạy khi DOM tải xong
    document.addEventListener('DOMContentLoaded', function () {
        PosApp.ScanModule.init();
        PosApp.CartModule.init();
        PosApp.MemberModule.init();
        PosApp.PromoModule.init();
        PosApp.CheckoutModule.init();
        PosApp.ClockModule.init();
    });

    window.PosApp = PosApp;

})(window, document);
