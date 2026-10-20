/**
 * charts.js - Quản lý biểu đồ doanh thu và widget Dashboard cho BookKiosk CMS
 * Tuần 7 - Phân hệ C (Dashboard & Giám sát)
 */

document.addEventListener("DOMContentLoaded", function () {
    const chartCanvas = document.getElementById("revenueBarChart");
    if (!chartCanvas || typeof Chart === "undefined") {
        return;
    }

    const yearSelect = document.getElementById("revenueYearSelect");
    const tableBody = document.getElementById("revenueTableBody");
    const tableTotalRevenue = document.getElementById("revenueTableTotalRevenue");
    const tableTotalOrders = document.getElementById("revenueTableTotalOrders");
    const tableYearLabel = document.getElementById("tableYearLabel");

    const kpiAnnualRevenue = document.getElementById("kpiAnnualRevenue");
    const kpiAnnualOrders = document.getElementById("kpiAnnualOrders");
    const kpiAnnualRevenueYearLabel = document.getElementById("kpiAnnualRevenueYearLabel");
    const kpiAnnualOrdersYearLabel = document.getElementById("kpiAnnualOrdersYearLabel");

    function formatVnd(val) {
        return new Intl.NumberFormat("vi-VN").format(val || 0) + " đ";
    }

    function formatNumber(val) {
        return new Intl.NumberFormat("vi-VN").format(val || 0);
    }

    // Đọc dữ liệu ban đầu từ data-revenue trên canvas (Server-rendered)
    let initialRawData = [];
    try {
        const rawAttr = chartCanvas.getAttribute("data-revenue");
        if (rawAttr) {
            initialRawData = JSON.parse(rawAttr);
        }
    } catch (e) {
        console.error("Lỗi parse data-revenue ban đầu:", e);
    }

    // Chuẩn hóa mảng 12 tháng
    const monthLabels = [
        "Tháng 1", "Tháng 2", "Tháng 3", "Tháng 4", "Tháng 5", "Tháng 6",
        "Tháng 7", "Tháng 8", "Tháng 9", "Tháng 10", "Tháng 11", "Tháng 12"
    ];

    let currentOrdersData = new Array(12).fill(0);
    const initialRevenueData = new Array(12).fill(0);

    if (Array.isArray(initialRawData)) {
        initialRawData.forEach(item => {
            const m = (item.month || item.Month || 0);
            if (m >= 1 && m <= 12) {
                initialRevenueData[m - 1] = Number(item.revenue ?? item.Revenue ?? 0);
                currentOrdersData[m - 1] = Number(item.totalOrders ?? item.TotalOrders ?? 0);
            }
        });
    }

    // Khởi tạo Chart.js Bar Chart
    const ctx = chartCanvas.getContext("2d");
    const revenueChart = new Chart(ctx, {
        type: "bar",
        data: {
            labels: monthLabels,
            datasets: [{
                label: "Doanh thu (VNĐ)",
                data: initialRevenueData,
                backgroundColor: "rgba(13, 110, 253, 0.75)",
                borderColor: "rgb(13, 110, 253)",
                borderWidth: 1,
                borderRadius: 4,
                hoverBackgroundColor: "rgba(13, 110, 253, 0.95)"
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    backgroundColor: "rgba(33, 37, 41, 0.9)",
                    titleFont: { size: 13, weight: "bold" },
                    bodyFont: { size: 12 },
                    padding: 10,
                    callbacks: {
                        label: function (context) {
                            const val = context.raw || 0;
                            return "Doanh thu: " + formatVnd(val);
                        },
                        afterLabel: function (context) {
                            const idx = context.dataIndex;
                            const orders = currentOrdersData[idx] || 0;
                            return "Số đơn: " + formatNumber(orders);
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: function (value) {
                            if (value >= 1_000_000_000) return (value / 1_000_000_000).toLocaleString("vi-VN") + " tỷ";
                            if (value >= 1_000_000) return (value / 1_000_000).toLocaleString("vi-VN") + " tr";
                            if (value >= 1_000) return (value / 1_000).toLocaleString("vi-VN") + " k";
                            return value.toLocaleString("vi-VN");
                        }
                    },
                    grid: {
                        color: "rgba(0, 0, 0, 0.05)"
                    }
                },
                x: {
                    grid: {
                        display: false
                    }
                }
            }
        }
    });

    // Xử lý thay đổi Năm qua AJAX
    if (yearSelect) {
        yearSelect.addEventListener("change", function () {
            const selectedYear = parseInt(this.value, 10);
            if (!selectedYear) return;

            yearSelect.disabled = true;

            fetch(`/Home/GetRevenueChartData?year=${selectedYear}`, {
                headers: { "Accept": "application/json" }
            })
            .then(res => {
                if (!res.ok) throw new Error("HTTP " + res.status);
                return res.json();
            })
            .then(res => {
                if (!res || !res.success) {
                    notifyError(res?.message || "Không thể tải dữ liệu doanh thu cho năm đã chọn.", "warning");
                    return;
                }

                // Cập nhật mảng 12 tháng
                const newRevenueData = new Array(12).fill(0);
                const newOrdersData = new Array(12).fill(0);

                if (Array.isArray(res.data)) {
                    res.data.forEach(item => {
                        const m = (item.month || item.Month || 0);
                        if (m >= 1 && m <= 12) {
                            newRevenueData[m - 1] = Number(item.revenue ?? item.Revenue ?? 0);
                            newOrdersData[m - 1] = Number(item.totalOrders ?? item.TotalOrders ?? 0);
                        }
                    });
                }

                currentOrdersData = newOrdersData;
                revenueChart.data.datasets[0].data = newRevenueData;
                revenueChart.update();

                // Cập nhật nhãn năm
                if (tableYearLabel) tableYearLabel.textContent = res.year;
                if (kpiAnnualRevenueYearLabel) kpiAnnualRevenueYearLabel.textContent = res.year;
                if (kpiAnnualOrdersYearLabel) kpiAnnualOrdersYearLabel.textContent = res.year;

                // Cập nhật bảng đối chiếu 12 tháng (DOM an toàn, không dùng innerHTML)
                if (tableBody) {
                    tableBody.textContent = "";
                    for (let i = 0; i < 12; i++) {
                        const m = i + 1;
                        const rev = newRevenueData[i];
                        const ord = newOrdersData[i];

                        const tr = document.createElement("tr");

                        const tdMonth = document.createElement("td");
                        tdMonth.className = "text-center fw-semibold";
                        tdMonth.textContent = `Tháng ${m}`;
                        tr.appendChild(tdMonth);

                        const tdRev = document.createElement("td");
                        tdRev.className = "text-end";
                        tdRev.textContent = formatVnd(rev);
                        tr.appendChild(tdRev);

                        const tdOrd = document.createElement("td");
                        tdOrd.className = "text-end";
                        tdOrd.textContent = formatNumber(ord);
                        tr.appendChild(tdOrd);

                        tableBody.appendChild(tr);
                    }
                }

                // Cập nhật tổng dòng footer bảng
                if (tableTotalRevenue) tableTotalRevenue.textContent = formatVnd(res.totalRevenue);
                if (tableTotalOrders) tableTotalOrders.textContent = formatNumber(res.totalOrders);

                // Cập nhật thẻ KPI
                if (kpiAnnualRevenue) kpiAnnualRevenue.textContent = formatVnd(res.totalRevenue);
                if (kpiAnnualOrders) kpiAnnualOrders.textContent = formatNumber(res.totalOrders);
            })
            .catch(err => {
                console.error("Lỗi tải dữ liệu doanh thu:", err);
                notifyError("Đã xảy ra lỗi khi kết nối máy chủ để lấy dữ liệu biểu đồ.", "danger");
            })
            .finally(() => {
                yearSelect.disabled = false;
            });
        });
    }

    function notifyError(message, type = "danger") {
        if (typeof showToast === "function") {
            showToast(message, type);
        } else {
            alert(message);
        }
    }

    function sanitizeImageUrl(url) {
        if (!url || typeof url !== "string") return null;
        const trimmed = url.trim();
        if (!trimmed) return null;
        // Chặn các giao thức lạ/nguy hiểm: javascript:, data:, vbscript: hoặc protocol-relative //
        if (/^(javascript|data|vbscript):/i.test(trimmed)) return null;
        if (trimmed.startsWith("//")) return null;
        // Chỉ chấp nhận đường dẫn tương đối nội bộ (/...) hoặc http(s)://
        if (trimmed.startsWith("/") || /^https?:\/\//i.test(trimmed)) {
            return trimmed;
        }
        return null;
    }

    // Xử lý thay đổi Top sách (5/10/20) qua AJAX
    const topBooksSelect = document.getElementById("topBooksSelect");
    const topBooksContainer = document.getElementById("topBooksContainer");
    const topBooksCardTitle = document.getElementById("topBooksCardTitle");

    if (topBooksSelect && topBooksContainer) {
        topBooksSelect.addEventListener("change", function () {
            const topVal = parseInt(this.value, 10);
            if (![5, 10, 20].includes(topVal)) return;

            topBooksSelect.disabled = true;
            fetch(`/Home/GetTopBooksJson?top=${topVal}`, {
                headers: { "Accept": "application/json" }
            })
            .then(res => {
                if (!res.ok) throw new Error("HTTP " + res.status);
                return res.json();
            })
            .then(res => {
                if (!res || !res.success || !Array.isArray(res.data)) {
                    notifyError(res?.message || "Không thể tải danh sách top sách.", "warning");
                    return;
                }

                if (topBooksCardTitle) {
                    topBooksCardTitle.textContent = `Top ${topVal} Sách Bán Chạy`;
                }

                topBooksContainer.textContent = "";

                if (res.data.length === 0) {
                    const emptyDiv = document.createElement("div");
                    emptyDiv.className = "text-center text-muted py-5";
                    const icon = document.createElement("i");
                    icon.className = "fas fa-book-open fa-2x mb-2 d-block opacity-50";
                    const span = document.createElement("span");
                    span.textContent = "Chưa có dữ liệu sách bán chạy.";
                    emptyDiv.appendChild(icon);
                    emptyDiv.appendChild(span);
                    topBooksContainer.appendChild(emptyDiv);
                    return;
                }

                const listGroup = document.createElement("div");
                listGroup.className = "list-group list-group-flush";

                res.data.forEach((book, index) => {
                    const rank = index + 1;
                    const itemDiv = document.createElement("div");
                    itemDiv.className = "list-group-item d-flex align-items-center px-3 py-2 border-bottom";

                    // Rank badge
                    const badge = document.createElement("span");
                    let badgeClass = "bg-light text-secondary border";
                    if (rank === 1) badgeClass = "bg-warning text-dark fw-bold";
                    else if (rank === 2) badgeClass = "bg-secondary text-white";
                    else if (rank === 3) badgeClass = "bg-danger text-white";
                    badge.className = `badge rounded-pill ${badgeClass} me-3`;
                    badge.style.width = "24px";
                    badge.style.height = "24px";
                    badge.style.display = "inline-flex";
                    badge.style.alignItems = "center";
                    badge.style.justifyContent = "center";
                    badge.style.fontSize = "0.75rem";
                    badge.textContent = rank;
                    itemDiv.appendChild(badge);

                    // Image or icon
                    const safeImageUrl = sanitizeImageUrl(book.imageUrl);
                    if (safeImageUrl) {
                        const img = document.createElement("img");
                        img.src = safeImageUrl;
                        img.alt = book.title || "";
                        img.className = "rounded object-fit-cover me-2 border";
                        img.style.width = "36px";
                        img.style.height = "50px";
                        const fallbackDiv = document.createElement("div");
                        fallbackDiv.className = "bg-light border rounded d-none align-items-center justify-content-center text-muted me-2";
                        fallbackDiv.style.width = "36px";
                        fallbackDiv.style.height = "50px";
                        const iconBook = document.createElement("i");
                        iconBook.className = "fas fa-book small";
                        fallbackDiv.appendChild(iconBook);

                        img.onerror = function () {
                            this.style.display = "none";
                            fallbackDiv.classList.remove("d-none");
                        };
                        itemDiv.appendChild(img);
                        itemDiv.appendChild(fallbackDiv);
                    } else {
                        const fallbackDiv = document.createElement("div");
                        fallbackDiv.className = "bg-light border rounded d-flex align-items-center justify-content-center text-muted me-2";
                        fallbackDiv.style.width = "36px";
                        fallbackDiv.style.height = "50px";
                        const iconBook = document.createElement("i");
                        iconBook.className = "fas fa-book small";
                        fallbackDiv.appendChild(iconBook);
                        itemDiv.appendChild(fallbackDiv);
                    }

                    // Details
                    const detailsDiv = document.createElement("div");
                    detailsDiv.className = "flex-grow-1 min-w-0 me-2";

                    const titleDiv = document.createElement("div");
                    titleDiv.className = "text-truncate fw-semibold small text-dark";
                    titleDiv.title = book.title || "";
                    titleDiv.textContent = book.title || "";
                    detailsDiv.appendChild(titleDiv);

                    const soldDiv = document.createElement("div");
                    soldDiv.className = "text-muted";
                    soldDiv.style.fontSize = "0.75rem";
                    const cartIcon = document.createElement("i");
                    cartIcon.className = "fas fa-cart-shopping me-1";
                    soldDiv.appendChild(cartIcon);
                    soldDiv.appendChild(document.createTextNode("Đã bán: "));
                    const soldBold = document.createElement("span");
                    soldBold.className = "fw-bold text-dark";
                    soldBold.textContent = formatNumber(book.totalSold);
                    soldDiv.appendChild(soldBold);
                    soldDiv.appendChild(document.createTextNode(" cuốn"));
                    detailsDiv.appendChild(soldDiv);
                    itemDiv.appendChild(detailsDiv);

                    // Revenue
                    const revDiv = document.createElement("div");
                    revDiv.className = "text-end text-nowrap";
                    const revVal = document.createElement("div");
                    revVal.className = "small fw-bold text-success";
                    revVal.textContent = formatVnd(book.totalRevenue);
                    revDiv.appendChild(revVal);
                    itemDiv.appendChild(revDiv);

                    listGroup.appendChild(itemDiv);
                });

                topBooksContainer.appendChild(listGroup);
            })
            .catch(err => {
                console.error("Lỗi tải danh sách top sách:", err);
                notifyError("Đã xảy ra lỗi khi kết nối máy chủ để lấy top sách.", "danger");
            })
            .finally(() => {
                topBooksSelect.disabled = false;
            });
        });
    }
});
