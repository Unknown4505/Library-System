/**
 * PHÂN HỆ H: QUẢN LÝ HÓA ĐƠN & ĐỐI SOÁT (ORDERS CLIENT JS)
 */
(function (window, document) {
    'use strict';

    function getAntiForgeryToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function formatMoneyVnd(amount) {
        if (typeof amount !== 'number') amount = Number(amount) || 0;
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
    }

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

    // 1. Cập nhật chuông thông báo (chỉ đếm "Cần hỗ trợ" trong Tuần 6)
    function updateNeedsSupportBell() {
        const badge = document.getElementById('notificationBadge');
        if (!badge) return;

        fetch('/Orders/GetNeedsSupportCount', {
            headers: { 'Accept': 'application/json' }
        })
        .then(res => res.json())
        .then(data => {
            const count = data && typeof data.count === 'number' ? data.count : 0;
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count;
                badge.classList.remove('d-none');
            } else {
                badge.classList.add('d-none');
            }
        })
        .catch(err => console.warn('[Orders] Lỗi kiểm tra số đơn Cần hỗ trợ:', err));
    }

    // 2. Mở Modal Chi tiết & Đối soát đơn hàng
    async function openOrderDetailModal(orderId) {
        const modalContainer = document.getElementById('orderDetailModalContent');
        const modalEl = document.getElementById('orderDetailModal');
        if (!modalContainer || !modalEl) return;

        modalContainer.innerHTML = '<div class="text-center py-5"><div class="spinner-border text-primary" role="status"></div><div class="mt-2 text-muted">Đang tải dữ liệu đơn hàng...</div></div>';
        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();

        try {
            const response = await fetch(`/Orders/Details/${orderId}`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!response.ok) {
                modalContainer.innerHTML = '<div class="alert alert-danger m-3">Không thể tải thông tin đơn hàng hoặc endpoint chưa sẵn sàng.</div>';
                return;
            }

            const html = await response.text();
            modalContainer.innerHTML = html;

            // Khởi tạo các event bên trong modal chi tiết nếu có
            bindDetailModalEvents(modalContainer);
        } catch (err) {
            console.error('Lỗi khi tải chi tiết đơn hàng:', err);
            modalContainer.innerHTML = '<div class="alert alert-danger m-3">Lỗi kết nối khi tải chi tiết đơn hàng.</div>';
        }
    }

    function bindDetailModalEvents(container) {
        // Nút In lại hóa đơn từ trong Modal Chi tiết
        const btnPrint = container.querySelector('.btn-print-from-detail');
        if (btnPrint) {
            btnPrint.addEventListener('click', function () {
                const orderId = btnPrint.getAttribute('data-order-id');
                if (orderId) reprintOrderReceipt(orderId);
            });
        }
    }

    // 3. In lại hóa đơn K80 (Chỉ dành cho đơn Paid)
    async function reprintOrderReceipt(orderId) {
        try {
            const response = await fetch(`/Orders/Receipt/${orderId}`, {
                headers: { 'Accept': 'application/json' }
            });
            const result = await response.json();

            if (result.isEndpointUnavailable) {
                alert(result.message || 'Chức năng in lại hóa đơn chưa sẵn sàng trên Backend.');
                return;
            }

            if (!result.success || !result.data) {
                alert(result.message || 'Không thể in lại hóa đơn cho đơn hàng này.');
                return;
            }

            // Bind dữ liệu vào Receipt Modal
            renderReprintReceipt(result.data);

            const receiptModalEl = document.getElementById('receiptModal');
            if (receiptModalEl) {
                const receiptModal = bootstrap.Modal.getOrCreateInstance(receiptModalEl);
                receiptModal.show();
            }
        } catch (err) {
            console.error('Lỗi khi in lại hóa đơn:', err);
            alert('Lỗi kết nối khi tải dữ liệu hóa đơn.');
        }
    }

    function renderReprintReceipt(receipt) {
        const receiptArea = document.getElementById('receiptPrintArea');
        if (receiptArea) receiptArea.setAttribute('data-receipt-official', 'true');

        const alertPending = document.getElementById('receiptPendingAlert');
        if (alertPending) alertPending.classList.add('d-none');

        const unofficialNotice = document.getElementById('receiptUnofficialNotice');
        if (unofficialNotice) unofficialNotice.classList.add('d-none');

        const btnPrint = document.getElementById('btnPrintReceipt');
        if (btnPrint) {
            btnPrint.disabled = false;
            btnPrint.title = '';
        }

        const storeName = document.getElementById('receiptStoreName');
        const storeAddress = document.getElementById('receiptStoreAddress');
        const storeContact = document.getElementById('receiptStoreContact');
        if (storeName && receipt.storeName) storeName.textContent = receipt.storeName;
        if (storeAddress && receipt.storeAddress) storeAddress.textContent = receipt.storeAddress;
        if (storeContact) {
            const parts = [];
            if (receipt.storeHotline) parts.push(`Hotline: ${receipt.storeHotline}`);
            if (receipt.storeWebsite) parts.push(`Web: ${receipt.storeWebsite}`);
            if (parts.length > 0) storeContact.textContent = parts.join(' - ');
        }

        const codeEl = document.getElementById('receiptOrderCode');
        const createdEl = document.getElementById('receiptCreatedAt');
        const completedEl = document.getElementById('receiptCompletedAt');
        const cashierEl = document.getElementById('receiptCashierName');
        const memberEl = document.getElementById('receiptMemberName');

        if (codeEl) codeEl.textContent = receipt.orderCode || '--';
        if (createdEl) createdEl.textContent = formatDateTime(receipt.createdAt);
        if (completedEl) completedEl.textContent = formatDateTime(receipt.completedAt);
        if (cashierEl) cashierEl.textContent = receipt.cashierName || 'Thu ngân';
        if (memberEl) memberEl.textContent = receipt.memberDisplayName || 'Thành viên';

        const pointsRow = document.getElementById('receiptPointsEarnedRow');
        const pointsSpan = document.getElementById('receiptPointsEarned');
        if (pointsRow && pointsSpan) {
            if (receipt.pointsEarned && receipt.pointsEarned > 0) {
                pointsSpan.textContent = `+${receipt.pointsEarned} điểm`;
                pointsRow.classList.remove('d-none');
            } else {
                pointsRow.classList.add('d-none');
            }
        }

        const tbody = document.getElementById('receiptItemsBody');
        if (tbody) {
            tbody.textContent = '';
            (receipt.items || []).forEach(item => {
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
        }

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

        const subTotalEl = document.getElementById('receiptSubTotal');
        if (subTotalEl) subTotalEl.textContent = formatMoneyVnd(receipt.subTotal);

        const discountRow = document.getElementById('receiptDiscountRow');
        const discountSpan = document.getElementById('receiptDiscountAmount');
        const discountLabel = document.getElementById('receiptDiscountLabel');
        if (discountRow && discountSpan) {
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
        }

        const pointsUsedRow = document.getElementById('receiptPointsUsedRow');
        const pointsUsedSpan = document.getElementById('receiptPointsUsedAmount');
        if (pointsUsedRow && pointsUsedSpan) {
            if (receipt.pointsUsedAmount > 0) {
                pointsUsedSpan.textContent = '-' + formatMoneyVnd(receipt.pointsUsedAmount);
                pointsUsedRow.classList.remove('d-none');
            } else {
                pointsUsedRow.classList.add('d-none');
            }
        }

        const totalEl = document.getElementById('receiptTotalAmount');
        if (totalEl) totalEl.textContent = formatMoneyVnd(receipt.totalAmount);

        const paymentMethodEl = document.getElementById('receiptPaymentMethod');
        if (paymentMethodEl) {
            paymentMethodEl.textContent = receipt.paymentMethod === 'QR' ? 'Chuyển khoản QR' : 'Tiền mặt';
        }
    }

    // 4. Hủy đơn hàng Quầy (Counter Only)
    let pendingCancelOrderId = null;

    function openCancelCounterModal(orderId, orderCode) {
        pendingCancelOrderId = orderId;
        const codeSpan = document.getElementById('cancelOrderModalCode');
        if (codeSpan) codeSpan.textContent = orderCode;

        const modalEl = document.getElementById('cancelCounterOrderModal');
        if (modalEl) {
            const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            modal.show();
        }
    }

    async function submitCancelCounterOrder() {
        if (!pendingCancelOrderId) return;

        const btn = document.getElementById('btnConfirmCancelCounterOrder');
        if (btn) btn.disabled = true;

        try {
            const response = await fetch(`/Orders/Cancel/${pendingCancelOrderId}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': getAntiForgeryToken()
                }
            });

            const result = await response.json();

            const modalEl = document.getElementById('cancelCounterOrderModal');
            if (modalEl) {
                const modal = bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();
            }

            if (result.success) {
                alert(result.message || 'Đã hủy đơn hàng thành công.');
                window.location.reload();
            } else {
                alert(result.message || 'Hủy đơn hàng thất bại.');
            }
        } catch (err) {
            console.error('Lỗi khi hủy đơn hàng:', err);
            alert('Lỗi kết nối khi hủy đơn hàng.');
        } finally {
            if (btn) btn.disabled = false;
            pendingCancelOrderId = null;
        }
    }

    // Khởi tạo các event lắng nghe
    document.addEventListener('DOMContentLoaded', function () {
        // Cập nhật chuông thông báo ngay khi tải trang
        updateNeedsSupportBell();
        // Kiểm tra định kỳ mỗi 30s
        setInterval(updateNeedsSupportBell, 30000);

        // Click xem chi tiết đơn hàng
        document.querySelectorAll('.btn-view-order-detail').forEach(btn => {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                const orderId = this.getAttribute('data-order-id');
                if (orderId) openOrderDetailModal(orderId);
            });
        });

        // Click in lại hóa đơn
        document.querySelectorAll('.btn-reprint-receipt').forEach(btn => {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                const orderId = this.getAttribute('data-order-id');
                if (orderId) reprintOrderReceipt(orderId);
            });
        });

        // Click mở modal hủy đơn quầy
        document.querySelectorAll('.btn-cancel-counter-order').forEach(btn => {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                const orderId = this.getAttribute('data-order-id');
                const orderCode = this.getAttribute('data-order-code');
                if (orderId) openCancelCounterModal(orderId, orderCode);
            });
        });

        // Nút xác nhận hủy trong modal
        const btnConfirmCancel = document.getElementById('btnConfirmCancelCounterOrder');
        if (btnConfirmCancel) {
            btnConfirmCancel.addEventListener('click', submitCancelCounterOrder);
        }

        // Nút In hóa đơn trong receipt modal (CMS-POS-PRINT-01)
        const btnPrintReceipt = document.getElementById('btnPrintReceipt');
        if (btnPrintReceipt) {
            btnPrintReceipt.addEventListener('click', function () {
                const receiptArea = document.getElementById('receiptPrintArea');
                const isOfficial = receiptArea && receiptArea.getAttribute('data-receipt-official') === 'true';
                if (!isOfficial) {
                    alert('Chưa có hóa đơn chính thức - không dùng làm chứng từ.');
                    return;
                }
                window.print();
            });
        }

        // Reset marker khi đóng modal
        const receiptModalEl = document.getElementById('receiptModal');
        if (receiptModalEl) {
            receiptModalEl.addEventListener('hidden.bs.modal', function () {
                const receiptArea = document.getElementById('receiptPrintArea');
                if (receiptArea) receiptArea.setAttribute('data-receipt-official', 'false');
            });
        }
    });

    window.OrdersApp = {
        openOrderDetailModal: openOrderDetailModal,
        reprintOrderReceipt: reprintOrderReceipt,
        updateNeedsSupportBell: updateNeedsSupportBell
    };

})(window, document);
