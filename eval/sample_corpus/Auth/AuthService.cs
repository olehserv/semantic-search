using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SampleShop.Data;

namespace SampleShop.Auth;

// Handles user authentication: verifying credentials at login and hashing
// passwords for storage. This is where the password check lives.
public class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    // Validates a username/password pair against the stored password hash.
    // Returns true when the credentials are correct.
    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        var user = await _db.Users.FindAsync(username);
        if (user is null)
        {
            return false;
        }

        var hashed = HashPassword(password, user.Salt);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hashed),
            Encoding.UTF8.GetBytes(user.PasswordHash));
    }

    // Derives a salted SHA-256 hash of the supplied password.
    private static string HashPassword(string password, string salt)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + password));
        return Convert.ToHexString(bytes);
    }
}
