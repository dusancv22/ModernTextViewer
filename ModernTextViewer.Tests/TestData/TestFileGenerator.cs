using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ModernTextViewer.Tests.TestData
{
    /// <summary>
    /// Generates test files of various sizes for performance and stability testing
    /// </summary>
    public static class TestFileGenerator
    {
        private static readonly string[] SampleWords = 
        {
            "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
            "Lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit",
            "performance", "test", "file", "generation", "memory", "usage", "optimization",
            "hyperlink", "http://example.com", "https://github.com", "file://test.txt"
        };

        private static readonly string[] HyperlinkTemplates =
        {
            "http://example.com/page/{0}",
            "https://github.com/user/repo/issues/{0}",
            "ftp://files.example.com/download/{0}.txt",
            "file://C:/TestFiles/document{0}.pdf",
            "mailto:test{0}@example.com",
            "https://docs.microsoft.com/en-us/dotnet/api/system.{0}"
        };

        /// <summary>
        /// Generates a test file with specified size and characteristics
        /// </summary>
        public static async Task<string> GenerateTestFileAsync(
            string fileName, 
            long targetSizeBytes, 
            int hyperlinkDensityPercent = 5,
            bool includeSpecialCharacters = true)
        {
            var testDataPath = Path.Combine(Path.GetTempPath(), "ModernTextViewerTests");
            Directory.CreateDirectory(testDataPath);
            
            var filePath = Path.Combine(testDataPath, fileName);
            
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            var random = new Random(42); // Fixed seed for reproducible tests
            var currentSize = 0L;
            var lineNumber = 1;
            
            while (currentSize < targetSizeBytes)
            {
                var line = GenerateLine(random, lineNumber, hyperlinkDensityPercent, includeSpecialCharacters);
                await writer.WriteLineAsync(line);
                
                currentSize += Encoding.UTF8.GetByteCount(line + Environment.NewLine);
                lineNumber++;
                
                // Progress indicator for large files
                if (lineNumber % 10000 == 0)
                {
                    await writer.FlushAsync();
                }
            }
            
            return filePath;
        }
        
        /// <summary>
        /// Generates a file with specific hyperlink count for hyperlink processing tests
        /// </summary>
        public static async Task<string> GenerateHyperlinkTestFileAsync(
            string fileName, 
            int hyperlinkCount, 
            long targetSizeBytes = 1024 * 1024) // 1MB default
        {
            var testDataPath = Path.Combine(Path.GetTempPath(), "ModernTextViewerTests");
            Directory.CreateDirectory(testDataPath);
            
            var filePath = Path.Combine(testDataPath, fileName);
            var random = new Random(42);
            
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            var currentSize = 0L;
            var hyperlinksAdded = 0;
            var lineNumber = 1;
            
            while (currentSize < targetSizeBytes)
            {
                string line;
                
                // Add hyperlinks strategically
                if (hyperlinksAdded < hyperlinkCount && random.Next(100) < 20) // 20% chance per line
                {
                    var template = HyperlinkTemplates[random.Next(HyperlinkTemplates.Length)];
                    var hyperlinkText = string.Format(template, hyperlinksAdded + 1);
                    line = $"Line {lineNumber}: This line contains a hyperlink: {hyperlinkText} - some additional text.";
                    hyperlinksAdded++;
                }
                else
                {
                    line = GenerateLine(random, lineNumber, 0, true);
                }
                
                await writer.WriteLineAsync(line);
                currentSize += Encoding.UTF8.GetByteCount(line + Environment.NewLine);
                lineNumber++;
            }
            
            // Ensure we have the requested number of hyperlinks
            while (hyperlinksAdded < hyperlinkCount)
            {
                var template = HyperlinkTemplates[random.Next(HyperlinkTemplates.Length)];
                var hyperlinkText = string.Format(template, hyperlinksAdded + 1);
                var line = $"Additional hyperlink line: {hyperlinkText}";
                await writer.WriteLineAsync(line);
                hyperlinksAdded++;
            }
            
            return filePath;
        }
        
        /// <summary>
        /// Generates a corrupted file for error recovery testing
        /// </summary>
        public static async Task<string> GenerateCorruptedFileAsync(string fileName)
        {
            var testDataPath = Path.Combine(Path.GetTempPath(), "ModernTextViewerTests");
            Directory.CreateDirectory(testDataPath);
            
            var filePath = Path.Combine(testDataPath, fileName);
            
            // Create a file with invalid UTF-8 sequences
            var validText = "This is valid text.\r\n";
            var invalidBytes = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC }; // Invalid UTF-8
            var moreValidText = "More valid text after corruption.\r\n";
            
            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            
            // Write valid text
            var validBytes = Encoding.UTF8.GetBytes(validText);
            await stream.WriteAsync(validBytes, 0, validBytes.Length);
            
            // Write invalid bytes
            await stream.WriteAsync(invalidBytes, 0, invalidBytes.Length);
            
            // Write more valid text
            var moreValidBytes = Encoding.UTF8.GetBytes(moreValidText);
            await stream.WriteAsync(moreValidBytes, 0, moreValidBytes.Length);
            
            return filePath;
        }
        
        /// <summary>
        /// Generates test files for a complete performance test suite
        /// </summary>
        public static async Task<TestFileSet> GenerateTestFileSuiteAsync()
        {
            var testFiles = new TestFileSet();
            
            // Small files (1KB - 100KB)
            testFiles.SmallFile1KB = await GenerateTestFileAsync("small_1kb.txt", 1024);
            testFiles.SmallFile10KB = await GenerateTestFileAsync("small_10kb.txt", 10 * 1024);
            testFiles.SmallFile100KB = await GenerateTestFileAsync("small_100kb.txt", 100 * 1024);
            
            // Medium files (1MB - 10MB)
            testFiles.MediumFile1MB = await GenerateTestFileAsync("medium_1mb.txt", 1024 * 1024);
            testFiles.MediumFile10MB = await GenerateTestFileAsync("medium_10mb.txt", 10 * 1024 * 1024);
            
            // Large files (50MB - 100MB)
            testFiles.LargeFile50MB = await GenerateTestFileAsync("large_50mb.txt", 50L * 1024 * 1024);
            testFiles.LargeFile100MB = await GenerateTestFileAsync("large_100mb.txt", 100L * 1024 * 1024);
            
            // Very large file (500MB) - only for stability tests
            testFiles.VeryLargeFile500MB = await GenerateTestFileAsync("very_large_500mb.txt", 500L * 1024 * 1024);
            
            // Hyperlink-heavy files
            testFiles.ManyHyperlinks1000 = await GenerateHyperlinkTestFileAsync("hyperlinks_1000.txt", 1000);
            testFiles.ManyHyperlinks10000 = await GenerateHyperlinkTestFileAsync("hyperlinks_10000.txt", 10000);
            
            // Special case files
            testFiles.CorruptedFile = await GenerateCorruptedFileAsync("corrupted.txt");
            testFiles.EmptyFile = await GenerateTestFileAsync("empty.txt", 0);
            
            return testFiles;
        }
        
        private static string GenerateLine(Random random, int lineNumber, int hyperlinkDensityPercent, bool includeSpecialCharacters)
        {
            var wordCount = random.Next(5, 20);
            var words = new string[wordCount];
            
            for (int i = 0; i < wordCount; i++)
            {
                // Add hyperlink based on density
                if (random.Next(100) < hyperlinkDensityPercent)
                {
                    var template = HyperlinkTemplates[random.Next(HyperlinkTemplates.Length)];
                    words[i] = string.Format(template, random.Next(1000));
                }
                else
                {
                    words[i] = SampleWords[random.Next(SampleWords.Length)];
                }
            }
            
            var line = $"Line {lineNumber}: " + string.Join(" ", words);
            
            // Add special characters occasionally
            if (includeSpecialCharacters && random.Next(10) == 0)
            {
                var specialChars = new[] { "àáâãäå", "çčćĉ", "èéêë", "ñńň", "öôõø", "üûù", "ÿý", "§†‡•", "«»""", "…–—" };
                line += " " + specialChars[random.Next(specialChars.Length)];
            }
            
            return line;
        }
        
        /// <summary>
        /// Cleans up all test files
        /// </summary>
        public static void CleanupTestFiles()
        {
            try
            {
                var testDataPath = Path.Combine(Path.GetTempPath(), "ModernTextViewerTests");
                if (Directory.Exists(testDataPath))
                {
                    Directory.Delete(testDataPath, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
    
    public class TestFileSet
    {
        public string SmallFile1KB { get; set; } = string.Empty;
        public string SmallFile10KB { get; set; } = string.Empty;
        public string SmallFile100KB { get; set; } = string.Empty;
        public string MediumFile1MB { get; set; } = string.Empty;
        public string MediumFile10MB { get; set; } = string.Empty;
        public string LargeFile50MB { get; set; } = string.Empty;
        public string LargeFile100MB { get; set; } = string.Empty;
        public string VeryLargeFile500MB { get; set; } = string.Empty;
        public string ManyHyperlinks1000 { get; set; } = string.Empty;
        public string ManyHyperlinks10000 { get; set; } = string.Empty;
        public string CorruptedFile { get; set; } = string.Empty;
        public string EmptyFile { get; set; } = string.Empty;
    }
}