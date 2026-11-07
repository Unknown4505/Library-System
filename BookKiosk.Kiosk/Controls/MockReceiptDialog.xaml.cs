using System;
using System.Windows;
using BookKiosk.Application.DTOs.Order;

namespace BookKiosk.Kiosk.Controls
{
    public partial class MockReceiptDialog : Window
    {
        public ReceiptDto Receipt { get; }
        public DateTime DisplayCompletedAt { get; }
        public bool HasPromotionDiscount => Receipt.DiscountAmount > 0;
        public bool HasPointsDiscount => Receipt.PointsUsedAmount > 0;
        public bool HasPromotionType => Receipt.PromotionType.HasValue;
        public string PromotionDisplayName => !string.IsNullOrWhiteSpace(Receipt.PromotionName)
            ? Receipt.PromotionName
            : "Khuyến mãi";

        public MockReceiptDialog(ReceiptDto receipt)
        {
            InitializeComponent();

            Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));

            var utcTime = receipt.CompletedAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(receipt.CompletedAt, DateTimeKind.Utc)
                : receipt.CompletedAt.ToUniversalTime();
            DisplayCompletedAt = utcTime.ToLocalTime();

            // Set DataContext so XAML can bind to properties
            DataContext = this;
        }
    }
}
