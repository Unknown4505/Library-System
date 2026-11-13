using BookKiosk.Application.DTOs.Users;
using BookKiosk.CMS.Models.Common;

namespace BookKiosk.CMS.ViewModels.Users
{
    public class UserManagementViewModel
    {
        public bool UseMockApi { get; set; }
        public bool IsBackendAvailable { get; set; }

        public int TotalUsers { get; set; }
        public int AdminCount { get; set; }
        public int ManagerCount { get; set; }
        public int StaffCount { get; set; }

        public PaginatedResult<UserDto> Users { get; set; } = new();

        public string? SearchKeyword { get; set; }
        public string? SelectedRole { get; set; }
        public bool? SelectedStatus { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }
}
