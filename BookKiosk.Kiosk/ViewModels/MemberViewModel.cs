using System;
using System.Linq;
using System.Windows.Input;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Services.Api;
using BookKiosk.Kiosk.Pages;
using BookKiosk.Application.DTOs.Order;

namespace BookKiosk.Kiosk.ViewModels
{
    /// <summary>
    /// [MEMBER-01] Trạng thái tra cứu thành viên. Phân biệt rõ các trường hợp để CanConfirm() không tự fallback sang MemberId = null.
    /// </summary>
    public enum MemberLookupState
    {
        /// <summary>Chưa nhập số điện thoại — khách vãng lai mặc định.</summary>
        Idle,
        /// <summary>Đang gọi API tra cứu.</summary>
        Checking,
        /// <summary>Lookup thành công, MemberId != null.</summary>
        Verified,
        /// <summary>SĐT không tịn tại trong hệ thống (NOT_FOUND).</summary>
        NotFound,
        /// <summary>Lỗi mạng hoặc server 5xx.</summary>
        Failed,
        /// <summary>User chủ động bỏ qua thành viên — cho phép tạo đơn khách.</summary>
        Skipped
    }

    public class MemberViewModel : BaseViewModel
    {
        private readonly CartService _cartService;
        private readonly NavigationService _navigationService;
        private readonly IBookKioskApiClient _api;

        public int? MemberId { get; private set; }

        // [MEMBER-01] Trạng thái lookup chính xác
        private MemberLookupState _lookupState = MemberLookupState.Idle;
        public MemberLookupState LookupState
        {
            get => _lookupState;
            private set
            {
                _lookupState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCheckingMember));
                OnPropertyChanged(nameof(ShowSkipButton));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>Backward-compat: true khi đang gọi API.</summary>
        public bool IsCheckingMember => _lookupState == MemberLookupState.Checking;

        /// <summary>[MEMBER-01] Nút Bỏ qua chỉ hiện khi lookup thất bại (NotFound hoặc Failed).</summary>
        public bool ShowSkipButton => _lookupState == MemberLookupState.NotFound || _lookupState == MemberLookupState.Failed;

        private int _phoneCheckVersion = 0;
        private string _phoneNumber = "";
        public string PhoneNumber
        {
            get => _phoneNumber;
            set 
            { 
                if (_phoneNumber == value) return;

                _phoneNumber = value; 
                OnPropertyChanged();
                
                // FE-14: Luôn hủy tra cứu cũ và reset ngay lập tức MemberId, điểm về trạng thái ban đầu
                _phoneCheckVersion++;
                MemberId = null;
                AvailablePoints = 0;
                PointsUsed = 0;
                ErrorMessage = "";

                if (!string.IsNullOrWhiteSpace(_phoneNumber) && _phoneNumber.Length == 10)
                {
                    // [MEMBER-01] Chuyển sang Checking trước khi gọi API
                    LookupState = MemberLookupState.Checking;
                    CheckMemberPhoneAsync(_phoneNumber);
                }
                else
                {
                    // [MEMBER-01] Reset về Idle khi xóa hoặc nhập dở số điện thoại
                    LookupState = MemberLookupState.Idle;
                }
            }
        }

        private async void CheckMemberPhoneAsync(string phone)
        {
            int currentVersion = _phoneCheckVersion;
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

                    // [MEMBER-01] Lookup thành công
                    LookupState = MemberLookupState.Verified;
                }
                else
                {
                    AvailablePoints = 0;
                    MemberId = null;
                    PointsUsed = 0;
                    ErrorMessage = response?.Message ?? "Số điện thoại chưa được đăng ký thành viên. Vui lòng đăng ký ở quầy.";

                    // [MEMBER-01] NOT_FOUND — hiện nút Bỏ qua, khóa Confirm
                    LookupState = MemberLookupState.NotFound;
                }
            }
            catch
            {
                if (currentVersion != _phoneCheckVersion || phone != _phoneNumber) return;
                AvailablePoints = 0;
                MemberId = null;
                PointsUsed = 0;
                ErrorMessage = "Không thể kết nối máy chủ để kiểm tra thành viên. Vui lòng thử lại hoặc bỏ qua.";

                // [MEMBER-01] Lỗi mạng/500 — hiện nút Bỏ qua, khóa Confirm
                LookupState = MemberLookupState.Failed;
            }
            // Không có finally reset IsCheckingMember — LookupState đã thay thế.
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

        /// <summary>[MEMBER-01] Cho phép user chủ động bỏ qua thành viên và tạo đơn khách.</summary>
        public ICommand SkipMemberCommand { get; }

        public MemberViewModel(CartService cartService, NavigationService navigationService, IBookKioskApiClient api)
        {
            _cartService = cartService;
            _navigationService = navigationService;
            _api = api;

            // FE-14 & FE-19: Chặn re-entry / double-click và chặn confirm khi đang tra cứu SĐT
            ConfirmCommand = new RelayCommand(_ => ExecuteConfirm(), _ => CanConfirm());
            UseMaxPointsCommand = new RelayCommand(_ => ExecuteUseMaxPoints());

            // [MEMBER-01] Nút Bỏ qua chỉ active khi lookup thất bại
            SkipMemberCommand = new RelayCommand(_ => ExecuteSkipMember(), _ => CanSkip());
        }

        private bool CanConfirm()
        {
            if (IsLoading) return false;

            // [MEMBER-01] Kiểm tra LookupState thay vì chỉ kiểm tra IsCheckingMember
            return _lookupState switch
            {
                // Khách vãng lai (chưa nhập SĐT)
                MemberLookupState.Idle => string.IsNullOrEmpty(PhoneNumber),
                // Lookup thành công
                MemberLookupState.Verified => true,
                // User chủ động bỏ qua
                MemberLookupState.Skipped => true,
                // Checking, NotFound, Failed — khóa Confirm
                _ => false
            };
        }

        /// <summary>[MEMBER-01] Nút Bỏ qua chỉ khả dụng khi lookup thất bại.</summary>
        private bool CanSkip()
        {
            return !IsLoading && (_lookupState == MemberLookupState.NotFound || _lookupState == MemberLookupState.Failed);
        }

        /// <summary>[MEMBER-01] User chủ động chọn bỏ qua — set Skipped để mở khóa Confirm với MemberId = null.</summary>
        private void ExecuteSkipMember()
        {
            if (!CanSkip()) return;
            MemberId = null;
            AvailablePoints = 0;
            PointsUsed = 0;
            LookupState = MemberLookupState.Skipped;
            ErrorMessage = "Bạn đã chọn bỏ qua tích điểm. Đơn hàng sẽ được xử lý với tài khoản khách.";
        }

        public override void Initialize(object parameter)
        {
            base.Initialize(parameter);
            _phoneCheckVersion++;
            // [MEMBER-01] Reset về Idle khi vào trang mới
            LookupState = MemberLookupState.Idle;
            PhoneNumber = "";
            PointsUsed = 0;
            ErrorMessage = "";
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(Discount));
            OnPropertyChanged(nameof(TotalAmount));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private async void ExecuteConfirm()
        {
            // FE-14 & FE-19: Guard clause chặn khi đang tạo đơn hoặc đang tra cứu SĐT
            if (IsLoading) return;

            // [MEMBER-01] Guard dựa trên LookupState — không tự fallback sang guest
            switch (_lookupState)
            {
                case MemberLookupState.Checking:
                    ErrorMessage = "Đang kiểm tra thông tin thành viên, vui lòng đợi...";
                    return;
                case MemberLookupState.NotFound:
                    ErrorMessage = "Số điện thoại không tìm thấy. Vui lòng nhập lại hoặc nhấn 'Bỏ qua tích điểm' để tiếp tục.";
                    return;
                case MemberLookupState.Failed:
                    ErrorMessage = "Lỗi kết nối. Vui lòng thử lại hoặc nhấn 'Bỏ qua tích điểm' để tiếp tục.";
                    return;
                // Idle (không nhập SĐT), Verified, Skipped — được phép tiếp tục
            }

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
            if (IsCheckingMember)
            {
                ErrorMessage = "Đang kiểm tra thông tin thành viên, vui lòng đợi trong giây lát...";
                return;
            }

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
