using System.Security.Claims;

namespace BookKiosk.CMS.Helpers
{
    public static class PermissionHelper
    {
        public const string RoleAdmin = "Admin";
        public const string RoleStaff = "Staff";

        public static bool IsAdmin(this ClaimsPrincipal user)
        {
            return user.IsInRole(RoleAdmin);
        }

        public static bool IsStaff(this ClaimsPrincipal user)
        {
            return user.IsInRole(RoleStaff);
        }
    }
}
