using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api.Models;
using Api.Repositories;
using JungleBackend.Shared;

namespace Api.Services;

public sealed class GuestService(IGuestRepository repository)
{
    public async Task<GuestResponse> CreateGuestAsync(CancellationToken cancellationToken = default)
    {
        // 256-bit cryptographically secure opaque credential
        var credentialBytes = RandomNumberGenerator.GetBytes(32);
        var credential = Convert.ToHexString(credentialBytes).ToLowerInvariant();

        // Database only stores the SHA-256 hash of the credential, never plaintext
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(credential));
        var credentialHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var profileId = Guid.NewGuid().ToString("D");
        var profile = new GuestProfile
        {
            Id = profileId,
            CredentialHash = credentialHash,
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.CreateAsync(profile, cancellationToken);
        return new GuestResponse(profileId, credential);
    }

    public async Task<string?> ValidateCredentialAsync(string credential, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credential) || credential.Length != 64) return null;

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(credential.Trim()));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var guest = await repository.FindByCredentialHashAsync(hash, cancellationToken);
        return guest?.Id;
    }
}
