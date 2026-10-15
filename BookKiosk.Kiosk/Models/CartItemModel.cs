using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BookKiosk.Kiosk.Models
{
    public class CartItemModel : INotifyPropertyChanged
    {
        public BookModel Book { get; set; }
        
        private int _quantity;
        public int Quantity 
        { 
            get => _quantity; 
            set 
            {
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPrice));
            }
        }

        public decimal TotalPrice => Book.SellingPrice * Quantity;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
