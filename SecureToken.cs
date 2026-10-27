using System;
using System.Security.Cryptography;
using System.Text;

namespace Journal;

/// <summary>
/// Windows DPAPI (CurrentUser) protection for the Notion token.
/// The token is never written to disk in plain text on Windows.
/// This path is only used on Windows; the app targets Windows only.
/// </summary>
public static class SecureToken
{
    public static string Protect(string plain)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI token storage is Windows-only.");
        var bytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public static string? Unprotect(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI token storage is Windows-only.");
        var bytes = ProtectedData.Unprotect(
            Convert.FromBase64String(base64), null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
