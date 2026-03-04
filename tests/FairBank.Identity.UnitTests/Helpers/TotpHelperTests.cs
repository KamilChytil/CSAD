using System.Text.RegularExpressions;
using FluentAssertions;
using FairBank.Identity.Application.Helpers;

namespace FairBank.Identity.UnitTests.Helpers;

public class TotpHelperTests
{
    [Fact]
    public void GenerateSecret_ShouldReturnBase32String()
    {
        // Act
        var secret = TotpHelper.GenerateSecret();

        // Assert
        secret.Should().NotBeNullOrWhiteSpace();
        Regex.IsMatch(secret, "^[A-Z2-7]+$").Should().BeTrue(
            "secret should be a valid Base32-encoded string");
    }

    [Fact]
    public void GenerateSecret_ShouldGenerateUniqueSecrets()
    {
        // Act
        var secret1 = TotpHelper.GenerateSecret();
        var secret2 = TotpHelper.GenerateSecret();

        // Assert
        secret1.Should().NotBe(secret2);
    }

    [Fact]
    public void VerifyCode_WithNullCode_ShouldReturnFalse()
    {
        // Arrange
        var secret = TotpHelper.GenerateSecret();

        // Act
        var result = TotpHelper.VerifyCode(secret, null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyCode_WithWrongLengthCode_ShouldReturnFalse()
    {
        // Arrange
        var secret = TotpHelper.GenerateSecret();

        // Act
        var result = TotpHelper.VerifyCode(secret, "12345"); // 5 digits instead of 6

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetOtpAuthUri_ShouldContainSecretAndIssuer()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var email = "user@example.com";

        // Act
        var uri = TotpHelper.GetOtpAuthUri(secret, email);

        // Assert
        uri.Should().Contain($"secret={secret}");
        uri.Should().Contain("issuer=FairBank");
        uri.Should().StartWith("otpauth://totp/");
    }

    [Fact]
    public void GetOtpAuthUri_ShouldEncodeEmail()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var email = "user+test@example.com";

        // Act
        var uri = TotpHelper.GetOtpAuthUri(secret, email);

        // Assert
        uri.Should().Contain(Uri.EscapeDataString(email));
        uri.Should().NotContain("user+test@example.com");
    }

    [Fact]
    public void GenerateBackupCodes_ShouldReturn8CodesByDefault()
    {
        // Act
        var codes = TotpHelper.GenerateBackupCodes();

        // Assert
        codes.Should().HaveCount(8);
    }

    [Fact]
    public void GenerateBackupCodes_ShouldReturnRequestedCount()
    {
        // Act
        var codes = TotpHelper.GenerateBackupCodes(5);

        // Assert
        codes.Should().HaveCount(5);
    }

    [Fact]
    public void GenerateBackupCodes_ShouldReturn8DigitCodes()
    {
        // Act
        var codes = TotpHelper.GenerateBackupCodes();

        // Assert
        foreach (var code in codes)
        {
            code.Should().HaveLength(8);
            code.Should().MatchRegex("^[0-9]{8}$");
        }
    }

    [Fact]
    public void Base32RoundTrip_ShouldPreserveData()
    {
        // We test the Base32 round-trip indirectly:
        // GenerateSecret encodes random bytes as Base32, then VerifyCode
        // decodes that Base32 string back to bytes. If both operations are
        // correct, VerifyCode will compute the right TOTP and return true
        // for a code we compute ourselves using the same secret.

        // Arrange
        var secret = TotpHelper.GenerateSecret();

        // Generate a valid code by computing one ourselves using the same
        // secret. TotpHelper.VerifyCode will decode the secret from Base32
        // and compute its own code, then compare. If the Base32 round-trip
        // is broken, it will never match.

        // We use VerifyCode with the code generated from the same secret
        // at the current time step. We need to generate the code the same
        // way the helper does internally. Since GenerateCode is private,
        // we use reflection.
        var generateCodeMethod = typeof(TotpHelper).GetMethod(
            "GenerateCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var base32DecodeMethod = typeof(TotpHelper).GetMethod(
            "Base32Decode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        generateCodeMethod.Should().NotBeNull("GenerateCode method should exist");
        base32DecodeMethod.Should().NotBeNull("Base32Decode method should exist");

        var secretBytes = (byte[])base32DecodeMethod!.Invoke(null, [secret])!;
        var timeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var code = (string)generateCodeMethod!.Invoke(null, [secretBytes, timeStep])!;

        // Act
        var result = TotpHelper.VerifyCode(secret, code);

        // Assert
        result.Should().BeTrue("a code generated from the same secret at current time should verify");
    }
}
