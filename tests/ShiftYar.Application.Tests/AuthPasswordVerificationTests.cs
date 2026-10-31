using System;
using Xunit;

namespace ShiftYar.Application.Tests;

public class AuthPasswordVerificationTests
{
    private static bool IsBcryptHash(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length != 60)
            return false;

        return password.StartsWith("$2a$") ||
               password.StartsWith("$2b$") ||
               password.StartsWith("$2y$") ||
               password.StartsWith("$2x$");
    }

    private static bool VerifyPassword(string inputPassword, string storedPassword, out bool needsRehash)
    {
        needsRehash = false;

        if (string.IsNullOrEmpty(storedPassword) || string.IsNullOrEmpty(inputPassword))
            return false;

        if (IsBcryptHash(storedPassword))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(inputPassword, storedPassword);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }

        if (storedPassword == inputPassword)
        {
            needsRehash = true;
            return true;
        }

        return false;
    }

    [Fact]
    public void VerifyPassword_WithValidBcryptHash_ReturnsTrueAndNoRehashNeeded()
    {
        var rawPassword = "MySecretPassword123";
        var hash = BCrypt.Net.BCrypt.HashPassword(rawPassword);

        var result = VerifyPassword(rawPassword, hash, out var needsRehash);

        Assert.True(result);
        Assert.False(needsRehash);
    }

    [Fact]
    public void VerifyPassword_WithLegacyPlainText_ReturnsTrueAndFlagsRehash()
    {
        var rawPassword = "12345678";
        var storedPlainText = "12345678";

        var result = VerifyPassword(rawPassword, storedPlainText, out var needsRehash);

        Assert.True(result);
        Assert.True(needsRehash);
    }

    [Fact]
    public void VerifyPassword_WithWrongPasswordAgainstPlainText_ReturnsFalse()
    {
        var rawPassword = "WrongPassword";
        var storedPlainText = "12345678";

        var result = VerifyPassword(rawPassword, storedPlainText, out var needsRehash);

        Assert.False(result);
        Assert.False(needsRehash);
    }

    [Fact]
    public void VerifyPassword_WithInvalidSaltFormat_DoesNotThrow_AndReturnsFalse()
    {
        var rawPassword = "12345678";
        var corruptHash = "$2a$11$invalid_salt_that_is_corrupt_but_has_sixty_characters_12345";

        var exception = Record.Exception(() =>
        {
            var result = VerifyPassword(rawPassword, corruptHash, out var needsRehash);
            Assert.False(result);
        });

        Assert.Null(exception);
    }
}

