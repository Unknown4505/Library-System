using System;
using System.Linq;
using System.Windows.Input;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Services.Api;
using BookKiosk.Kiosk.Pages;
using BookKiosk.Application.DTOs.Order;

namespace BookKiosk.Kiosk.ViewModels
{
    public class MemberViewModel : BaseViewModel
    {
        private readonly CartService _cartService;
        private readonly NavigationService _navigationService;
        private readonly IBookKioskApiClient _api;

        public int? MemberId { get; private set; }

        private int _phoneCheckVersion = 0;
        private string _phoneNumber = "";
        public string PhoneNumber
        {
            get => _phoneNumber;
            set 
            { 
                _phoneNumber = value; 
                OnPropertyChanged();
                
                if (_phoneNumber != null && _phoneNumber.Length == 10)
                {
                    CheckMemberPhoneAsync(_phoneNumber);
                }
                else
                {
                    _phoneCheckVersion++; // Hủy mọi kết quả tra cứu của SĐT cũ
                    AvailablePoints = 0;
                    PointsUsed = 0;
                    MemberId = null;
                    ErrorMessage = "";
                }
            }
        }

        private async void CheckMemberPhoneAsync(string phone)
        {
            int currentVersion = ++_phoneCheckVersion;
            try
            {
                var response = await _api.GetMemberByPhoneAsync(phone);
                
                // FE-14: Chỉ chấp nhận kết quả tương ứng SĐT hiện tại và version mới nhất
                if (currentVersion != _phoneCheckVersion || phone != _phoneNumber)
                {
                    return;
                }

                if (response != null && response.Success && response.Data != null)
                {
                    AvailablePoints = response.Data.Points;
                    MemberId = response.Data.MemberId;
                    ErrorMessage = "";

                    // FE-12: Tự động điều chỉnh điểm dùng nếu đã nhập trước đó
                    if (PointsUsed > 0)
                    {
                        PointsUsed = Math.Min(PointsUsed, GetMaxPointsAllowed());
                    }
                }
                else
                {
                    AvailablePoints = 0;
                    MemberId = null;
                    PointsUsed = 0;
                    ErrorMessage = response?.Message ?? "Số điện thoại chưa được đăng ký thành viên. Vui lòng đăng ký ở quầy.";
                }
            }
            catch
            {
                if (currentVersion != _phoneCheckVersion || phone != _phoneNumber) return;
                AvailablePoints = 0;
                MemberId = null;
                PointsUsed = 0;
                ErrorMessage = "Không thể kết nối máy chủ để kiểm tra thành viên. Vui lòng thử lại.";
            }
        }

        private int _availablePoints = 0;
        public int AvailablePoints
        {
            get => _availablePoints;
            set 
            { 
                _availablePoints = value; 
                OnPropertyChanged(); 
                if (PointsUsed > 0)
                {
                    PointsUsed = Math.Min(PointsUsed, GetMaxPointsAllowed());
                }
            }
        }

        /// <summary>
        /// FE-12: Tính số điểm tối đa được phép dùng cho đơn hiện tại:
        /// - Giới hạn trần 0..100 điểm/đơn
        /// - Không vượt số dư khả dụng (AvailablePoints)
        /// - Không vượt giá trị thanh toán của đơn (SubTotal / 1000m)
        /// </summary>
        public int GetMaxPointsAllowed()
        {
            int maxPointsForOrder = (int)(SubTotal / 1000m);
            return Math.Max(0, Math.Min(100, Math.Min(AvailablePoints, maxPointsForOrder)));
        }

        private int _pointsUsed = 0;
        public int PointsUsed
        {
            get => _pointsUsed;
            set 
            {
                int maxAllowed = GetMaxPointsAllowed();
                int clamped = Math.Clamp(value, 0, maxAllowed);
                
                _pointsUsed = clamped; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(Discount));
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
                    int maxAllowed = GetMaxPointsAllowed();
                    if (parsed < 0)
                    {
                        PointsUsed = 0;
                        ErrorMessage = "Số điểm sử dụng không được là số âm.";
                    }
                    else if (parsed > maxAllowed)
                    {
                        PointsUsed = maxAllowed;
                        ErrorMessage = $"Tối đa được áp dụng {maxAllowed} điểm cho đơn hàng này (tối đa 100 điểm/đơn, không vượt số dư và tổng tiền).";
                    }
                    else
                    {
                        if (PointsUsed != parsed) PointsUsed = parsed;
                    }
                }
                else
                {
                    if (PointsUsed != 0) PointsUsed = 0;
                }
            }
        }

        public decimal SubTotal => _cartService.GetTotalAmount();
        
        public decimal Discount => PointsUsed * 1000m;

        public decimal TotalAmount
        {
            get
            {
                decimal total = SubTotal - Discount;
                return total < 0 ? 0 : total;
            }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand UseMaxPointsCommand { get; }

        public MemberViewModel(CartService cartService, NavigationService navigationService, IBookKioskApiClient api)
        {
            _cartService = cartService;
            _navigationService = navigationService;
            _api = api;

            // FE-19: Chặn re-entry / double-click thông qua CanExecute
            ConfirmCommand = new RelayCommand(_ => ExecuteConfirm(), _ => !IsLoading);
            UseMaxPointsCommand = new RelayCommand(_ => ExecuteUseMaxPoints());
        }

        public override void Initialize(object parameter)
        {
            base.Initialize(parameter);
            PhoneNumber = "";
            PointsUsed = 0;
            ErrorMessage = "";
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(Discount));
            OnPropertyChanged(nameof(TotalAmount));
        }

        private async void ExecuteConfirm()
        {
            // FE-19: Guard clause chặn re-entry khi request đang xử lý
            if (IsLoading) return;
            IsLoading = true;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();

            try
            {
                var request = new CheckoutRequestDto
                {
                    MemberId = this.MemberId,
                    PointsToUse = this.PointsUsed,
                    Items = _cartService.Items.Select(x => new CheckoutItemDto 
                    { 
                        BookId = x.Book.BookId, 
                        Quantity = x.Quantity 
                    }).ToList()
                };

                var response = await _api.CheckoutAsync(request);

                if (response != null && response.Success && response.Data != null)
                {
                    _navigationService.Navigate<CheckoutPage>(new CheckoutParameter 
                    { 
                        TotalAmount = response.Data.TotalAmount,
                        DiscountAmount = response.Data.DiscountAmount,
                        PointsUsedAmount = response.Data.PointsUsedAmount,
                        PointsUsed = PointsUsed,
                        MemberId = this.MemberId,
                        OrderId = response.Data.OrderId,
                        OrderCode = response.Data.OrderCode,
                        SepayQrCodeUrl = response.Data.SepayQrCodeUrl
                    });
                }
                else
                {
                    ErrorMessage = response?.Message ?? "Thanh toán thất bại. Vui lòng thử lại.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Có lỗi xảy ra khi tạo đơn hàng: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ExecuteUseMaxPoints()
        {
            if (string.IsNullOrEmpty(PhoneNumber))
            {
                ErrorMessage = "Vui lòng nhập Số điện thoại trước khi sử dụng điểm!";
                return;
            }

            if (AvailablePoints <= 0)
            {
                ErrorMessage = "Số dư điểm khả dụng hiện tại là 0.";
                return;
            }

            // FE-12: Giới hạn 0..100 điểm, không vượt AvailablePoints và giá trị payable
            int maxPointsAllowed = GetMaxPointsAllowed();
            
            PointsUsed = maxPointsAllowed;
            ErrorMessage = $"Đã áp dụng tối đa {PointsUsed} điểm.";
        }
    }

    public class CheckoutParameter
    {
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal PointsUsedAmount { get; set; }
        public int PointsUsed { get; set; }
        public int? MemberId { get; set; }
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string SepayQrCodeUrl { get; set; } = string.Empty;
    }
}
