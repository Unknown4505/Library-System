using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BookKiosk.CMS.Models.Inventory
{
    public class ImportReceiptDto
    {
        public int ImportReceiptId { get; set; }
        public string ReceiptNumber => $"PNK-{ImportReceiptId:D6}";
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public decimal TotalAmount { get; set; }

        public string? Note { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes
        {
            get => Note;
            set => Note = value;
        }

        public int ItemCount { get; set; }
        public int TotalQuantity { get; set; }
    }

    public class ImportReceiptDetailDto
    {
        public int ImportReceiptId { get; set; }
        public string ReceiptNumber => $"PNK-{ImportReceiptId:D6}";
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierPhone { get; set; }
        public int UserId { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public decimal TotalAmount { get; set; }

        public string? Note { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes
        {
            get => Note;
            set => Note = value;
        }

        public List<ImportReceiptItemDto> Details { get; set; } = new();

        [JsonPropertyName("items")]
        public List<ImportReceiptItemDto> Items
        {
            get => Details;
            set => Details = value;
        }
    }

    public class ImportReceiptItemDto
    {
        public int BookId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Author { get; set; }
        public string? ImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal CostPrice { get; set; }

        [JsonPropertyName("importPrice")]
        public decimal ImportPrice
        {
            get => CostPrice;
            set => CostPrice = value;
        }

        public decimal LineTotal => Quantity * (ImportPrice > 0 ? ImportPrice : CostPrice);
    }

    public class CreateImportReceiptDto
    {
        [Required(ErrorMessage = "Vui lòng chọn nhà cung cấp")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nhà cung cấp")]
        [Display(Name = "Nhà cung cấp")]
        public int SupplierId { get; set; }

        [MaxLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự")]
        [Display(Name = "Ghi chú phiếu nhập")]
        public string? Note { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes
        {
            get => Note;
            set => Note = value;
        }

        [Required(ErrorMessage = "Phiếu nhập phải có ít nhất 1 đầu sách")]
        public List<CreateImportReceiptItemDto> Details { get; set; } = new();

        [JsonPropertyName("items")]
        public List<CreateImportReceiptItemDto> Items
        {
            get => Details;
            set => Details = value;
        }
    }

    public class CreateImportReceiptItemDto
    {
        [Required(ErrorMessage = "Mã sách không được để trống")]
        public int BookId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng nhập phải lớn hơn 0")]
        public int Quantity { get; set; } = 1;

        [Range(0, double.MaxValue, ErrorMessage = "Giá nhập phải lớn hơn hoặc bằng 0")]
        public decimal CostPrice { get; set; }

        [JsonPropertyName("importPrice")]
        public decimal ImportPrice
        {
            get => CostPrice;
            set => CostPrice = value;
        }
    }
}
