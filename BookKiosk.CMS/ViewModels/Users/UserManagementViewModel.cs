using System;
using System.Collections.Generic;

namespace BookKiosk.CMS.ViewModels.Users
{
    public class UserAccountItem
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Staff"; // Admin, Manager, Staff
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
    }

    public class UserManagementViewModel
    {
        public bool UseMockApi { get; set; }
        public bool IsBackendAvailable { get; set; }

        public int TotalUsers { get; set; }
        public int AdminCount { get; set; }
        public int ManagerCount { get; set; }
        public int StaffCount { get; set; }

        public List<UserAccountItem> Users { get; set; } = new();

        public string? SearchKeyword { get; set; }
        public string? SelectedRole { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }
}
