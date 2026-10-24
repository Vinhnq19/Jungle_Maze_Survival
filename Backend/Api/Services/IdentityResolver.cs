using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Api.Services;

public sealed record ResolvedIdentity(string ProfileKey, bool IsGuest, string? Error = null);

public sealed class IdentityResolver(GuestService guestService, IConfiguration configuration)
{
    private readonly bool _publicHosting = configuration.GetValue<bool>("PUBLIC_HOSTING");

    public bool IsSecureOrLoopback(HttpContext context)
    {
        if (context.Request.IsHttps) return true;

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp != null && IPAddress.IsLoopback(remoteIp)) return true;

        // Trust this deployment boundary only for private Docker peers, never an arbitrary Internet header.
        var ip = remoteIp?.IsIPv4MappedToIPv6 == true ? remoteIp.MapToIPv4() : remoteIp;
        var bytes = ip?.GetAddressBytes();
        var privatePeer = bytes is { Length: 4 } && (bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31);
        if (privatePeer && !_publicHosting && configuration.GetValue<bool>("ALLOW_LOCAL_HTTP")) return true;
        if (privatePeer && _publicHosting && string.Equals(context.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    /// <summary>
    /// Resolves caller identity from either JWT Bearer or X-Guest-Credential header.
    /// Never logs proof, JWT, room codes, or tickets.
    /// </summary>
    public async Task<ResolvedIdentity> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (!IsSecureOrLoopback(context)) return new ResolvedIdentity("", false, "HttpsRequired");
        // 1. Check account JWT Bearer first
        var sub = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(sub))
        {
            return new ResolvedIdentity($"account:{sub}", IsGuest: false);
        }

        // 2. Check guest credential header
        var guestCredential = context.Request.Headers["X-Guest-Credential"].ToString();
        if (!string.IsNullOrWhiteSpace(guestCredential))
        {
            // Client must use HTTPS except HTTP loopback
            if (!IsSecureOrLoopback(context))
            {
                return new ResolvedIdentity("", IsGuest: true, Error: "HttpsRequired");
            }

            var guestProfileId = await guestService.ValidateCredentialAsync(guestCredential, cancellationToken);
            if (string.IsNullOrEmpty(guestProfileId))
            {
                return new ResolvedIdentity("", IsGuest: true, Error: "InvalidCredentials");
            }

            return new ResolvedIdentity($"guest:{guestProfileId}", IsGuest: true);
        }

        return new ResolvedIdentity("", IsGuest: false, Error: "Unauthorized");
    }
}
