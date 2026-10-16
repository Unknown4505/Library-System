using BookKiosk.CMS.Models.Catalog;
using BookKiosk.CMS.Models.Common;

namespace BookKiosk.CMS.ViewModels.Catalog
{
    public class BookListViewModel
    {
        public PaginatedResult<BookDto> Books { get; set; } = new();

        // Filters
        public string? Keyword { get; set; }
        public int? CategoryId { get; set; }
        public int? AreaId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        // Dropdown reference data
        public IEnumerable<CategoryDto> Categories { get; set; } = new List<CategoryDto>();
        public IEnumerable<AreaDto> Areas { get; set; } = new List<AreaDto>();
    }
}
