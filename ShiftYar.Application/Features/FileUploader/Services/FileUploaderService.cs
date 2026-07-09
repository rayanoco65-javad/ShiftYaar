using ShiftYar.Application.Interfaces.FileUploaderInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.FileUploader.Services
{
    public class FileUploaderService : IFileUploader
    {
        private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100 MB

        private static readonly Dictionary<string, string> MimeTypeExtensions = new Dictionary<string, string>
    {
        { "image/jpeg", ".jpg" },
        { "image/png", ".png" },
        { "image/gif", ".gif" },
        { "application/pdf", ".pdf" },
        { "video/mp4", ".mp4" },
        { "video/mpeg", ".mpeg" },
        { "video/quicktime", ".mov" },
        { "video/x-msvideo", ".avi" },
        { "video/x-ms-wmv", ".wmv" },
        { "text/plain", ".txt" },
        { "application/msword", ".doc" },
        { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx" },
        { "application/vnd.ms-excel", ".xls" },
        { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx" },
        { "application/zip", ".zip" },
        // Add more MIME types as needed
    };


        //public static string UploadFile(string fileBase64, string subfolder = "")
        public string UploadFile(string fileBase64, string subfolder = "")
        {
            if (string.IsNullOrEmpty(fileBase64))
            {
                return "/files/default.jpg";
            }

            try
            {
                var (mimeType, fileBytes) = ParseBase64String(fileBase64);

                if (fileBytes.Length > MaxFileSizeBytes)
                {
                    throw new Exception("File size exceeds the maximum allowed limit.");
                }

                string fileExtension = GetFileExtensionFromMimeType(mimeType);
                string fileName = Guid.NewGuid().ToString() + fileExtension;
                string mainFolder = GetMainFolderFromMimeType(mimeType);

                // If a subfolder is specified, add it to the path
                string fullFolder = string.IsNullOrEmpty(subfolder) ? mainFolder : Path.Combine(mainFolder, subfolder);

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", fullFolder, fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                File.WriteAllBytes(filePath, fileBytes);

                return $"/{fullFolder.Replace("\\", "/")}/{fileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading file: {ex.Message}");
                return "/files/default.jpg";
            }
        }


        //public static string UpdateFile(string newFileBase64, string previousFilePath, string subfolder = "")
        public string UpdateFile(string newFileBase64, string previousFilePath, string subfolder = "")
        {
            if (string.IsNullOrEmpty(newFileBase64))
            {
                DeleteFileFromServer(previousFilePath);
                return "/files/default.jpg";
            }

            try
            {
                var (mimeType, fileBytes) = ParseBase64String(newFileBase64);

                if (fileBytes.Length > MaxFileSizeBytes)
                {
                    throw new Exception("File size exceeds the maximum allowed limit.");
                }

                DeleteFileFromServer(previousFilePath);

                string fileExtension = GetFileExtensionFromMimeType(mimeType);
                string fileName = Guid.NewGuid().ToString() + fileExtension;
                string mainFolder = GetMainFolderFromMimeType(mimeType);

                // If a subfolder is specified, add it to the path
                string fullFolder = string.IsNullOrEmpty(subfolder) ? mainFolder : Path.Combine(mainFolder, subfolder);

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", fullFolder, fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                File.WriteAllBytes(filePath, fileBytes);

                return $"/{fullFolder.Replace("\\", "/")}/{fileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating file: {ex.Message}");
                return previousFilePath;
            }
        }


        //public static void DeleteFileFromServer(string filePath)
        public void DeleteFileFromServer(string filePath)
        {
            if (filePath != "/files/default.jpg" && !string.IsNullOrEmpty(filePath))
            {
                try
                {
                    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath.TrimStart('/'));
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting file: {ex.Message}");
                }
            }
        }


        private static (string mimeType, byte[] fileBytes) ParseBase64String(string fileBase64)
        {
            var parts = fileBase64.Split(',');
            string mimeType = parts[0].Split(':')[1].Split(';')[0];
            string base64Data = parts[1];
            byte[] fileBytes = Convert.FromBase64String(base64Data);
            return (mimeType, fileBytes);
        }


        private static string GetFileExtensionFromMimeType(string mimeType)
        {
            if (MimeTypeExtensions.TryGetValue(mimeType.ToLower(), out string extension))
            {
                return extension;
            }
            return ".bin"; // Default extension for unknown types
        }


        private static string GetMainFolderFromMimeType(string mimeType)
        {
            if (mimeType.StartsWith("image/"))
                return "images";
            else if (mimeType.StartsWith("video/"))
                return "videos";
            else if (mimeType == "application/pdf" || mimeType.Contains("word") || mimeType.Contains("excel") || mimeType == "text/plain")
                return "documents";
            else if (mimeType == "application/zip")
                return "archives";
            else
                return "files"; // Default folder for other types
        }

        public void DeleteFileFomServer(string filePath)
        {
            throw new NotImplementedException();
        }
    }
}
