namespace BookKiosk.Kiosk.Models
{
    public class BookModel
    {
        public int BookId { get; set; }
        public string Barcode { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public decimal SellingPrice { get; set; }
        public string ImageUrl { get; set; }
        public int CategoryId { get; set; }
        public string AreaName { get; set; }
        public int AvailableStock { get; set; }
    }
}
