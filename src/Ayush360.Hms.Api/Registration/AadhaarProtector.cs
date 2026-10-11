using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Ayush360.Hms.Api.Registration;

/// <summary>"Aadhaar" configuration section. Both keys are secrets: Key Vault on Azure, never in Git.</summary>
public sealed class AadhaarOptions
{
    public const string SectionName = "Aadhaar";

    /// <summary>AES-256 key (base64, 32 bytes) that encrypts the full number. Key Vault secret "Aadhaar--EncryptionKey".</summary>
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>HMAC key (base64, 32 bytes) for the search fingerprint. Key Vault secret "Aadhaar--HashKey".</summary>
    public string HashKey { get; set; } = string.Empty;
}

/// <summary>What gets stored for a patient's Aadhaar. The plain number itself is never stored.</summary>
public sealed record ProtectedAadhaar(string Encrypted, string Hash, string Last4);

/// <summary>
/// Aadhaar handling (UIDAI rules: store encrypted, show masked):
///   - Encrypted: AES-256-GCM, random nonce per value ("v1." + base64(nonce | ciphertext | tag)).
///     Only needed later for ABHA / PM-JAY; nothing in the app decrypts it today.
///   - Hash: HMAC-SHA256 fingerprint so the desk can search by the full number and duplicates are caught,
///     without being able to work the number back out of the database.
///   - Last4: for the masked display "XXXX XXXX 1234".
/// Losing the keys makes stored numbers unreadable, so production keys must be backed up (Key Vault does this).
/// Never log an Aadhaar number.
/// </summary>
public sealed class AadhaarProtector
{
    private const string Version = "v1.";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _encryptionKey;
    private readonly byte[] _hashKey;

    public AadhaarProtector(IOptions<AadhaarOptions> options, IHostEnvironment environment, ILogger<AadhaarProtector> logger)
    {
        var keys = options.Value;
        if (string.IsNullOrWhiteSpace(keys.EncryptionKey) || string.IsNullOrWhiteSpace(keys.HashKey))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Aadhaar:EncryptionKey and Aadhaar:HashKey are missing. Store two random 32-byte keys (base64) in Key Vault " +
                    "as 'Aadhaar--EncryptionKey' and 'Aadhaar--HashKey'. Back them up: without them stored numbers can't be read.");
            }

            keys = LoadOrCreateDevelopmentKeys(logger);
        }

        _encryptionKey = ReadKey(keys.EncryptionKey, "Aadhaar:EncryptionKey");
        _hashKey = ReadKey(keys.HashKey, "Aadhaar:HashKey");
    }

    /// <summary>Digits only: "1234 5678 9012" -> "123456789012".</summary>
    public static string Normalize(string? input) => new((input ?? "").Where(char.IsDigit).ToArray());

    public static string Mask(string? last4) => string.IsNullOrEmpty(last4) ? "" : $"XXXX XXXX {last4}";

    public ProtectedAadhaar Protect(string digits)
    {
        if (!IsValid(digits))
        {
            throw new ArgumentException("Not a valid Aadhaar number.", nameof(digits));
        }

        var plain = Encoding.ASCII.GetBytes(digits);
        var output = new byte[NonceSize + plain.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using (var aes = new AesGcm(_encryptionKey, TagSize))
        {
            aes.Encrypt(nonce, plain, output.AsSpan(NonceSize, plain.Length), output.AsSpan(NonceSize + plain.Length));
        }

        return new ProtectedAadhaar(Version + Convert.ToBase64String(output), Hash(digits), digits[^4..]);
    }

    /// <summary>Search / duplicate fingerprint (same number -> same hash, within this deployment's key).</summary>
    public string Hash(string digits) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_hashKey, Encoding.ASCII.GetBytes(digits)));

    /// <summary>The full number. Only for future ABHA / insurance integrations — never for screens or logs.</summary>
    public string Reveal(string encrypted)
    {
        if (!encrypted.StartsWith(Version, StringComparison.Ordinal))
        {
            throw new CryptographicException("Unknown Aadhaar encryption format.");
        }

        var data = Convert.FromBase64String(encrypted[Version.Length..]);
        var plainLength = data.Length - NonceSize - TagSize;
        var plain = new byte[plainLength];
        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Decrypt(data.AsSpan(0, NonceSize), data.AsSpan(NonceSize, plainLength), data.AsSpan(NonceSize + plainLength), plain);
        return Encoding.ASCII.GetString(plain);
    }

    /// <summary>12 digits, not starting with 0 or 1, valid Verhoeff check digit (same rule as the React form).</summary>
    public static bool IsValid(string digits)
    {
        if (digits.Length != 12 || digits[0] is '0' or '1' || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var c = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            c = VerhoeffD[c, VerhoeffP[i % 8, digits[digits.Length - 1 - i] - '0']];
        }

        return c == 0;
    }

    private static readonly int[,] VerhoeffD =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 2, 3, 4, 0, 6, 7, 8, 9, 5 }, { 2, 3, 4, 0, 1, 7, 8, 9, 5, 6 },
        { 3, 4, 0, 1, 2, 8, 9, 5, 6, 7 }, { 4, 0, 1, 2, 3, 9, 5, 6, 7, 8 }, { 5, 9, 8, 7, 6, 0, 4, 3, 2, 1 },
        { 6, 5, 9, 8, 7, 1, 0, 4, 3, 2 }, { 7, 6, 5, 9, 8, 2, 1, 0, 4, 3 }, { 8, 7, 6, 5, 9, 3, 2, 1, 0, 4 },
        { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 },
    };

    private static readonly int[,] VerhoeffP =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 5, 7, 6, 2, 8, 3, 0, 9, 4 }, { 5, 8, 0, 3, 7, 9, 6, 1, 4, 2 },
        { 8, 9, 1, 6, 0, 4, 3, 5, 2, 7 }, { 9, 4, 5, 3, 1, 2, 6, 8, 7, 0 }, { 4, 2, 8, 6, 5, 7, 3, 9, 0, 1 },
        { 2, 7, 9, 3, 8, 0, 6, 4, 1, 5 }, { 7, 0, 4, 6, 9, 1, 3, 2, 5, 8 },
    };

    private static byte[] ReadKey(string base64, string name)
    {
        var key = Convert.FromBase64String(base64);
        if (key.Length != 32)
        {
            throw new InvalidOperationException($"{name} must be 32 bytes (base64).");
        }

        return key;
    }

    /// <summary>Development only: one key pair per developer PC, outside the repository, created on first run.</summary>
    private static AadhaarOptions LoadOrCreateDevelopmentKeys(ILogger logger)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ayush360", "hms-dev-aadhaar-keys.json");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var created = new AadhaarOptions
            {
                EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                HashKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            };
            File.WriteAllText(path, JsonSerializer.Serialize(created));
            logger.LogWarning("Created development Aadhaar keys at {KeyPath}", path);
        }

        return JsonSerializer.Deserialize<AadhaarOptions>(File.ReadAllText(path))
               ?? throw new InvalidOperationException($"Could not read {path}.");
    }
}
