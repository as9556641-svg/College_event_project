namespace CollegeEventRegistration.Core.Interfaces;

public interface IBannerUploadService
{
    Task<(bool Succeeded, string? FilePath, string? ErrorMessage)> UploadBannerAsync(Stream fileStream, string originalFileName, string contentType, long fileLength);
    Task<bool> DeleteBannerAsync(string? relativeFilePath);
}

