using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ModernTextViewer.src.Services
{
    /// <summary>
    /// Helper service for extracting readable text from .docx files for use in raw and preview modes.
    /// This is intentionally simple and focuses on paragraphs and line breaks rather than full Word formatting.
    /// </summary>
    public static class DocxPreviewService
    {
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        /// <summary>
        /// Extracts plain text from a .docx file by reading word/document.xml and concatenating paragraph text.
        /// </summary>
        public static Task<string> LoadDocxAsPlainTextAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            // This work is fast enough to run synchronously; wrap in Task for async API compatibility.
            return Task.FromResult(ExtractPlainText(filePath));
        }

        private static string ExtractPlainText(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            try
            {
                using var zip = ZipFile.OpenRead(filePath);
                var entry = zip.GetEntry("word/document.xml");
                if (entry == null)
                {
                    return string.Empty;
                }

                using var stream = entry.Open();
                var xdoc = XDocument.Load(stream);

                var paragraphs = xdoc
                    .Descendants(W + "p")
                    .Select(p => GetParagraphText(p))
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();

                return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
            }
            catch
            {
                // On any parse error, fall back to an empty string so the caller can handle gracefully.
                return string.Empty;
            }
        }

        private static string GetParagraphText(XElement paragraph)
        {
            var sb = new StringBuilder();

            foreach (var node in paragraph.DescendantNodes())
            {
                if (node is XElement element)
                {
                    if (element.Name == W + "t")
                    {
                        sb.Append((string)element);
                    }
                    else if (element.Name == W + "br")
                    {
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
