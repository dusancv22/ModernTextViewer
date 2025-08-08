using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using ModernTextViewer.src.Models;
using System.Collections.Generic;
using System.Buffers;
using System.Threading;

namespace ModernTextViewer.src.Services
{
    public class FileService
    {
        private const int BUFFER_SIZE = 65536; // 64KB buffer for optimal I/O
        private const int PROGRESS_THRESHOLD = 1024 * 1024; // 1MB threshold for progress reporting
        
        public static async Task<(string content, List<HyperlinkModel> hyperlinks)> LoadFileAsync(string filePath, 
            IProgress<(int bytesRead, long totalBytes)>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            try
            {
                var fileInfo = new FileInfo(filePath);
                long totalBytes = fileInfo.Length;
                
                // Use optimized loading based on file size
                string content;
                if (totalBytes < PROGRESS_THRESHOLD)
                {
                    content = await LoadSmallFileOptimizedAsync(filePath, cancellationToken);
                }
                else
                {
                    content = await LoadLargeFileOptimizedAsync(filePath, totalBytes, progress, cancellationToken);
                }

                // Extract hyperlinks if present using optimized method
                var (cleanContent, hyperlinks) = ExtractHyperlinkMetadataOptimized(content);

                // Optimize line ending normalization using StringBuilder
                var normalizedContent = NormalizeLineEndingsOptimized(cleanContent);
                
                return (normalizedContent, hyperlinks);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error reading file: {ex.Message}", ex);
            }
        }

        public static async Task SaveFileAsync(string filePath, string content, List<HyperlinkModel>? hyperlinks = null, 
            IProgress<int>? progress = null, CancellationToken cancellationToken = default)
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

                // Use optimized method for line ending normalization and writing
                await SaveContentOptimizedAsync(filePath, content, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error saving file: {ex.Message}", ex);
            }
        }
        
        // Optimized methods for performance
        private static async Task<string> LoadSmallFileOptimizedAsync(string filePath, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(filePath, new UTF8Encoding(false), true, BUFFER_SIZE);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }
        
        private static async Task<string> LoadLargeFileOptimizedAsync(string filePath, long totalBytes, 
            IProgress<(int bytesRead, long totalBytes)>? progress, CancellationToken cancellationToken)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BUFFER_SIZE, useAsync: true);
            using var reader = new StreamReader(fileStream, new UTF8Encoding(false), true, BUFFER_SIZE);
            
            var stringBuilder = new StringBuilder((int)Math.Min(totalBytes, int.MaxValue));
            var buffer = ArrayPool<char>.Shared.Rent(BUFFER_SIZE);
            
            try
            {
                int totalCharsRead = 0;
                int charsRead;
                
                while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    stringBuilder.Append(buffer, 0, charsRead);
                    totalCharsRead += charsRead;
                    
                    // Report progress periodically
                    if (progress != null && totalCharsRead % (BUFFER_SIZE * 4) == 0)
                    {
                        // Estimate bytes read (rough approximation for UTF-8)
                        int estimatedBytesRead = totalCharsRead;
                        progress.Report((estimatedBytesRead, totalBytes));
                    }
                }
                
                progress?.Report(((int)totalBytes, totalBytes)); // Final progress
                return stringBuilder.ToString();
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
        
        private static (string content, List<HyperlinkModel> hyperlinks) ExtractHyperlinkMetadataOptimized(string content)
        {
            if (string.IsNullOrEmpty(content))
                return (content, new List<HyperlinkModel>());

            const string metadataStart = "<!--HYPERLINKS:";
            const string metadataEnd = "-->";
            
            // Use ReadOnlySpan for efficient string searching
            ReadOnlySpan<char> contentSpan = content.AsSpan();
            int metadataStartIndex = contentSpan.LastIndexOf(metadataStart.AsSpan());
            
            if (metadataStartIndex == -1)
                return (content, new List<HyperlinkModel>());

            int metadataEndIndex = contentSpan.Slice(metadataStartIndex).IndexOf(metadataEnd.AsSpan());
            if (metadataEndIndex == -1)
                return (content, new List<HyperlinkModel>());
                
            metadataEndIndex += metadataStartIndex;

            // Extract clean content efficiently
            var cleanContent = content.Substring(0, metadataStartIndex).TrimEnd();
            var metadataJson = content.Substring(metadataStartIndex + metadataStart.Length, 
                metadataEndIndex - metadataStartIndex - metadataStart.Length);

            try
            {
                var hyperlinks = HyperlinkService.DeserializeHyperlinks(metadataJson);
                return (cleanContent, hyperlinks);
            }
            catch
            {
                return (content, new List<HyperlinkModel>());
            }
        }
        
        private static string NormalizeLineEndingsOptimized(ReadOnlySpan<char> content)
        {
            if (content.IsEmpty)
                return string.Empty;
                
            // Pre-allocate StringBuilder with estimated capacity
            var result = new StringBuilder(content.Length + (content.Length / 50)); // Rough estimate for line ending expansion
            
            for (int i = 0; i < content.Length; i++)
            {
                char current = content[i];
                
                if (current == '\r')
                {
                    // Handle \r\n or standalone \r
                    if (i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        // Skip \r, the \n will be processed next
                        continue;
                    }
                    else
                    {
                        // Standalone \r - convert to system line ending
                        result.Append(Environment.NewLine);
                    }
                }
                else if (current == '\n')
                {
                    // \n (either standalone or part of \r\n) - convert to system line ending
                    result.Append(Environment.NewLine);
                }
                else
                {
                    result.Append(current);
                }
            }
            
            return result.ToString();
        }
        
        private static string NormalizeLineEndingsOptimized(string content)
        {
            return NormalizeLineEndingsOptimized(content.AsSpan());
        }
        
        private static async Task SaveContentOptimizedAsync(string filePath, string content, 
            IProgress<int>? progress, CancellationToken cancellationToken)
        {
            // Normalize line endings to Windows format efficiently
            var normalizedContent = NormalizeLineEndingsToWindowsOptimized(content);
            
            // Ensure content ends with line ending
            if (!normalizedContent.EndsWith("\r\n"))
            {
                normalizedContent += "\r\n";
            }
            
            // Use optimized writing for large content
            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(false), BUFFER_SIZE);
            
            if (normalizedContent.Length > PROGRESS_THRESHOLD)
            {
                await WriteWithProgressAsync(writer, normalizedContent, progress, cancellationToken);
            }
            else
            {
                await writer.WriteAsync(normalizedContent).ConfigureAwait(false);
                progress?.Report(100);
            }
        }
        
        private static string NormalizeLineEndingsToWindowsOptimized(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;
                
            ReadOnlySpan<char> span = content.AsSpan();
            var result = new StringBuilder(content.Length + (content.Length / 50));
            
            for (int i = 0; i < span.Length; i++)
            {
                char current = span[i];
                
                if (current == '\r')
                {
                    if (i + 1 < span.Length && span[i + 1] == '\n')
                    {
                        // Keep \r\n as is
                        result.Append("\r\n");
                        i++; // Skip the \n
                    }
                    else
                    {
                        // Convert standalone \r to \r\n
                        result.Append("\r\n");
                    }
                }
                else if (current == '\n')
                {
                    // Convert standalone \n to \r\n
                    result.Append("\r\n");
                }
                else
                {
                    result.Append(current);
                }
            }
            
            return result.ToString();
        }
        
        private static async Task WriteWithProgressAsync(StreamWriter writer, string content, 
            IProgress<int>? progress, CancellationToken cancellationToken)
        {
            const int chunkSize = BUFFER_SIZE;
            int totalLength = content.Length;
            int written = 0;
            
            for (int i = 0; i < totalLength; i += chunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                int currentChunkSize = Math.Min(chunkSize, totalLength - i);
                await writer.WriteAsync(content.AsMemory(i, currentChunkSize)).ConfigureAwait(false);
                
                written += currentChunkSize;
                progress?.Report((int)((double)written / totalLength * 100));
            }
        }
    }
}

