using Bookstore.Mobile.Models;
using Refit;

namespace Bookstore.Mobile.Interfaces.Apis
{
    // Cần Auth Header (Admin)
    public interface IAdminUserApi
    {
        [Get("/admin/users")]
        Task<ApiResponse<PagedResult<UserDto>>> GetUsers(
            [Query] int page = 1,
            [Query] int pageSize = 15,
            [Query] string? role = null,
            [Query] bool? isActive = null,
            [Query] string? search = null);

        [Get("/admin/users/{userId}")]
        Task<ApiResponse<UserDto>> GetUserById(Guid userId);

        [Put("/admin/users/{userId}/status")]
        Task<ApiResponse<object>> UpdateUserStatus(Guid userId, [Body] UpdateUserStatusDto dto);
    }
}