using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using ModernTextViewer.src.Models;
using System.Collections.Generic;
using System.Threading;

namespace ModernTextViewer.src.Services
{
    public class FileService
    {
        private const long STREAMING_THRESHOLD = 50 * 1024 * 1024; // 50MB threshold for streaming mode
        private const long MAX_SAFE_FILE_SIZE = 500 * 1024 * 1024; // 500MB absolute limit
        private const int MAX_RETRY_ATTEMPTS = 3;
        
        // Performance monitoring integration
        private static PerformanceMonitor? performanceMonitor;
        
        public static void SetPerformanceMonitor(PerformanceMonitor? monitor)
        {
            performanceMonitor = monitor;
        }

        public static bool ShouldUseStreaming(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Warning, 
                        "Cannot check streaming requirement: file path is empty");
                    return false;
                }

                if (!File.Exists(filePath))
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Warning, 
                        $"Cannot check streaming requirement: file does not exist - {filePath}");
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                
                if (fileInfo.Length > MAX_SAFE_FILE_SIZE)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Warning, 
                        $"File size ({fileInfo.Length / 1024 / 1024}MB) exceeds safe limit ({MAX_SAFE_FILE_SIZE / 1024 / 1024}MB)", 
                        null, filePath);
                }

                return fileInfo.Length > STREAMING_THRESHOLD;
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Warning, 
                    "Error checking streaming requirement", ex, filePath);
                return false; // Safe fallback
            }
        }

        public static async Task<(string content, List<HyperlinkModel> hyperlinks)> LoadFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                // Input validation
                if (string.IsNullOrEmpty(filePath))
                {
                    var ex = new ArgumentException("File path cannot be empty");
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Validation, ErrorManager.ErrorSeverity.Error, 
                        "Invalid file path provided", ex, "LoadFileAsync");
                    throw ex;
                }

                if (!File.Exists(filePath))
                {
                    var ex = new FileNotFoundException("File not found", filePath);
                    ErrorManager.LogFileError("load", filePath, ex, "File existence check");
                    throw ex;
                }

                // Check file size before attempting to load
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > MAX_SAFE_FILE_SIZE)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Warning, 
                        $"File is very large ({fileInfo.Length / 1024 / 1024}MB). Consider using streaming mode.", 
                        null, filePath);
                }

                // Use ErrorRecovery for robust file loading
                var recoveryResult = await ErrorRecovery.RecoverFileOperationAsync(
                    async (ct) =>
                    {
                        string content;
                        using (var reader = new StreamReader(filePath, new UTF8Encoding(false)))
                        {
                            content = await reader.ReadToEndAsync().ConfigureAwait(false);
                        }
                        
                        ct.ThrowIfCancellationRequested();
                        return content;
                    },
                    filePath,
                    "LoadFile",
                    cancellationToken);

                if (!recoveryResult.Success)
                {
                    var ex = new IOException($"Failed to load file after recovery attempts: {recoveryResult.ErrorMessage}");
                    ErrorManager.LogFileError("load", filePath, ex, $"Recovery failed after {recoveryResult.AttemptsUsed} attempts");
                    throw ex;
                }

                var fileContent = recoveryResult.Result as string ?? string.Empty;
                
                // Memory-safe hyperlink extraction
                var hyperlinkResult = await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await Task.Run(() => HyperlinkService.ExtractHyperlinkMetadata(fileContent), cancellationToken);
                }, 
                ErrorManager.ErrorCategory.Memory, 
                "Extract hyperlinks",
                (string.Empty, new List<HyperlinkModel>()));

                var (cleanContent, hyperlinks) = hyperlinkResult;
                
                // Safe line ending normalization
                try
                {
                    cleanContent = cleanContent.Replace("\r\n", "\n").Replace("\r", "\n");
                    string[] lines = cleanContent.Split('\n');
                    return (string.Join(Environment.NewLine, lines), hyperlinks);
                }
                catch (OutOfMemoryException ex)
                {
                    ErrorManager.LogMemoryError("normalize line endings", ex, GC.GetTotalMemory(false));
                    
                    // Fallback: return original content without normalization
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Info, 
                        "Using original content without line ending normalization due to memory constraints");
                    return (fileContent, hyperlinks);
                }
            },
            ErrorManager.ErrorCategory.FileIO,
            "Load file async",
            (string.Empty, new List<HyperlinkModel>()));
        }

        public static async Task SaveFileAsync(string filePath, string content, List<HyperlinkModel>? hyperlinks = null, CancellationToken cancellationToken = default)
        {
            await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                // Input validation
                if (string.IsNullOrEmpty(filePath))
                {
                    var ex = new ArgumentException("File path cannot be empty");
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Validation, ErrorManager.ErrorSeverity.Error, 
                        "Invalid file path provided for save", ex, "SaveFileAsync");
                    throw ex;
                }

                if (string.IsNullOrEmpty(content))
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Validation, ErrorManager.ErrorSeverity.Warning, 
                        "Saving empty content", null, filePath);
                }

                // Check available disk space
                await ValidateDiskSpaceAsync(filePath, content);

                // Use ErrorRecovery for robust file saving
                var recoveryResult = await ErrorRecovery.ExecuteWithRetryAsync(async (ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    
                    // Add hyperlink metadata if present (with memory protection)
                    string finalContent = content;
                    if (hyperlinks != null && hyperlinks.Count > 0)
                    {
                        finalContent = await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
                        {
                            return await Task.Run(() => HyperlinkService.AddHyperlinkMetadata(content, hyperlinks), ct);
                        },
                        ErrorManager.ErrorCategory.Memory,
                        "Add hyperlink metadata",
                        content);
                    }
                    
                    ct.ThrowIfCancellationRequested();

                    // Memory-safe content processing
                    string processedContent = await ErrorRecovery.RecoverMemoryOperationAsync(
                        async (ct2) => await Task.Run(() =>
                        {
                            // Split content into lines and rejoin with Windows line endings
                            string[] lines = finalContent.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                            var result = string.Join("\r\n", lines);

                            // Ensure the content ends with a single line ending
                            if (!result.EndsWith("\r\n"))
                            {
                                result += "\r\n";
                            }
                            
                            return result;
                        }, ct2).ConfigureAwait(false),
                        // Fallback: minimal processing
                        async (ct2) => await Task.FromResult(finalContent.EndsWith("\r\n") ? finalContent : finalContent + "\r\n"),
                        "Process content formatting",
                        ct);

                    if (!processedContent.Success)
                    {
                        throw new OutOfMemoryException("Failed to process content for saving");
                    }

                    var contentToSave = processedContent.Result as string ?? finalContent;
                    
                    ct.ThrowIfCancellationRequested();

                    // Atomic write operation with backup
                    await SaveWithBackupAsync(filePath, contentToSave, ct);

                    return true;
                },
                "Save file operation",
                ErrorManager.ErrorCategory.FileIO,
                MAX_RETRY_ATTEMPTS,
                cancellationToken);

                if (!recoveryResult.Success)
                {
                    var ex = new IOException($"Failed to save file after {recoveryResult.AttemptsUsed} attempts: {recoveryResult.ErrorMessage}");
                    ErrorManager.LogFileError("save", filePath, ex, "All retry attempts failed");
                    throw ex;
                }

                ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Info, 
                    $"File saved successfully to {filePath}", null, $"Size: {content.Length} characters");

                return true;
            },
            ErrorManager.ErrorCategory.FileIO,
            "Save file async",
            false);
        }

        public static async Task SaveStreamingFileAsync(string filePath, string content, List<HyperlinkModel>? hyperlinks = null, CancellationToken cancellationToken = default)
        {
            await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                // Input validation
                if (string.IsNullOrEmpty(filePath))
                {
                    var ex = new ArgumentException("File path cannot be empty");
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Validation, ErrorManager.ErrorSeverity.Error, 
                        "Invalid file path provided for streaming save", ex, "SaveStreamingFileAsync");
                    throw ex;
                }

                // Check available disk space
                await ValidateDiskSpaceAsync(filePath, content);

                // Use ErrorRecovery for robust streaming save
                var recoveryResult = await ErrorRecovery.ExecuteWithRetryAsync(async (ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    
                    // Add hyperlink metadata if present (with memory protection)
                    string finalContent = content;
                    if (hyperlinks != null && hyperlinks.Count > 0)
                    {
                        var metadataResult = await ErrorRecovery.RecoverMemoryOperationAsync(
                            async (ct2) => await Task.Run(() => HyperlinkService.AddHyperlinkMetadata(content, hyperlinks), ct2),
                            async (ct2) => await Task.FromResult(content), // Fallback: no metadata
                            "Add hyperlink metadata for streaming",
                            ct);

                        finalContent = metadataResult.Success ? metadataResult.Result as string ?? content : content;
                        
                        if (!metadataResult.Success)
                        {
                            ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Warning, 
                                "Skipping hyperlink metadata due to memory constraints");
                        }
                    }
                    
                    // Streaming save with atomic operation
                    var tempPath = filePath + ".tmp";
                    
                    try
                    {
                        using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192);
                        using var writer = new StreamWriter(fileStream, new UTF8Encoding(false));
                        
                        await WriteContentInChunksAsync(writer, finalContent, ct);
                        await writer.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        ErrorManager.LogFileError("streaming write to temp file", tempPath, ex);
                        throw;
                    }
                    
                    ct.ThrowIfCancellationRequested();

                    // Atomic move to final location
                    if (File.Exists(filePath))
                    {
                        var backupPath = filePath + ".backup";
                        File.Move(filePath, backupPath);
                        
                        try
                        {
                            File.Move(tempPath, filePath);
                            File.Delete(backupPath);
                        }
                        catch
                        {
                            // Restore backup on failure
                            if (File.Exists(backupPath))
                            {
                                if (File.Exists(filePath)) File.Delete(filePath);
                                File.Move(backupPath, filePath);
                            }
                            throw;
                        }
                    }
                    else
                    {
                        File.Move(tempPath, filePath);
                    }

                    return true;
                },
                "Streaming file save operation",
                ErrorManager.ErrorCategory.FileIO,
                MAX_RETRY_ATTEMPTS,
                cancellationToken);

                if (!recoveryResult.Success)
                {
                    var ex = new IOException($"Failed to save file in streaming mode after {recoveryResult.AttemptsUsed} attempts: {recoveryResult.ErrorMessage}");
                    ErrorManager.LogFileError("streaming save", filePath, ex, "All retry attempts failed");
                    throw ex;
                }

                ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Info, 
                    $"File saved successfully in streaming mode to {filePath}", null, $"Size: ~{content.Length} characters");

                return true;
            },
            ErrorManager.ErrorCategory.FileIO,
            "Save streaming file async",
            false);
        }

        private static async Task WriteContentInChunksAsync(StreamWriter writer, string content, CancellationToken cancellationToken)
        {
            await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                var processedContent = content.Replace("\r\n", "\n").Replace("\r", "\n");
                var lines = processedContent.Split('\n');
                
                ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, ErrorManager.ErrorSeverity.Info, 
                    $"Writing {lines.Length} lines in chunks");

                for (int i = 0; i < lines.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        await writer.WriteAsync(lines[i]);
                        if (i < lines.Length - 1)
                        {
                            await writer.WriteAsync("\r\n");
                        }
                        
                        // Flush periodically to avoid memory buildup and handle potential I/O errors
                        if (i % 1000 == 0)
                        {
                            await writer.FlushAsync();
                            
                            // Progress reporting for very large files
                            if (i % 10000 == 0 && lines.Length > 20000)
                            {
                                var progress = (i * 100) / lines.Length;
                                ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, ErrorManager.ErrorSeverity.Info, 
                                    $"Streaming write progress: {progress}% ({i}/{lines.Length} lines)");
                            }
                        }
                    }
                    catch (IOException ex)
                    {
                        ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Error, 
                            $"I/O error at line {i} during streaming write", ex);
                        throw;
                    }
                    catch (OutOfMemoryException ex)
                    {
                        ErrorManager.LogMemoryError("streaming write chunk", ex, GC.GetTotalMemory(false));
                        
                        // Try to recover by flushing and forcing GC
                        try
                        {
                            await writer.FlushAsync();
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            
                            ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Info, 
                                "Attempted memory recovery during streaming write");
                        }
                        catch
                        {
                            // Re-throw original exception if recovery fails
                            throw ex;
                        }
                        
                        throw;
                    }
                }
                
                // Ensure final line ending
                try
                {
                    if (!content.EndsWith("\r\n"))
                    {
                        await writer.WriteAsync("\r\n");
                    }
                    
                    await writer.FlushAsync();
                }
                catch (Exception ex)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Error, 
                        "Error writing final line ending", ex);
                    throw;
                }

                return true;
            },
            ErrorManager.ErrorCategory.FileIO,
            "Write content in chunks",
            false);
        }

        public static async Task<StreamingFileInfo> AnalyzeFileForStreamingAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                using var processor = new StreamingTextProcessor();
                return await processor.AnalyzeFileAsync(filePath, cancellationToken);
            },
            ErrorManager.ErrorCategory.FileIO,
            "Analyze file for streaming",
            new StreamingFileInfo { FilePath = filePath, RequiresStreaming = false });
        }

        private static async Task ValidateDiskSpaceAsync(string filePath, string content)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var driveInfo = new DriveInfo(Path.GetPathRoot(directory) ?? "C:");
                
                if (!driveInfo.IsReady)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Warning, 
                        $"Cannot check disk space: drive {driveInfo.Name} is not ready");
                    return;
                }

                // Estimate space needed (content size + overhead)
                var estimatedSize = System.Text.Encoding.UTF8.GetByteCount(content);
                var requiredSpace = estimatedSize * 2; // 2x for safety margin

                if (driveInfo.AvailableFreeSpace < requiredSpace)
                {
                    var ex = new IOException($"Insufficient disk space. Required: {requiredSpace / 1024}KB, Available: {driveInfo.AvailableFreeSpace / 1024}KB");
                    ErrorManager.LogFileError("validate disk space", filePath, ex);
                    throw ex;
                }

                if (driveInfo.AvailableFreeSpace < requiredSpace * 2)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Warning, 
                        $"Low disk space warning. Available: {driveInfo.AvailableFreeSpace / 1024 / 1024}MB", 
                        null, filePath);
                }
            }
            catch (Exception ex) when (!(ex is IOException))
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Warning, 
                    "Could not validate disk space", ex, filePath);
                // Don't throw - disk space validation is best-effort
            }
        }

        private static async Task SaveWithBackupAsync(string filePath, string content, CancellationToken cancellationToken)
        {
            var backupPath = string.Empty;
            var tempPath = string.Empty;

            try
            {
                // Create backup if file already exists
                if (File.Exists(filePath))
                {
                    backupPath = filePath + ".backup";
                    File.Copy(filePath, backupPath, true);
                }

                // Write to temporary file first (atomic operation)
                tempPath = filePath + ".tmp";
                using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(false)))
                {
                    await writer.WriteAsync(content).ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Atomic move to final location
                if (File.Exists(filePath))
                {
                    File.Replace(tempPath, filePath, backupPath);
                }
                else
                {
                    File.Move(tempPath, filePath);
                }

                // Clean up backup after successful save
                if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                {
                    try
                    {
                        File.Delete(backupPath);
                    }
                    catch (Exception ex)
                    {
                        ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Info, 
                            "Could not clean up backup file", ex, backupPath);
                        // Don't throw - cleanup is best-effort
                    }
                }
            }
            catch (Exception ex)
            {
                // Cleanup on failure
                try
                {
                    if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }

                    // Restore from backup if available
                    if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                        File.Move(backupPath, filePath);
                        
                        ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Info, 
                            "Restored file from backup after save failure", null, filePath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Warning, 
                        "Failed to cleanup after save error", cleanupEx, filePath);
                }

                throw;
            }
        }
    }
}

