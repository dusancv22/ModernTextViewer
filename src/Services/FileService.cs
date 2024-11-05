using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernTextViewer.src.Services
{
    public class FileService
    {
        public async Task<string> LoadFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty");

            return await File.ReadAllTextAsync(filePath);
        }

        public async Task SaveFileAsync(string filePath, string content)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty");

            await File.WriteAllTextAsync(filePath, content);
        }
    }
}

