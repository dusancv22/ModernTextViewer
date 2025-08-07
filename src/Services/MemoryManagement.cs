using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ModernTextViewer.src.Models;

namespace ModernTextViewer.src.Services
{
    /// <summary>
    /// Font caching system to prevent Font object leaks and reduce memory usage
    /// </summary>
    public class FontCache : IDisposable
    {
        private readonly Dictionary<string, Font> fontCache = new Dictionary<string, Font>();
        private readonly object lockObject = new object();
        private bool disposed = false;

        /// <summary>
        /// Gets or creates a cached font
        /// </summary>
        public Font GetFont(string familyName, float size, FontStyle style = FontStyle.Regular)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(FontCache));

            var key = $"{familyName}|{size}|{style}";
            
            lock (lockObject)
            {
                if (fontCache.TryGetValue(key, out var cachedFont))
                {
                    return cachedFont;
                }

                try
                {
                    var newFont = new Font(familyName, size, style);
                    fontCache[key] = newFont;
                    return newFont;
                }
                catch (ArgumentException)
                {
                    // Fallback to default font if family not found
                    var fallbackKey = $"Microsoft Sans Serif|{size}|{style}";
                    if (fontCache.TryGetValue(fallbackKey, out var fallbackFont))
                    {
                        return fallbackFont;
                    }
                    
                    var newFallbackFont = new Font(FontFamily.GenericSansSerif, size, style);
                    fontCache[fallbackKey] = newFallbackFont;
                    return newFallbackFont;
                }
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                lock (lockObject)
                {
                    foreach (var font in fontCache.Values)
                    {
                        font?.Dispose();
                    }
                    fontCache.Clear();
                }
                disposed = true;
            }
        }
    }

    /// <summary>
    /// Circular buffer implementation for efficient undo/redo with memory limits
    /// </summary>
    public class CircularUndoBuffer : IDisposable
    {
        public class UndoState
        {
            public string Text { get; set; } = "";
            public List<HyperlinkModel> Hyperlinks { get; set; } = new List<HyperlinkModel>();
            public int SelectionStart { get; set; }
            public int SelectionLength { get; set; }
            public DateTime Timestamp { get; set; }
            public long EstimatedMemorySize { get; set; }
        }

        private readonly UndoState[] buffer;
        private readonly int capacity;
        private readonly long maxMemoryBytes;
        private int head = 0;
        private int tail = 0;
        private int count = 0;
        private int currentPosition = -1;
        private long currentMemoryUsage = 0;
        private bool disposed = false;
        private readonly object lockObject = new object();

        // Large file threshold for diff-based undo (10MB)
        private const int LARGE_FILE_THRESHOLD = 10 * 1024 * 1024;
        
        public CircularUndoBuffer(int maxStates = 100, int maxMemoryMB = 50)
        {
            capacity = maxStates;
            maxMemoryBytes = maxMemoryMB * 1024L * 1024L;
            buffer = new UndoState[capacity];
        }

        /// <summary>
        /// Saves a new undo state with memory-efficient storage
        /// </summary>
        public void SaveState(string text, List<HyperlinkModel> hyperlinks, int selectionStart, int selectionLength)
        {
            if (disposed) return;

            lock (lockObject)
            {
                // Create new state
                var newState = new UndoState
                {
                    Text = text,
                    Hyperlinks = hyperlinks ?? new List<HyperlinkModel>(),
                    SelectionStart = selectionStart,
                    SelectionLength = selectionLength,
                    Timestamp = DateTime.Now
                };

                // Calculate memory usage
                newState.EstimatedMemorySize = EstimateMemorySize(newState);

                // For very large files, use diff-based storage
                if (text.Length > LARGE_FILE_THRESHOLD && count > 0)
                {
                    var previousState = GetCurrentState();
                    if (previousState != null && ShouldUseDiffStorage(text, previousState.Text))
                    {
                        // Store as diff (simplified approach - in production might use more sophisticated diff)
                        newState = CreateDiffState(previousState, newState);
                    }
                }

                // Add to buffer
                AddStateToBuffer(newState);

                // Clean up if memory limit exceeded
                CleanupOldStatesIfNeeded();

                // Reset redo position
                currentPosition = head - 1;
                if (currentPosition < 0) currentPosition = capacity - 1;
            }
        }

        /// <summary>
        /// Performs undo operation
        /// </summary>
        public UndoState? Undo()
        {
            if (disposed) return null;

            lock (lockObject)
            {
                if (count == 0) return null;

                // Move to previous state
                if (currentPosition == -1)
                {
                    currentPosition = head - 1;
                    if (currentPosition < 0) currentPosition = capacity - 1;
                }

                var prevPosition = currentPosition - 1;
                if (prevPosition < 0) prevPosition = capacity - 1;

                if (IsValidPosition(prevPosition))
                {
                    currentPosition = prevPosition;
                    return buffer[currentPosition];
                }

                return null;
            }
        }

        /// <summary>
        /// Performs redo operation
        /// </summary>
        public UndoState? Redo()
        {
            if (disposed) return null;

            lock (lockObject)
            {
                if (count == 0 || currentPosition == -1) return null;

                var nextPosition = (currentPosition + 1) % capacity;
                
                if (IsValidPosition(nextPosition) && nextPosition != head)
                {
                    currentPosition = nextPosition;
                    return buffer[currentPosition];
                }

                return null;
            }
        }

        private void AddStateToBuffer(UndoState state)
        {
            buffer[head] = state;
            currentMemoryUsage += state.EstimatedMemorySize;
            
            head = (head + 1) % capacity;
            
            if (count < capacity)
            {
                count++;
            }
            else
            {
                // Buffer is full, move tail
                currentMemoryUsage -= buffer[tail]?.EstimatedMemorySize ?? 0;
                tail = (tail + 1) % capacity;
            }
        }

        private void CleanupOldStatesIfNeeded()
        {
            // Remove oldest states if memory limit exceeded
            while (currentMemoryUsage > maxMemoryBytes && count > 1)
            {
                if (IsValidPosition(tail))
                {
                    currentMemoryUsage -= buffer[tail]?.EstimatedMemorySize ?? 0;
                    buffer[tail] = null!;
                }
                
                tail = (tail + 1) % capacity;
                count--;
            }
        }

        private bool IsValidPosition(int position)
        {
            return position >= 0 && position < capacity && buffer[position] != null;
        }

        private UndoState? GetCurrentState()
        {
            if (count == 0) return null;
            var prevIndex = head - 1;
            if (prevIndex < 0) prevIndex = capacity - 1;
            return buffer[prevIndex];
        }

        private static long EstimateMemorySize(UndoState state)
        {
            long size = 0;
            
            // Text size (2 bytes per char for UTF-16)
            size += state.Text.Length * 2;
            
            // Hyperlinks size
            size += state.Hyperlinks.Count * 128; // Rough estimate per hyperlink
            
            // Fixed overhead
            size += 64;
            
            return size;
        }

        private static bool ShouldUseDiffStorage(string newText, string oldText)
        {
            // Use diff if texts are large and similar
            if (newText.Length < LARGE_FILE_THRESHOLD || oldText.Length < LARGE_FILE_THRESHOLD)
                return false;

            // Simple similarity check - in production, could use more sophisticated algorithms
            var lengthDiff = Math.Abs(newText.Length - oldText.Length);
            return lengthDiff < (oldText.Length * 0.1); // Less than 10% size change
        }

        private static UndoState CreateDiffState(UndoState previousState, UndoState newState)
        {
            // Simplified diff implementation - stores full text for now
            // In production, could implement actual diff algorithm
            return newState;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                lock (lockObject)
                {
                    for (int i = 0; i < capacity; i++)
                    {
                        buffer[i] = null!;
                    }
                    count = 0;
                    currentMemoryUsage = 0;
                }
                disposed = true;
            }
        }
    }

    /// <summary>
    /// Monitors memory pressure and provides memory management recommendations
    /// </summary>
    public class MemoryPressureMonitor : IDisposable
    {
        private System.Threading.Timer? monitoringTimer;
        private long lastPrivateMemorySize = 0;
        private DateTime lastCleanupTime = DateTime.MinValue;
        private bool disposed = false;
        
        // Memory thresholds in MB
        private const long HIGH_MEMORY_THRESHOLD_MB = 500;
        private const long CRITICAL_MEMORY_THRESHOLD_MB = 800;
        private const int CLEANUP_INTERVAL_MINUTES = 5;

        public event EventHandler<MemoryPressureEventArgs>? MemoryPressureDetected;

        public void Start()
        {
            if (disposed) return;
            
            monitoringTimer = new System.Threading.Timer(CheckMemoryCallback, null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public void CheckMemoryPressure()
        {
            if (disposed) return;

            try
            {
                using var process = Process.GetCurrentProcess();
                long currentMemoryMB = process.PrivateMemorySize64 / (1024 * 1024);
                
                var pressureLevel = MemoryPressureLevel.Normal;
                
                if (currentMemoryMB > CRITICAL_MEMORY_THRESHOLD_MB)
                {
                    pressureLevel = MemoryPressureLevel.Critical;
                }
                else if (currentMemoryMB > HIGH_MEMORY_THRESHOLD_MB)
                {
                    pressureLevel = MemoryPressureLevel.High;
                }

                if (pressureLevel != MemoryPressureLevel.Normal)
                {
                    MemoryPressureDetected?.Invoke(this, new MemoryPressureEventArgs
                    {
                        CurrentMemoryMB = currentMemoryMB,
                        PressureLevel = pressureLevel
                    });

                    // Trigger cleanup if needed
                    if (DateTime.Now - lastCleanupTime > TimeSpan.FromMinutes(CLEANUP_INTERVAL_MINUTES))
                    {
                        TriggerGarbageCollection(pressureLevel);
                        lastCleanupTime = DateTime.Now;
                    }
                }

                lastPrivateMemorySize = currentMemoryMB;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Memory monitoring error: {ex.Message}");
            }
        }

        private void CheckMemoryCallback(object? state)
        {
            CheckMemoryPressure();
        }

        private static void TriggerGarbageCollection(MemoryPressureLevel level)
        {
            if (level == MemoryPressureLevel.Critical)
            {
                // Aggressive cleanup for critical memory pressure
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true, true);
            }
            else if (level == MemoryPressureLevel.High)
            {
                // Normal cleanup for high memory pressure
                GC.Collect(1, GCCollectionMode.Optimized, false);
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                monitoringTimer?.Dispose();
                monitoringTimer = null;
                disposed = true;
            }
        }
    }

    public enum MemoryPressureLevel
    {
        Normal,
        High,
        Critical
    }

    public class MemoryPressureEventArgs : EventArgs
    {
        public long CurrentMemoryMB { get; set; }
        public MemoryPressureLevel PressureLevel { get; set; }
    }

    /// <summary>
    /// Alias for compatibility with existing code
    /// </summary>
    public class UndoBuffer : CircularUndoBuffer
    {
        public UndoBuffer(int maxStates = 100, int maxMemoryMB = 50) : base(maxStates, maxMemoryMB)
        {
        }
    }
}