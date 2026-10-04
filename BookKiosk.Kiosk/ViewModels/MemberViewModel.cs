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
            set { _phoneNumber = value; OnPropertyChanged(); }
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

        public ICommand SkipCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand UseMaxPointsCommand { get; }

        public MemberViewModel(CartService cartService, NavigationService navigationService)
        {
            _cartService = cartService;
            _navigationService = navigationService;

            SkipCommand = new RelayCommand(_ => ExecuteSkip());
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

        private void ExecuteSkip()
        {
            // Bỏ qua nhập thông tin, đi tới CheckoutPage
            _navigationService.Navigate<CheckoutPage>(new CheckoutParameter { TotalAmount = SubTotal, PointsUsed = 0 });
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

            // Quy tắc: 1 điểm = 1000đ. Tối đa dùng 100 điểm.
            // Không được dùng điểm lớn hơn tổng giá trị đơn hàng
            int maxPointsAllowed = (int)(SubTotal / 1000m);
            if (maxPointsAllowed > 100) 
            {
                maxPointsAllowed = 100;
            }
            
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
