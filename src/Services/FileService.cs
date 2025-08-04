using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using ModernTextViewer.src.Models;
using System.Collections.Generic;

namespace ModernTextViewer.src.Services
{
    public class FileService
    {
        public static async Task<(string content, List<HyperlinkModel> hyperlinks)> LoadFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            try
            {
                // Read file content
                string content;
                using (var reader = new StreamReader(filePath, new UTF8Encoding(false)))
                {
                    content = await reader.ReadToEndAsync();
                }

                // Extract hyperlinks if present
                var (cleanContent, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(content);

                // Normalize line endings to system default
                cleanContent = cleanContent.Replace("\r\n", "\n").Replace("\r", "\n");
                string[] lines = cleanContent.Split('\n');
                return (string.Join(Environment.NewLine, lines), hyperlinks);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error reading file: {ex.Message}", ex);
            }
        }

        public static async Task SaveFileAsync(string filePath, string content, List<HyperlinkModel>? hyperlinks = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty");

            try
            {
                // Add hyperlink metadata if present
                if (hyperlinks != null && hyperlinks.Count > 0)
                {
                    content = HyperlinkService.AddHyperlinkMetadata(content, hyperlinks);
                }

                // Split content into lines and rejoin with Windows line endings
                string[] lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                content = string.Join("\r\n", lines);

                // Ensure the content ends with a single line ending
                if (!content.EndsWith("\r\n"))
                {
                    content += "\r\n";
                }

                // Write content using StreamWriter to ensure proper line endings
                using (var writer = new StreamWriter(filePath, false, new UTF8Encoding(false)))
                {
                    await writer.WriteAsync(content);
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Error saving file: {ex.Message}", ex);
            }
        }
    }
}

