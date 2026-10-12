using System.ComponentModel.DataAnnotations;

namespace Api.DTOs;

public sealed class RegisterRequest
{
    private string _username = string.Empty;

    // Chuẩn hóa trước khi validation chạy: "  ab  " vẫn là tên quá ngắn.
    /// <summary>Tên đăng nhập, bỏ khoảng trắng hai đầu.</summary>
    /// <example>player01</example>
    [Required, StringLength(32, MinimumLength = 3)]
    public string Username
    {
        get => _username;
        set => _username = (value ?? string.Empty).Trim();
    }

    // Không Trim password: khoảng trắng có thể là một phần của mật khẩu.
    /// <summary>Mật khẩu demo, không bị Trim và chỉ lưu hash phía server.</summary>
    /// <example>ClassDemo_123!</example>
    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    private string _username = string.Empty;

    /// <summary>Tên tài khoản đã đăng ký, không phân biệt hoa/thường.</summary>
    /// <example>player01</example>
    [Required, StringLength(32, MinimumLength = 3)]
    public string Username
    {
        get => _username;
        set => _username = (value ?? string.Empty).Trim();
    }

    /// <summary>Mật khẩu của tài khoản demo.</summary>
    /// <example>ClassDemo_123!</example>
    [Required, StringLength(128)]
    public string Password { get; set; } = string.Empty;
}

// DTO response chỉ chứa các trường được phép trả ra, không trả model MongoDB trực tiếp.
public sealed record UserResponse(string Id, string Username, DateTime CreatedAtUtc);
public sealed record LoginResponse(string AccessToken, string UserId, DateTime ExpiresAtUtc);
