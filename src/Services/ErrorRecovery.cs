using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModernTextViewer.src.Models;

namespace ModernTextViewer.src.Services
{
    public static class ErrorRecovery
    {
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int BASE_RETRY_DELAY_MS = 1000;
        private const int MAX_RETRY_DELAY_MS = 10000;

        public enum RecoveryStrategy
        {
            Retry,
            Fallback,
            GracefulDegradation,
            UserIntervention
        }

        public class RecoveryResult
        {
            public bool Success { get; set; }
            public RecoveryStrategy StrategyUsed { get; set; }
            public string? ErrorMessage { get; set; }
            public object? Result { get; set; }
            public int AttemptsUsed { get; set; }
        }

        public static async Task<RecoveryResult> ExecuteWithRetryAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationName,
            ErrorManager.ErrorCategory category,
            int maxAttempts = MAX_RETRY_ATTEMPTS,
            CancellationToken cancellationToken = default)
        {
            Exception? lastException = null;
            
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var result = await operation(cancellationToken);
                    
                    if (attempt > 1)
                    {
                        ErrorManager.LogError(category, ErrorManager.ErrorSeverity.Info, 
                            $"Operation '{operationName}' succeeded after {attempt} attempts");
                    }

                    return new RecoveryResult
                    {
                        Success = true,
                        StrategyUsed = attempt > 1 ? RecoveryStrategy.Retry : RecoveryStrategy.Retry,
                        Result = result,
                        AttemptsUsed = attempt
                    };
                }
                catch (OperationCanceledException)
                {
                    return new RecoveryResult
                    {
                        Success = false,
                        StrategyUsed = RecoveryStrategy.UserIntervention,
                        ErrorMessage = "Operation was cancelled",
                        AttemptsUsed = attempt
                    };
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    ErrorManager.LogError(category, ErrorManager.ErrorSeverity.Warning, 
                        $"Attempt {attempt}/{maxAttempts} failed for '{operationName}': {ex.Message}", ex);

                    if (attempt == maxAttempts || !ShouldRetry(ex))
                    {
                        break;
                    }

                    // Exponential backoff with jitter
                    var delay = CalculateRetryDelay(attempt);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            return new RecoveryResult
            {
                Success = false,
                StrategyUsed = RecoveryStrategy.Retry,
                ErrorMessage = lastException?.Message ?? "Unknown error",
                AttemptsUsed = maxAttempts
            };
        }

        public static async Task<RecoveryResult> RecoverFileOperationAsync(
            Func<CancellationToken, Task<string>> primaryOperation,
            string filePath,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            // First, try the primary operation with retry
            var primaryResult = await ExecuteWithRetryAsync(
                primaryOperation,
                operationName,
                ErrorManager.ErrorCategory.FileIO,
                cancellationToken: cancellationToken);

            if (primaryResult.Success)
            {
                return primaryResult;
            }

            // If primary failed, try fallback strategies
            return await TryFileOperationFallbacks(filePath, operationName, cancellationToken);
        }

        public static async Task<RecoveryResult> RecoverMemoryOperationAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            Func<CancellationToken, Task<T>>? fallbackOperation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to force garbage collection before memory-intensive operation
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var result = await operation(cancellationToken);
                return new RecoveryResult
                {
                    Success = true,
                    StrategyUsed = RecoveryStrategy.Retry,
                    Result = result,
                    AttemptsUsed = 1
                };
            }
            catch (OutOfMemoryException ex)
            {
                ErrorManager.LogMemoryError(operationName, ex, GC.GetTotalMemory(false));

                if (fallbackOperation != null)
                {
                    try
                    {
                        // Force aggressive cleanup
                        ForceMemoryCleanup();

                        var fallbackResult = await fallbackOperation(cancellationToken);
                        return new RecoveryResult
                        {
                            Success = true,
                            StrategyUsed = RecoveryStrategy.Fallback,
                            Result = fallbackResult,
                            AttemptsUsed = 2
                        };
                    }
                    catch (Exception fallbackEx)
                    {
                        ErrorManager.LogMemoryError($"{operationName} (fallback)", fallbackEx);
                    }
                }

                return new RecoveryResult
                {
                    Success = false,
                    StrategyUsed = RecoveryStrategy.GracefulDegradation,
                    ErrorMessage = "Insufficient memory to complete operation",
                    AttemptsUsed = fallbackOperation != null ? 2 : 1
                };
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Error, 
                    $"Memory operation failed: {operationName}", ex);

                return new RecoveryResult
                {
                    Success = false,
                    StrategyUsed = RecoveryStrategy.UserIntervention,
                    ErrorMessage = ex.Message,
                    AttemptsUsed = 1
                };
            }
        }

        public static RecoveryResult RecoverUIOperation(Action operation, Action? fallbackOperation, string operationName)
        {
            try
            {
                operation();
                return new RecoveryResult
                {
                    Success = true,
                    StrategyUsed = RecoveryStrategy.Retry,
                    AttemptsUsed = 1
                };
            }
            catch (Exception ex)
            {
                ErrorManager.LogUIError("UI Component", operationName, ex);

                if (fallbackOperation != null)
                {
                    try
                    {
                        fallbackOperation();
                        return new RecoveryResult
                        {
                            Success = true,
                            StrategyUsed = RecoveryStrategy.Fallback,
                            AttemptsUsed = 2
                        };
                    }
                    catch (Exception fallbackEx)
                    {
                        ErrorManager.LogUIError("UI Component", $"{operationName} (fallback)", fallbackEx);
                    }
                }

                return new RecoveryResult
                {
                    Success = false,
                    StrategyUsed = RecoveryStrategy.GracefulDegradation,
                    ErrorMessage = ex.Message,
                    AttemptsUsed = fallbackOperation != null ? 2 : 1
                };
            }
        }

        public static async Task<bool> TryRecoverFromCriticalErrorAsync(Exception criticalError, string context)
        {
            ErrorManager.LogError(ErrorManager.ErrorCategory.System, ErrorManager.ErrorSeverity.Critical, 
                $"Critical error in {context}", criticalError);

            try
            {
                // Attempt emergency cleanup
                await PerformEmergencyCleanupAsync();

                // Try to save any unsaved work
                if (Application.OpenForms.Count > 0)
                {
                    var mainForm = Application.OpenForms[0];
                    if (mainForm is Form form)
                    {
                        // This would need to be implemented in the main form
                        await TryEmergencySaveAsync(form);
                    }
                }

                return true;
            }
            catch (Exception recoveryEx)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.System, ErrorManager.ErrorSeverity.Critical, 
                    "Failed to recover from critical error", recoveryEx, context);
                return false;
            }
        }

        private static async Task<RecoveryResult> TryFileOperationFallbacks(string filePath, string operationName, CancellationToken cancellationToken)
        {
            // Try to access file with different approaches
            var fallbacks = new[]
            {
                async (ct) => await TryReadOnlyAccessAsync(filePath, ct),
                async (ct) => await TryStreamingAccessAsync(filePath, ct),
                async (ct) => await TryChunkedAccessAsync(filePath, ct)
            };

            foreach (var fallback in fallbacks)
            {
                try
                {
                    var result = await fallback(cancellationToken);
                    if (!string.IsNullOrEmpty(result))
                    {
                        return new RecoveryResult
                        {
                            Success = true,
                            StrategyUsed = RecoveryStrategy.Fallback,
                            Result = result,
                            AttemptsUsed = 1
                        };
                    }
                }
                catch (Exception ex)
                {
                    ErrorManager.LogFileError($"{operationName} (fallback)", filePath, ex);
                }
            }

            return new RecoveryResult
            {
                Success = false,
                StrategyUsed = RecoveryStrategy.GracefulDegradation,
                ErrorMessage = "All file access methods failed",
                AttemptsUsed = fallbacks.Length
            };
        }

        private static async Task<string> TryReadOnlyAccessAsync(string filePath, CancellationToken cancellationToken)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);
            return await reader.ReadToEndAsync();
        }

        private static async Task<string> TryStreamingAccessAsync(string filePath, CancellationToken cancellationToken)
        {
            // Try to read file in smaller chunks to avoid memory issues
            const int chunkSize = 4096;
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, chunkSize);
            using var reader = new StreamReader(fileStream);
            
            var content = new System.Text.StringBuilder();
            var buffer = new char[chunkSize];
            int bytesRead;

            while ((bytesRead = await reader.ReadAsync(buffer, 0, chunkSize)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                content.Append(buffer, 0, bytesRead);
                
                // Limit total content to prevent memory issues
                if (content.Length > 100 * 1024 * 1024) // 100MB limit
                {
                    content.AppendLine("\n\n[Content truncated - file too large for recovery mode]");
                    break;
                }
            }

            return content.ToString();
        }

        private static async Task<string> TryChunkedAccessAsync(string filePath, CancellationToken cancellationToken)
        {
            // Read only the first part of the file if it's very large
            const int maxBytes = 10 * 1024 * 1024; // 10MB
            
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            
            var bytesToRead = Math.Min(maxBytes, (int)fileStream.Length);
            var buffer = new byte[bytesToRead];
            
            var totalBytesRead = 0;
            while (totalBytesRead < bytesToRead)
            {
                var bytesRead = await fileStream.ReadAsync(buffer, totalBytesRead, bytesToRead - totalBytesRead, cancellationToken);
                if (bytesRead == 0) break;
                totalBytesRead += bytesRead;
            }

            var content = System.Text.Encoding.UTF8.GetString(buffer, 0, totalBytesRead);
            
            if (fileStream.Length > maxBytes)
            {
                content += $"\n\n[Showing first {totalBytesRead / 1024}KB of {fileStream.Length / 1024}KB file]";
            }

            return content;
        }

        private static bool ShouldRetry(Exception exception)
        {
            return exception switch
            {
                IOException ioEx when ioEx.Message.Contains("being used") => true,
                IOException ioEx when ioEx.Message.Contains("network") => true,
                UnauthorizedAccessException => false,
                FileNotFoundException => false,
                DirectoryNotFoundException => false,
                OutOfMemoryException => false,
                ArgumentException => false,
                _ => true
            };
        }

        private static int CalculateRetryDelay(int attemptNumber)
        {
            // Exponential backoff with jitter
            var baseDelay = BASE_RETRY_DELAY_MS * Math.Pow(2, attemptNumber - 1);
            var jitter = new Random().Next(0, (int)(baseDelay * 0.1));
            var delay = (int)Math.Min(baseDelay + jitter, MAX_RETRY_DELAY_MS);
            
            return delay;
        }

        private static void ForceMemoryCleanup()
        {
            // Aggressive memory cleanup
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // Also compact the large object heap if available (.NET 4.5.1+)
            try
            {
                System.Runtime.GCSettings.LargeObjectHeapCompactionMode = 
                    System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
            }
            catch
            {
                // Ignore if not supported
            }
        }

        private static async Task PerformEmergencyCleanupAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Force garbage collection
                    ForceMemoryCleanup();

                    // Clear any static caches that might exist
                    // This would need to be coordinated with other services
                    
                    ErrorManager.LogError(ErrorManager.ErrorCategory.System, ErrorManager.ErrorSeverity.Info, 
                        "Emergency cleanup completed");
                }
                catch (Exception ex)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.System, ErrorManager.ErrorSeverity.Warning, 
                        "Emergency cleanup partially failed", ex);
                }
            });
        }

        private static async Task TryEmergencySaveAsync(Form mainForm)
        {
            try
            {
                // This would need to be implemented by the main form
                // For now, just log that we attempted it
                ErrorManager.LogError(ErrorManager.ErrorCategory.System, ErrorManager.ErrorSeverity.Info, 
                    "Attempting emergency save of user data");
                
                // Could use reflection to find and call save methods, or use an interface
                await Task.Delay(100); // Placeholder for actual save logic
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.System, ErrorManager.ErrorSeverity.Warning, 
                    "Emergency save failed", ex);
            }
        }
    }
}