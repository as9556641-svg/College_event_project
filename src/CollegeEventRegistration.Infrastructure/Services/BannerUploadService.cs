using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

public class BannerUploadService : IBannerUploadService
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/pjpeg",
        "image/png",
        "image/webp"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<BannerUploadService> _logger;

    public BannerUploadService(IWebHostEnvironment environment, ILogger<BannerUploadService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<(bool Succeeded, string? FilePath, string? ErrorMessage)> UploadBannerAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileLength)
    {
        if (fileStream == null || fileLength <= 0)
        {
            return (false, null, "No image file provided.");
        }

        if (fileLength > MaxFileSize)
        {
            return (false, null, $"Image file size ({fileLength / 1024 / 1024} MB) exceeds maximum allowed limit of 5 MB.");
        }

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            return (false, null, $"Invalid file extension '{ext}'. Only JPG, PNG, and WEBP formats are permitted.");
        }

        if (!AllowedMimeTypes.Contains(contentType))
        {
            return (false, null, $"Invalid image MIME type '{contentType}'.");
        }

        // Validate image magic bytes to prevent masqueraded executables
        byte[] headerBytes = new byte[12];
        int bytesRead = await fileStream.ReadAsync(headerBytes, 0, headerBytes.Length);
        fileStream.Position = 0; // Reset stream

        if (!IsValidImageHeader(headerBytes, ext))
        {
            return (false, null, "The uploaded file does not match a valid image signature.");
        }

        try
        {
            var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsDir = Path.Combine(webRoot, "uploads", "banners");

            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            // Generate completely safe unique file name (prevents path traversal & overwrites)
            var safeFileName = $"{Guid.NewGuid():N}{ext}";
            var destinationPath = Path.Combine(uploadsDir, safeFileName);

            // Double check destination stays strictly inside uploadsDir
            var fullDest = Path.GetFullPath(destinationPath);
            var fullUploads = Path.GetFullPath(uploadsDir);
            if (!fullDest.StartsWith(fullUploads, StringComparison.OrdinalIgnoreCase))
            {
                return (false, null, "Path traversal sequence detected and blocked.");
            }

            using (var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await fileStream.CopyToAsync(destinationStream);
            }

            var relativePath = $"/uploads/banners/{safeFileName}";
            _logger.LogInformation("Successfully uploaded secure event banner: {Path}", relativePath);
            return (true, relativePath, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store uploaded event banner image.");
            return (false, null, "An internal error occurred while saving the event banner.");
        }
    }

    public Task<bool> DeleteBannerAsync(string? relativeFilePath)
    {
        if (string.IsNullOrWhiteSpace(relativeFilePath))
            return Task.FromResult(false);

        try
        {
            if (!relativeFilePath.StartsWith("/uploads/banners/", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(false);

            var fileName = Path.GetFileName(relativeFilePath);
            var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var fullPath = Path.Combine(webRoot, "uploads", "banners", fileName);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted banner file: {Path}", fullPath);
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete old banner file: {Path}", relativeFilePath);
        }

        return Task.FromResult(false);
    }

    private static bool IsValidImageHeader(byte[] bytes, string ext)
    {
        if (bytes.Length < 4) return false;

        // JPEG: FF D8 FF
        if ((ext == ".jpg" || ext == ".jpeg") && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return true;

        // PNG: 89 50 4E 47
        if (ext == ".png" && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return true;

        // WEBP: RIFF .... WEBP
        if (ext == ".webp" && bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && // RIFF
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) // WEBP
            return true;

        return false;
    }
}

