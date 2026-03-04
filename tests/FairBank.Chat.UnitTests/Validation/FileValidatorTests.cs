using FluentAssertions;
using FairBank.Chat.Application.Validation;

namespace FairBank.Chat.UnitTests.Validation;

public class FileValidatorTests
{
    // ── ValidateMagicBytes ──────────────────────────────────────────────────

    [Fact]
    public void ValidateMagicBytes_ValidJpeg_ShouldReturnTrue()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

        var result = FileValidator.ValidateMagicBytes(bytes, "image/jpeg");

        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateMagicBytes_ValidPng_ShouldReturnTrue()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };

        var result = FileValidator.ValidateMagicBytes(bytes, "image/png");

        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateMagicBytes_ValidGif_ShouldReturnTrue()
    {
        var bytes = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };

        var result = FileValidator.ValidateMagicBytes(bytes, "image/gif");

        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateMagicBytes_ValidPdf_ShouldReturnTrue()
    {
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 };

        var result = FileValidator.ValidateMagicBytes(bytes, "application/pdf");

        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateMagicBytes_UnknownContentType_ShouldReturnTrue()
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        var result = FileValidator.ValidateMagicBytes(bytes, "text/plain");

        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateMagicBytes_TooShortBytes_ShouldReturnFalse()
    {
        var bytes = new byte[] { 0xFF, 0xD8 };

        var result = FileValidator.ValidateMagicBytes(bytes, "image/jpeg");

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateMagicBytes_EmptyBytes_ShouldReturnFalse()
    {
        var bytes = Array.Empty<byte>();

        var result = FileValidator.ValidateMagicBytes(bytes, "image/png");

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateMagicBytes_WrongMagicBytesForJpeg_ShouldReturnFalse()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };

        var result = FileValidator.ValidateMagicBytes(bytes, "image/jpeg");

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateMagicBytes_WrongMagicBytesForPng_ShouldReturnFalse()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

        var result = FileValidator.ValidateMagicBytes(bytes, "image/png");

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateMagicBytes_WrongMagicBytesForGif_ShouldReturnFalse()
    {
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 };

        var result = FileValidator.ValidateMagicBytes(bytes, "image/gif");

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateMagicBytes_WrongMagicBytesForPdf_ShouldReturnFalse()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

        var result = FileValidator.ValidateMagicBytes(bytes, "application/pdf");

        result.Should().BeFalse();
    }

    // ── IsContentTypeAllowed ────────────────────────────────────────────────

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("application/msword")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void IsContentTypeAllowed_AllowedType_ShouldReturnTrue(string contentType)
    {
        var result = FileValidator.IsContentTypeAllowed(contentType);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/javascript")]
    [InlineData("image/svg+xml")]
    [InlineData("application/x-executable")]
    [InlineData("application/octet-stream")]
    public void IsContentTypeAllowed_DisallowedType_ShouldReturnFalse(string contentType)
    {
        var result = FileValidator.IsContentTypeAllowed(contentType);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("IMAGE/JPEG")]
    [InlineData("Image/Png")]
    [InlineData("APPLICATION/PDF")]
    public void IsContentTypeAllowed_CaseInsensitive_ShouldReturnTrue(string contentType)
    {
        var result = FileValidator.IsContentTypeAllowed(contentType);

        result.Should().BeTrue();
    }

    // ── IsExtensionAllowed ──────────────────────────────────────────────────

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".gif")]
    [InlineData(".webp")]
    [InlineData(".pdf")]
    [InlineData(".txt")]
    [InlineData(".doc")]
    [InlineData(".docx")]
    public void IsExtensionAllowed_AllowedExtension_ShouldReturnTrue(string extension)
    {
        var result = FileValidator.IsExtensionAllowed(extension);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".js")]
    [InlineData(".html")]
    [InlineData(".svg")]
    [InlineData(".bat")]
    [InlineData(".sh")]
    public void IsExtensionAllowed_DisallowedExtension_ShouldReturnFalse(string extension)
    {
        var result = FileValidator.IsExtensionAllowed(extension);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsExtensionAllowed_NullExtension_ShouldReturnFalse()
    {
        var result = FileValidator.IsExtensionAllowed(null);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsExtensionAllowed_EmptyExtension_ShouldReturnFalse()
    {
        var result = FileValidator.IsExtensionAllowed("");

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(".JPG")]
    [InlineData(".Png")]
    [InlineData(".PDF")]
    [InlineData(".DOCX")]
    public void IsExtensionAllowed_CaseInsensitive_ShouldReturnTrue(string extension)
    {
        var result = FileValidator.IsExtensionAllowed(extension);

        result.Should().BeTrue();
    }

    // ── IsFileSizeValid ─────────────────────────────────────────────────────

    [Fact]
    public void IsFileSizeValid_OneByte_ShouldReturnTrue()
    {
        var result = FileValidator.IsFileSizeValid(1);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsFileSizeValid_FiveMegabytes_ShouldReturnTrue()
    {
        var result = FileValidator.IsFileSizeValid(5 * 1024 * 1024);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsFileSizeValid_ExactlyTenMegabytes_ShouldReturnTrue()
    {
        var result = FileValidator.IsFileSizeValid(10 * 1024 * 1024);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsFileSizeValid_ZeroBytes_ShouldReturnFalse()
    {
        var result = FileValidator.IsFileSizeValid(0);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsFileSizeValid_TenMegabytesPlusOne_ShouldReturnFalse()
    {
        var result = FileValidator.IsFileSizeValid(10 * 1024 * 1024 + 1);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsFileSizeValid_NegativeSize_ShouldReturnFalse()
    {
        var result = FileValidator.IsFileSizeValid(-1);

        result.Should().BeFalse();
    }
}
