using System;
using System.IO;
using System.Threading.Tasks;

namespace ModernTextViewer.src.Services
{
    public class FileService
    {
        public static async Task<string> LoadFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            try
            {
                return await File.ReadAllTextAsync(filePath);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error reading file: {ex.Message}", ex);
            }
        }

        public static async Task SaveFileAsync(string filePath, string content)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty");

            try
            {
                await File.WriteAllTextAsync(filePath, content);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error saving file: {ex.Message}", ex);
            }
        }
    }
}

