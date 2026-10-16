using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BookKiosk.CMS.Models.Common
{
    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new();

        /// <summary>
        /// Đồng bộ với thuộc tính 'page' trả về từ Backend API
        /// </summary>
        [JsonPropertyName("page")]
        public int Page
        {
            get => PageIndex;
            set => PageIndex = value;
        }

        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int TotalCount { get; set; }

        private int _totalPages;
        public int TotalPages
        {
            get => _totalPages > 0 ? _totalPages : (PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0);
            set => _totalPages = value;
        }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }
}
