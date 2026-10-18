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
                    alert(res?.message || "Không thể tải dữ liệu doanh thu cho năm đã chọn.");
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

                // Cập nhật bảng đối chiếu 12 tháng
                if (tableBody) {
                    let rowsHtml = "";
                    for (let i = 0; i < 12; i++) {
                        const m = i + 1;
                        const rev = newRevenueData[i];
                        const ord = newOrdersData[i];
                        rowsHtml += `
                            <tr>
                                <td class="text-center fw-semibold">Tháng ${m}</td>
                                <td class="text-end">${formatVnd(rev)}</td>
                                <td class="text-end">${formatNumber(ord)}</td>
                            </tr>
                        `;
                    }
                    tableBody.innerHTML = rowsHtml;
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
                alert("Đã xảy ra lỗi khi kết nối máy chủ để lấy dữ liệu biểu đồ.");
            })
            .finally(() => {
                yearSelect.disabled = false;
            });
        });
    }
});
