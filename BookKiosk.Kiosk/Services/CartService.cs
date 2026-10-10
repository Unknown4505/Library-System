using System;
using System.Collections.ObjectModel;
using System.Linq;
using BookKiosk.Kiosk.Models;

namespace BookKiosk.Kiosk.Services
{
    public class CartService
    {
        public ObservableCollection<CartItemModel> Items { get; } = new ObservableCollection<CartItemModel>();

        public event Action CartChanged;

        public void AddItem(BookModel book)
        {
            var existingItem = Items.FirstOrDefault(i => i.Book.BookId == book.BookId);
            if (existingItem != null)
            {
                if (existingItem.Quantity < book.AvailableStock)
                {
                    existingItem.Quantity++;
                }
            }
            else
            {
                Items.Add(new CartItemModel { Book = book, Quantity = 1 });
            }
            CartChanged?.Invoke();
        }

        public void DecreaseItem(int bookId)
        {
            var existingItem = Items.FirstOrDefault(i => i.Book.BookId == bookId);
            if (existingItem != null)
            {
                if (existingItem.Quantity > 1)
                {
                    existingItem.Quantity--;
                    CartChanged?.Invoke();
                }
                else
                {
                    RemoveItem(bookId);
                }
            }
        }

        public void RemoveItem(int bookId)
        {
            var item = Items.FirstOrDefault(i => i.Book.BookId == bookId);
            if (item != null)
            {
                Items.Remove(item);
                CartChanged?.Invoke();
            }
        }

        public void ClearCart()
        {
            Items.Clear();
            CartChanged?.Invoke();
        }

        public decimal GetTotalAmount()
        {
            return Items.Sum(i => i.TotalPrice);
        }
    }
}
