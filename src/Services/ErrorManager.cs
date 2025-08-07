using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModernTextViewer.src.Services
{
    public static class ErrorManager
    {
        private static readonly List<ErrorEntry> _errorLog = new();
        private static readonly object _lockObject = new();
        private static bool _loggingEnabled = true;
        private static int _maxLogEntries = 1000;

        public static event EventHandler<ErrorEventArgs>? ErrorOccurred;
        public static event EventHandler<CriticalErrorEventArgs>? CriticalErrorOccurred;

        public enum ErrorCategory
        {
            FileIO,
            Memory,
            Network,
            UI,
            Performance,
            Validation,
            System,
            Unknown
        }

        public enum ErrorSeverity
        {
            Info,
            Warning,
            Error,
            Critical
        }

        public class ErrorEntry
        {
            public DateTime Timestamp { get; set; } = DateTime.Now;
            public ErrorCategory Category { get; set; }
            public ErrorSeverity Severity { get; set; }
            public string Message { get; set; } = string.Empty;
            public string? Details { get; set; }
            public string? StackTrace { get; set; }
            public string? Context { get; set; }
            public Exception? Exception { get; set; }
        }

        public class ErrorEventArgs : EventArgs
        {
            public ErrorEntry Error { get; set; } = null!;
            public bool CanRecover { get; set; } = true;
            public List<string> SuggestedActions { get; set; } = new();
        }

        public class CriticalErrorEventArgs : EventArgs
        {
            public ErrorEntry Error { get; set; } = null!;
            public bool RequiresRestart { get; set; }
            public string RecoveryInstructions { get; set; } = string.Empty;
        }

        public static void LogError(ErrorCategory category, ErrorSeverity severity, string message, Exception? exception = null, string? context = null)
        {
            if (!_loggingEnabled) return;

            var error = new ErrorEntry
            {
                Category = category,
                Severity = severity,
                Message = message,
                Details = exception?.Message,
                StackTrace = exception?.StackTrace,
                Context = context,
                Exception = exception
            };

            lock (_lockObject)
            {
                _errorLog.Add(error);
                
                // Maintain log size
                if (_errorLog.Count > _maxLogEntries)
                {
                    _errorLog.RemoveRange(0, _errorLog.Count - _maxLogEntries);
                }
            }

            // Write to debug output
            Debug.WriteLine($"[{severity}] {category}: {message}");
            if (exception != null)
            {
                Debug.WriteLine($"Exception: {exception.Message}");
                Debug.WriteLine($"Stack: {exception.StackTrace}");
            }

            // Notify listeners
            var eventArgs = CreateErrorEventArgs(error);
            
            if (severity == ErrorSeverity.Critical)
            {
                var criticalArgs = new CriticalErrorEventArgs
                {
                    Error = error,
                    RequiresRestart = DetermineIfRestartRequired(error),
                    RecoveryInstructions = GetRecoveryInstructions(error)
                };
                CriticalErrorOccurred?.Invoke(null, criticalArgs);
            }
            else
            {
                ErrorOccurred?.Invoke(null, eventArgs);
            }
        }

        public static void LogFileError(string operation, string filePath, Exception exception, string? additionalContext = null)
        {
            var context = $"Operation: {operation}, File: {filePath}";
            if (!string.IsNullOrEmpty(additionalContext))
                context += $", Context: {additionalContext}";

            var severity = DetermineFileSeverity(exception);
            LogError(ErrorCategory.FileIO, severity, GetUserFriendlyFileMessage(operation, exception), exception, context);
        }

        public static void LogMemoryError(string operation, Exception exception, long? memoryUsage = null)
        {
            var context = $"Operation: {operation}";
            if (memoryUsage.HasValue)
                context += $", Memory Usage: {memoryUsage.Value / 1024 / 1024}MB";

            var severity = exception is OutOfMemoryException ? ErrorSeverity.Critical : ErrorSeverity.Error;
            LogError(ErrorCategory.Memory, severity, GetUserFriendlyMemoryMessage(operation, exception), exception, context);
        }

        public static void LogUIError(string component, string operation, Exception exception)
        {
            var context = $"Component: {component}, Operation: {operation}";
            var severity = DetermineUISeverity(exception);
            LogError(ErrorCategory.UI, severity, GetUserFriendlyUIMessage(component, operation, exception), exception, context);
        }

        public static void LogPerformanceWarning(string operation, long duration, string? details = null)
        {
            var message = $"Slow operation detected: {operation} took {duration}ms";
            var context = $"Operation: {operation}, Duration: {duration}ms";
            if (!string.IsNullOrEmpty(details))
                context += $", Details: {details}";

            LogError(ErrorCategory.Performance, ErrorSeverity.Warning, message, null, context);
        }

        public static async Task<T> ExecuteWithErrorHandlingAsync<T>(Func<Task<T>> operation, ErrorCategory category, string operationName, T? fallbackValue = default)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await operation();
                stopwatch.Stop();

                // Log performance warning if operation took too long
                if (stopwatch.ElapsedMilliseconds > GetPerformanceThreshold(category))
                {
                    LogPerformanceWarning(operationName, stopwatch.ElapsedMilliseconds);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                LogError(category, ErrorSeverity.Info, $"Operation cancelled: {operationName}");
                return fallbackValue!;
            }
            catch (Exception ex)
            {
                LogError(category, GetDefaultSeverity(category, ex), $"Operation failed: {operationName}", ex);
                
                if (fallbackValue != null)
                {
                    LogError(category, ErrorSeverity.Info, $"Using fallback value for: {operationName}");
                    return fallbackValue;
                }

                throw;
            }
        }

        public static void ExecuteWithErrorHandling(Action operation, ErrorCategory category, string operationName, Action? fallbackAction = null)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                operation();
                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds > GetPerformanceThreshold(category))
                {
                    LogPerformanceWarning(operationName, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                LogError(category, GetDefaultSeverity(category, ex), $"Operation failed: {operationName}", ex);
                
                fallbackAction?.Invoke();
            }
        }

        public static List<ErrorEntry> GetRecentErrors(int count = 50)
        {
            lock (_lockObject)
            {
                return _errorLog.TakeLast(count).ToList();
            }
        }

        public static List<ErrorEntry> GetErrorsByCategory(ErrorCategory category, int maxCount = 100)
        {
            lock (_lockObject)
            {
                return _errorLog.Where(e => e.Category == category).TakeLast(maxCount).ToList();
            }
        }

        public static void ClearErrorLog()
        {
            lock (_lockObject)
            {
                _errorLog.Clear();
            }
        }

        private static ErrorEventArgs CreateErrorEventArgs(ErrorEntry error)
        {
            var eventArgs = new ErrorEventArgs
            {
                Error = error,
                CanRecover = DetermineRecoverability(error),
                SuggestedActions = GetSuggestedActions(error)
            };

            return eventArgs;
        }

        private static ErrorSeverity DetermineFileSeverity(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException => ErrorSeverity.Error,
                DirectoryNotFoundException => ErrorSeverity.Error,
                FileNotFoundException => ErrorSeverity.Error,
                IOException when exception.Message.Contains("disk") => ErrorSeverity.Critical,
                IOException when exception.Message.Contains("space") => ErrorSeverity.Critical,
                OutOfMemoryException => ErrorSeverity.Critical,
                _ => ErrorSeverity.Warning
            };
        }

        private static ErrorSeverity DetermineUISeverity(Exception exception)
        {
            return exception switch
            {
                InvalidOperationException => ErrorSeverity.Warning,
                ArgumentException => ErrorSeverity.Warning,
                Win32Exception => ErrorSeverity.Error,
                ExternalException => ErrorSeverity.Error,
                _ => ErrorSeverity.Warning
            };
        }

        private static string GetUserFriendlyFileMessage(string operation, Exception exception)
        {
            return exception switch
            {
                FileNotFoundException => $"The file could not be found. It may have been moved, deleted, or renamed.",
                DirectoryNotFoundException => $"The folder path is not valid or accessible.",
                UnauthorizedAccessException => $"Access denied. You don't have permission to {operation.ToLower()} this file.",
                IOException when exception.Message.Contains("being used") => $"The file is currently open in another program. Please close it and try again.",
                IOException when exception.Message.Contains("space") => $"There is not enough disk space to complete the {operation.ToLower()} operation.",
                OutOfMemoryException => $"The file is too large to process with available memory.",
                _ => $"An error occurred while trying to {operation.ToLower()} the file: {exception.Message}"
            };
        }

        private static string GetUserFriendlyMemoryMessage(string operation, Exception exception)
        {
            return exception switch
            {
                OutOfMemoryException => $"Not enough memory available to complete the {operation}. Try closing other applications or working with a smaller file.",
                InvalidOperationException => $"A memory-related error occurred during {operation}. The operation cannot be completed.",
                _ => $"A memory error occurred during {operation}: {exception.Message}"
            };
        }

        private static string GetUserFriendlyUIMessage(string component, string operation, Exception exception)
        {
            return exception switch
            {
                InvalidOperationException => $"The {component} is not in a valid state for {operation}.",
                ArgumentException => $"Invalid input provided to {component}.",
                Win32Exception => $"A system error occurred in {component}. Try restarting the application.",
                _ => $"An error occurred in {component} during {operation}: {exception.Message}"
            };
        }

        private static bool DetermineRecoverability(ErrorEntry error)
        {
            return error.Category switch
            {
                ErrorCategory.FileIO => error.Exception is not (OutOfMemoryException or SystemException),
                ErrorCategory.Memory => error.Severity != ErrorSeverity.Critical,
                ErrorCategory.UI => true,
                ErrorCategory.Performance => true,
                ErrorCategory.Validation => true,
                _ => error.Severity != ErrorSeverity.Critical
            };
        }

        private static List<string> GetSuggestedActions(ErrorEntry error)
        {
            var actions = new List<string>();

            switch (error.Category)
            {
                case ErrorCategory.FileIO:
                    if (error.Exception is FileNotFoundException)
                    {
                        actions.Add("Check if the file path is correct");
                        actions.Add("Verify the file hasn't been moved or deleted");
                        actions.Add("Try opening a different file");
                    }
                    else if (error.Exception is UnauthorizedAccessException)
                    {
                        actions.Add("Run the application as administrator");
                        actions.Add("Check file permissions");
                        actions.Add("Ensure the file isn't read-only");
                    }
                    else if (error.Exception?.Message.Contains("being used") == true)
                    {
                        actions.Add("Close the file in other applications");
                        actions.Add("Wait a moment and try again");
                        actions.Add("Restart the application");
                    }
                    break;

                case ErrorCategory.Memory:
                    actions.Add("Close other applications to free memory");
                    actions.Add("Try working with a smaller file");
                    actions.Add("Restart the application");
                    if (error.Severity == ErrorSeverity.Critical)
                    {
                        actions.Add("Consider upgrading system memory");
                    }
                    break;

                case ErrorCategory.UI:
                    actions.Add("Try the operation again");
                    actions.Add("Restart the application if the problem persists");
                    break;

                case ErrorCategory.Performance:
                    actions.Add("Close other applications");
                    actions.Add("Try working with smaller files");
                    actions.Add("Check available system resources");
                    break;
            }

            return actions;
        }

        private static bool DetermineIfRestartRequired(ErrorEntry error)
        {
            return error.Category switch
            {
                ErrorCategory.Memory when error.Exception is OutOfMemoryException => true,
                ErrorCategory.System => true,
                ErrorCategory.UI when error.Exception is Win32Exception => true,
                _ => false
            };
        }

        private static string GetRecoveryInstructions(ErrorEntry error)
        {
            if (DetermineIfRestartRequired(error))
            {
                return "The application needs to be restarted to recover from this error. Your work will be automatically saved before restart.";
            }

            return error.Category switch
            {
                ErrorCategory.FileIO => "Try selecting a different file or check file permissions.",
                ErrorCategory.Memory => "Close other applications and try again with a smaller file.",
                ErrorCategory.UI => "The interface will attempt to recover automatically.",
                _ => "The application will attempt to continue normally."
            };
        }

        private static long GetPerformanceThreshold(ErrorCategory category)
        {
            return category switch
            {
                ErrorCategory.FileIO => 5000,  // 5 seconds for file operations
                ErrorCategory.Memory => 2000,  // 2 seconds for memory operations
                ErrorCategory.UI => 100,       // 100ms for UI operations
                _ => 1000                      // 1 second default
            };
        }

        private static ErrorSeverity GetDefaultSeverity(ErrorCategory category, Exception exception)
        {
            if (exception is OutOfMemoryException or SystemException)
                return ErrorSeverity.Critical;

            return category switch
            {
                ErrorCategory.FileIO => ErrorSeverity.Error,
                ErrorCategory.Memory => ErrorSeverity.Error,
                ErrorCategory.UI => ErrorSeverity.Warning,
                ErrorCategory.Performance => ErrorSeverity.Warning,
                ErrorCategory.Validation => ErrorSeverity.Warning,
                _ => ErrorSeverity.Error
            };
        }

        public static void SetLoggingEnabled(bool enabled)
        {
            _loggingEnabled = enabled;
        }

        public static void SetMaxLogEntries(int maxEntries)
        {
            _maxLogEntries = Math.Max(100, maxEntries);
        }
    }
}