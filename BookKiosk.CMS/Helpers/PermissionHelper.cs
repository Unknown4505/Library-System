using System.Security.Claims;

namespace BookKiosk.CMS.Helpers
{
    public static class PermissionHelper
    {
        public const string RoleAdmin = "Admin";
        public const string RoleStaff = "Staff";
        public const string RoleManager = "Manager";

        public static bool IsAdmin(this ClaimsPrincipal user)
        {
            return user.IsInRole(RoleAdmin);
        }

        public static bool IsStaff(this ClaimsPrincipal user)
        {
            return user.IsInRole(RoleStaff);
        }

        public static bool IsManager(this ClaimsPrincipal user)
        {
            return user.IsInRole(RoleManager);
        }

        /// <summary>
        /// Điều hướng trang đích mặc định tập trung theo vai trò (CMS-MANAGER-01):
        /// - Admin -> Home/Index (Dashboard)
        /// - Manager -> Home/StaffWorkspace
        /// - Staff -> Pos/Index
        /// - Khác / Fallback -> Home/StaffWorkspace (an toàn, tránh 403)
        /// </summary>
        public static (string Action, string Controller) GetDefaultLandingRoute(this ClaimsPrincipal user)
        {
            if (user.IsAdmin())
            {
                return ("Index", "Home");
            }

            if (user.IsManager())
            {
                return ("StaffWorkspace", "Home");
            }

            if (user.IsStaff())
            {
                return ("Index", "Pos");
            }

            return ("StaffWorkspace", "Home");
        }
    }
}
