using System;
using System.Collections.Generic;
using System.Windows;
using BookKiosk.Kiosk.Models;

namespace BookKiosk.Kiosk.Controls
{
    public partial class MockReceiptDialog : Window
    {
        public List<CartItemModel> Items { get; }
        public decimal TotalAmount { get; }
        public DateTime CurrentDate { get; }

        public MockReceiptDialog(List<CartItemModel> items, decimal totalAmount)
        {
            InitializeComponent();
            
            Items = items;
            TotalAmount = totalAmount;
            CurrentDate = DateTime.Now;
            
            // Set DataContext so XAML can bind to the properties above
            DataContext = this;
        }
    }
}
