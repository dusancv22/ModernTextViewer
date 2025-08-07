using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ModernTextViewer.src.Models;

namespace ModernTextViewer.src.Services
{
    public class StreamingTextProcessor : IDisposable
    {
        private const int DEFAULT_CHUNK_SIZE = 8192; // 8KB chunks
        private const int LARGE_FILE_THRESHOLD = 50 * 1024 * 1024; // 50MB
        private const int MAX_MEMORY_USAGE = 200 * 1024 * 1024; // 200MB max memory
        private const int TEXT_CACHE_SIZE = 10; // Cache 10 recent text segments
        
        private readonly Dictionary<long, TextSegment> textCache = new();
        private readonly Queue<long> cacheAccessOrder = new();
        private readonly object cacheLock = new();
        private long currentFileSize;
        private string currentFilePath = string.Empty;
        private bool disposedValue;

        public event EventHandler<ProgressEventArgs>? ProgressChanged;
        public event EventHandler<TextSegmentLoadedEventArgs>? TextSegmentLoaded;

        public class TextSegment
        {
            public long StartPosition { get; set; }
            public long Length { get; set; }
            public string Content { get; set; } = string.Empty;
            public List<HyperlinkModel> Hyperlinks { get; set; } = new();
            public DateTime LastAccessed { get; set; } = DateTime.Now;
        }

        public class ProgressEventArgs : EventArgs
        {
            public long ProcessedBytes { get; set; }
            public long TotalBytes { get; set; }
            public int PercentComplete => TotalBytes > 0 ? (int)((ProcessedBytes * 100) / TotalBytes) : 0;
            public string? CurrentOperation { get; set; }
        }

        public class TextSegmentLoadedEventArgs : EventArgs
        {
            public TextSegment Segment { get; set; } = null!;
        }

        public bool IsLargeFile(string filePath)
        {
            return ErrorManager.ExecuteWithErrorHandling(() =>
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Validation, ErrorManager.ErrorSeverity.Warning, 
                        "Cannot check if file is large: path is empty");
                    return false;
                }

                if (!File.Exists(filePath))
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Warning, 
                        $"Cannot check file size: file does not exist - {filePath}");
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                var isLarge = fileInfo.Length > LARGE_FILE_THRESHOLD;
                
                if (isLarge)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, ErrorManager.ErrorSeverity.Info, 
                        $"Large file detected: {fileInfo.Length / 1024 / 1024}MB (threshold: {LARGE_FILE_THRESHOLD / 1024 / 1024}MB)", 
                        null, filePath);
                }
                
                return isLarge;
            }, 
            ErrorManager.ErrorCategory.FileIO, 
            "Check if file is large", 
            fallbackAction: () => ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Info, 
                "Using fallback: assuming file is not large"));
        }

        public async Task<StreamingFileInfo> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                // Input validation
                if (string.IsNullOrEmpty(filePath))
                {
                    var ex = new ArgumentException("File path cannot be empty");
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Validation, ErrorManager.ErrorSeverity.Error, 
                        "Invalid file path for analysis", ex, "AnalyzeFileAsync");
                    throw ex;
                }

                if (!File.Exists(filePath))
                {
                    var ex = new FileNotFoundException("File not found for analysis", filePath);
                    ErrorManager.LogFileError("analyze", filePath, ex);
                    throw ex;
                }

                var fileInfo = new FileInfo(filePath);
                currentFileSize = fileInfo.Length;
                currentFilePath = filePath;

                // Check if file size exceeds safe limits
                if (fileInfo.Length > MAX_MEMORY_USAGE * 2) // 400MB limit
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Warning, 
                        $"File size ({fileInfo.Length / 1024 / 1024}MB) may cause memory issues", null, filePath);
                }

                var estimatedLines = await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await EstimateLineCountAsync(filePath, cancellationToken);
                },
                ErrorManager.ErrorCategory.FileIO,
                "Estimate line count",
                0L);

                var info = new StreamingFileInfo
                {
                    FilePath = filePath,
                    FileSize = fileInfo.Length,
                    IsLargeFile = fileInfo.Length > LARGE_FILE_THRESHOLD,
                    EstimatedLineCount = estimatedLines,
                    RequiresStreaming = fileInfo.Length > LARGE_FILE_THRESHOLD
                };

                ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Info, 
                    $"File analysis complete: {info.FileSize / 1024 / 1024}MB, ~{info.EstimatedLineCount} lines, Streaming: {info.RequiresStreaming}", 
                    null, filePath);

                return info;
            },
            ErrorManager.ErrorCategory.FileIO,
            "Analyze file for streaming",
            new StreamingFileInfo { FilePath = filePath, RequiresStreaming = false });
        }

        public async Task<TextSegment> LoadTextSegmentAsync(long startPosition, long length, CancellationToken cancellationToken = default)
        {
            return await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                // Validate input parameters
                if (startPosition < 0)
                {
                    var ex = new ArgumentOutOfRangeException(nameof(startPosition), "Start position cannot be negative");
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Validation, ErrorManager.ErrorSeverity.Error, 
                        "Invalid start position for segment load", ex);
                    throw ex;
                }

                if (length <= 0)
                {
                    var ex = new ArgumentOutOfRangeException(nameof(length), "Length must be positive");
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Validation, ErrorManager.ErrorSeverity.Error, 
                        "Invalid length for segment load", ex);
                    throw ex;
                }

                if (length > MAX_MEMORY_USAGE)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Warning, 
                        $"Requested segment size ({length / 1024 / 1024}MB) exceeds safe limit");
                }

                var segmentKey = (startPosition / DEFAULT_CHUNK_SIZE) * DEFAULT_CHUNK_SIZE;

                // Check cache first
                lock (cacheLock)
                {
                    if (textCache.TryGetValue(segmentKey, out var cachedSegment))
                    {
                        cachedSegment.LastAccessed = DateTime.Now;
                        UpdateCacheAccessOrder(segmentKey);
                        
                        ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, ErrorManager.ErrorSeverity.Info, 
                            $"Cache hit for segment at position {startPosition}");
                        
                        return cachedSegment;
                    }
                }

                // Load from file with error recovery
                var segment = await ErrorRecovery.RecoverMemoryOperationAsync(
                    async (ct) => await LoadSegmentFromFileAsync(startPosition, length, ct),
                    async (ct) => 
                    {
                        // Fallback: load smaller segment
                        var fallbackLength = Math.Min(length, DEFAULT_CHUNK_SIZE);
                        return await LoadSegmentFromFileAsync(startPosition, fallbackLength, ct);
                    },
                    "Load text segment",
                    cancellationToken);

                if (!segment.Success)
                {
                    var ex = new IOException($"Failed to load text segment at position {startPosition}: {segment.ErrorMessage}");
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Error, 
                        "Text segment load failed", ex, currentFilePath);
                    throw ex;
                }

                var loadedSegment = segment.Result as TextSegment;
                if (loadedSegment == null)
                {
                    throw new InvalidOperationException("Failed to cast segment result");
                }
                
                // Add to cache with memory monitoring
                lock (cacheLock)
                {
                    try
                    {
                        textCache[segmentKey] = loadedSegment;
                        cacheAccessOrder.Enqueue(segmentKey);
                        
                        // Remove oldest entries if cache is full
                        while (cacheAccessOrder.Count > TEXT_CACHE_SIZE)
                        {
                            var oldestKey = cacheAccessOrder.Dequeue();
                            textCache.Remove(oldestKey);
                        }
                    }
                    catch (OutOfMemoryException ex)
                    {
                        ErrorManager.LogMemoryError("cache text segment", ex, GC.GetTotalMemory(false));
                        
                        // Clear cache and try again
                        textCache.Clear();
                        cacheAccessOrder.Clear();
                        
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        
                        // Don't cache this segment due to memory pressure
                        ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Warning, 
                            "Skipping cache due to memory pressure");
                    }
                }

                TextSegmentLoaded?.Invoke(this, new TextSegmentLoadedEventArgs { Segment = loadedSegment });
                return loadedSegment;
            },
            ErrorManager.ErrorCategory.FileIO,
            "Load text segment",
            new TextSegment { StartPosition = startPosition, Length = 0, Content = string.Empty });
        }

        private async Task<TextSegment> LoadSegmentFromFileAsync(long startPosition, long length, CancellationToken cancellationToken)
        {
            return await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                var segment = new TextSegment
                {
                    StartPosition = startPosition,
                    Length = length
                };

                // Validate file state
                if (string.IsNullOrEmpty(currentFilePath))
                {
                    var ex = new InvalidOperationException("No file path set for streaming processor");
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Error, 
                        "Cannot load segment: no file path", ex);
                    throw ex;
                }

                if (!File.Exists(currentFilePath))
                {
                    var ex = new FileNotFoundException("File no longer exists for segment loading", currentFilePath);
                    ErrorManager.LogFileError("load segment", currentFilePath, ex);
                    throw ex;
                }

                using var fileStream = new FileStream(currentFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, DEFAULT_CHUNK_SIZE);
                
                // Validate file position
                if (startPosition >= fileStream.Length)
                {
                    var ex = new ArgumentOutOfRangeException(nameof(startPosition), $"Start position {startPosition} exceeds file length {fileStream.Length}");
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Validation, ErrorManager.ErrorSeverity.Error, 
                        "Invalid file position for segment", ex, currentFilePath);
                    throw ex;
                }

                fileStream.Seek(startPosition, SeekOrigin.Begin);

                // Adjust length if it would read beyond file end
                var actualLength = Math.Min(length, fileStream.Length - startPosition);
                var buffer = new byte[actualLength];
                
                int totalBytesRead = 0;
                while (totalBytesRead < actualLength)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var bytesToRead = (int)(actualLength - totalBytesRead);
                    var bytesRead = await fileStream.ReadAsync(buffer, totalBytesRead, bytesToRead, cancellationToken);
                    
                    if (bytesRead == 0)
                    {
                        ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Warning, 
                            $"Unexpected end of file at position {fileStream.Position}");
                        break;
                    }
                    
                    totalBytesRead += bytesRead;
                }

                // Convert to text with proper encoding detection
                var encoding = DetectEncoding(buffer, totalBytesRead);
                segment.Content = encoding.GetString(buffer, 0, totalBytesRead);
                segment.Length = totalBytesRead; // Update with actual length

                // Normalize line endings with error handling
                try
                {
                    segment.Content = NormalizeLineEndings(segment.Content);
                }
                catch (OutOfMemoryException ex)
                {
                    ErrorManager.LogMemoryError("normalize line endings in segment", ex, GC.GetTotalMemory(false));
                    // Keep original content if normalization fails due to memory
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Warning, 
                        "Skipping line ending normalization due to memory constraints");
                }

                // Extract hyperlinks from this segment with memory protection
                segment.Hyperlinks = await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExtractHyperlinksFromSegmentAsync(segment.Content, startPosition, cancellationToken);
                },
                ErrorManager.ErrorCategory.Memory,
                "Extract hyperlinks from segment",
                new List<HyperlinkModel>());

                return segment;
            },
            ErrorManager.ErrorCategory.FileIO,
            "Load segment from file",
            new TextSegment { StartPosition = startPosition, Length = 0, Content = string.Empty });
        }

        public async IAsyncEnumerable<TextSegment> StreamFileAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            currentFilePath = filePath;
            var fileInfo = new FileInfo(filePath);
            currentFileSize = fileInfo.Length;

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, DEFAULT_CHUNK_SIZE);
            var buffer = new byte[DEFAULT_CHUNK_SIZE];
            long processedBytes = 0;
            long position = 0;

            while (position < fileInfo.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bytesToRead = Math.Min(DEFAULT_CHUNK_SIZE, (int)(fileInfo.Length - position));
                var bytesRead = await fileStream.ReadAsync(buffer, 0, bytesToRead, cancellationToken);

                if (bytesRead == 0) break;

                var encoding = DetectEncoding(buffer, bytesRead);
                var content = encoding.GetString(buffer, 0, bytesRead);
                content = NormalizeLineEndings(content);

                var segment = new TextSegment
                {
                    StartPosition = position,
                    Length = bytesRead,
                    Content = content,
                    Hyperlinks = await ExtractHyperlinksFromSegmentAsync(content, position, cancellationToken)
                };

                processedBytes += bytesRead;
                position += bytesRead;

                ProgressChanged?.Invoke(this, new ProgressEventArgs
                {
                    ProcessedBytes = processedBytes,
                    TotalBytes = fileInfo.Length,
                    CurrentOperation = "Loading text segments"
                });

                yield return segment;
            }
        }

        public async Task<List<TextSegment>> SearchInFileAsync(string searchTerm, bool caseSensitive = false, CancellationToken cancellationToken = default)
        {
            var results = new List<TextSegment>();
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            await foreach (var segment in StreamFileAsync(currentFilePath, cancellationToken))
            {
                if (segment.Content.Contains(searchTerm, comparison))
                {
                    results.Add(segment);
                }
            }

            return results;
        }

        private async Task<long> EstimateLineCountAsync(string filePath, CancellationToken cancellationToken)
        {
            const int sampleSize = 8192; // Sample first 8KB to estimate
            
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[Math.Min(sampleSize, fileStream.Length)];
            var bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

            if (bytesRead == 0) return 0;

            var sampleText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var linesInSample = sampleText.Split('\n').Length;
            
            // Estimate total lines based on sample
            var estimatedLines = (long)((double)linesInSample / bytesRead * fileStream.Length);
            return estimatedLines;
        }

        private static Encoding DetectEncoding(byte[] buffer, int count)
        {
            // Simple UTF-8 BOM detection
            if (count >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                return new UTF8Encoding(true);
            }

            // Default to UTF-8 without BOM
            return new UTF8Encoding(false);
        }

        private static string NormalizeLineEndings(string content)
        {
            return content.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
        }

        private async Task<List<HyperlinkModel>> ExtractHyperlinksFromSegmentAsync(string content, long basePosition, CancellationToken cancellationToken)
        {
            // Extract hyperlinks in chunks to avoid blocking
            return await Task.Run(() =>
            {
                var hyperlinks = new List<HyperlinkModel>();
                // Use the existing HyperlinkService logic but adapted for segments
                var (_, extractedHyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(content);
                
                // Adjust positions to be relative to file start
                foreach (var hyperlink in extractedHyperlinks)
                {
                    hyperlink.StartIndex += (int)basePosition;
                    hyperlinks.Add(hyperlink);
                }
                
                return hyperlinks;
            }, cancellationToken);
        }

        private void UpdateCacheAccessOrder(long key)
        {
            // Move accessed item to end of queue (LRU implementation)
            var newQueue = new Queue<long>();
            while (cacheAccessOrder.Count > 0)
            {
                var item = cacheAccessOrder.Dequeue();
                if (item != key)
                    newQueue.Enqueue(item);
            }
            newQueue.Enqueue(key);
            
            while (newQueue.Count > 0)
            {
                cacheAccessOrder.Enqueue(newQueue.Dequeue());
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    lock (cacheLock)
                    {
                        textCache.Clear();
                        cacheAccessOrder.Clear();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class StreamingFileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public bool IsLargeFile { get; set; }
        public long EstimatedLineCount { get; set; }
        public bool RequiresStreaming { get; set; }
    }
}