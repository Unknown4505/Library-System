using System.Windows.Input;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Pages;

namespace BookKiosk.Kiosk.ViewModels
{
    public class MemberViewModel : BaseViewModel
    {
        private readonly CartService _cartService;
        private readonly NavigationService _navigationService;

        private string _phoneNumber = "";
        public string PhoneNumber
        {
            get => _phoneNumber;
            set 
            { 
                _phoneNumber = value; 
                OnPropertyChanged();
                
                // Mock: Khi nhập đủ 10 số thì hiển thị điểm
                if (_phoneNumber != null && _phoneNumber.Length >= 10)
                {
                    AvailablePoints = 500; // Giả lập có 500 điểm
                }
                else
                {
                    AvailablePoints = 0;
                    PointsUsed = 0;
                }
            }
        }

        private int _availablePoints = 0;
        public int AvailablePoints
        {
            get => _availablePoints;
            set { _availablePoints = value; OnPropertyChanged(); }
        }

        private int _pointsUsed = 0;
        public int PointsUsed
        {
            get => _pointsUsed;
            set 
            {
                _pointsUsed = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAmount));
                
                string newText = _pointsUsed > 0 ? _pointsUsed.ToString() : "";
                if (_pointsUsedText != newText)
                {
                    _pointsUsedText = newText;
                    OnPropertyChanged(nameof(PointsUsedText));
                }
            }
        }

        private string _pointsUsedText = "";
        public string PointsUsedText
        {
            get => _pointsUsedText;
            set
            {
                _pointsUsedText = value;
                OnPropertyChanged();
                if (int.TryParse(value, out int parsed))
                {
                    if (PointsUsed != parsed) PointsUsed = parsed;
                }
                else
                {
                    if (PointsUsed != 0) PointsUsed = 0;
                }
            }
        }

        public decimal SubTotal => _cartService.GetTotalAmount();
        
        public decimal TotalAmount
        {
            get
            {
                decimal discount = PointsUsed * 1000m;
                decimal total = SubTotal - discount;
                return total < 0 ? 0 : total;
            }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand UseMaxPointsCommand { get; }

        public MemberViewModel(CartService cartService, NavigationService navigationService)
        {
            _cartService = cartService;
            _navigationService = navigationService;

            ConfirmCommand = new RelayCommand(_ => ExecuteConfirm());
            UseMaxPointsCommand = new RelayCommand(_ => ExecuteUseMaxPoints());
        }

        public override void Initialize(object parameter)
        {
            base.Initialize(parameter);
            PhoneNumber = "";
            PointsUsed = 0;
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(TotalAmount));
        }

        private void ExecuteConfirm()
        {
            // Nếu có SĐT thì áp dụng điểm (Thực tế sẽ gọi API check SĐT)
            _navigationService.Navigate<CheckoutPage>(new CheckoutParameter { TotalAmount = TotalAmount, PointsUsed = PointsUsed });
        }

        private void ExecuteUseMaxPoints()
        {
            if (string.IsNullOrEmpty(PhoneNumber))
            {
                ErrorMessage = "Vui lòng nhập Số điện thoại trước khi sử dụng điểm!";
                return;
            }

            // Quy tắc: 1 điểm = 1000đ.
            // Không được dùng điểm lớn hơn tổng giá trị đơn hàng và số điểm đang có
            int maxPointsForOrder = (int)(SubTotal / 1000m);
            int maxPointsAllowed = Math.Min(maxPointsForOrder, AvailablePoints);
            
            PointsUsed = maxPointsAllowed;
            ErrorMessage = $"Đã áp dụng tối đa {PointsUsed} điểm.";
        }
    }

    public class CheckoutParameter
    {
        public decimal TotalAmount { get; set; }
        public int PointsUsed { get; set; }
    }
}
