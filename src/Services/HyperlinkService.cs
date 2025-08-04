using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModernTextViewer.src.Models;

namespace ModernTextViewer.src.Services
{
    public class HyperlinkService
    {
        private const string HyperlinkMetadataStart = "<!--HYPERLINKS:";
        private const string HyperlinkMetadataEnd = "-->";
        
        // Background processing constants
        private const int CHUNK_SIZE = 1000; // Process text in chunks of 1000 characters
        private const int DEBOUNCE_DELAY_MS = 300; // Wait 300ms before processing changes
        
        // Windows API for controlling text redraw
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private const int WM_SETREDRAW = 0x000B;
        
        public static string GenerateRtfWithHyperlinks(string text, List<HyperlinkModel> hyperlinks)
        {
            if (hyperlinks == null || hyperlinks.Count == 0)
                return text;

            var rtfBuilder = new StringBuilder();
            rtfBuilder.Append(@"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl{\f0\fnil\fcharset0 Consolas;}}");
            rtfBuilder.Append(@"{\colortbl ;\red0\green102\blue204;}");
            rtfBuilder.Append(@"\viewkind4\uc1\pard\f0\fs20 ");

            var sortedHyperlinks = hyperlinks.OrderBy(h => h.StartIndex).ToList();
            int currentIndex = 0;

            foreach (var hyperlink in sortedHyperlinks)
            {
                if (hyperlink.StartIndex > currentIndex)
                {
                    string plainText = text.Substring(currentIndex, hyperlink.StartIndex - currentIndex);
                    rtfBuilder.Append(EscapeRtfText(plainText));
                }

                rtfBuilder.Append(@"{\field{\*\fldinst{HYPERLINK """ + EscapeRtfText(hyperlink.Url) + @"""}}");
                rtfBuilder.Append(@"{\fldrslt{\ul\cf1 " + EscapeRtfText(hyperlink.DisplayText) + @"}}}");

                currentIndex = hyperlink.EndIndex;
            }

            if (currentIndex < text.Length)
            {
                string remainingText = text.Substring(currentIndex);
                rtfBuilder.Append(EscapeRtfText(remainingText));
            }

            rtfBuilder.Append(@"\par}");
            return rtfBuilder.ToString();
        }

        public static string GenerateHtmlWithHyperlinks(string text, List<HyperlinkModel> hyperlinks)
        {
            if (hyperlinks == null || hyperlinks.Count == 0)
            {
                return ConvertTextToHtml(text);
            }

            var htmlBuilder = new StringBuilder();
            var sortedHyperlinks = hyperlinks.OrderBy(h => h.StartIndex).ToList();
            int currentIndex = 0;

            foreach (var hyperlink in sortedHyperlinks)
            {
                if (hyperlink.StartIndex > currentIndex)
                {
                    string plainText = text.Substring(currentIndex, hyperlink.StartIndex - currentIndex);
                    htmlBuilder.Append(ConvertTextToHtml(plainText));
                }

                htmlBuilder.Append($"<a href=\"{System.Net.WebUtility.HtmlEncode(hyperlink.Url)}\">");
                htmlBuilder.Append(System.Net.WebUtility.HtmlEncode(hyperlink.DisplayText));
                htmlBuilder.Append("</a>");

                currentIndex = hyperlink.EndIndex;
            }

            if (currentIndex < text.Length)
            {
                string remainingText = text.Substring(currentIndex);
                htmlBuilder.Append(ConvertTextToHtml(remainingText));
            }

            return htmlBuilder.ToString();
        }

        private static string ConvertTextToHtml(string text)
        {
            // Replace line breaks with <br> tags
            string html = System.Net.WebUtility.HtmlEncode(text);
            
            // Replace different line break formats with HTML line breaks
            html = html.Replace("\r\n", "<br>");
            html = html.Replace("\n", "<br>");
            html = html.Replace("\r", "<br>");
            
            return html;
        }

        public static string GenerateClipboardHtml(string htmlContent)
        {
            const string headerFormat = "Version:0.9\r\nStartHTML:{0:D8}\r\nEndHTML:{1:D8}\r\nStartFragment:{2:D8}\r\nEndFragment:{3:D8}\r\n";
            const string htmlStart = "<html><head><meta charset=\"utf-8\"></head><body><!--StartFragment--><div>";
            const string htmlEnd = "</div><!--EndFragment--></body></html>";

            // Calculate positions - must account for actual byte length
            string header = string.Format(headerFormat, 0, 0, 0, 0);
            int headerByteLength = Encoding.UTF8.GetByteCount(header);
            int startHtmlBytes = headerByteLength;
            int htmlStartBytes = Encoding.UTF8.GetByteCount(htmlStart);
            int startFragmentBytes = headerByteLength + htmlStartBytes - Encoding.UTF8.GetByteCount("<div>");
            int htmlContentBytes = Encoding.UTF8.GetByteCount(htmlContent);
            int endFragmentBytes = startFragmentBytes + Encoding.UTF8.GetByteCount("<div>") + htmlContentBytes;
            int htmlEndBytes = Encoding.UTF8.GetByteCount(htmlEnd);
            int endHtmlBytes = headerByteLength + htmlStartBytes + htmlContentBytes + htmlEndBytes;

            string formattedHeader = string.Format(headerFormat, startHtmlBytes, endHtmlBytes, startFragmentBytes, endFragmentBytes);
            
            return formattedHeader + htmlStart + htmlContent + htmlEnd;
        }

        public static void SetClipboardWithHyperlinks(string text, List<HyperlinkModel> hyperlinks)
        {
            DataObject dataObject = new DataObject();
            
            // Set plain text
            dataObject.SetText(text, TextDataFormat.Text);
            
            // Set RTF format
            string rtfContent = GenerateRtfWithHyperlinks(text, hyperlinks);
            dataObject.SetData(DataFormats.Rtf, rtfContent);
            
            // Set HTML format
            string htmlContent = GenerateHtmlWithHyperlinks(text, hyperlinks);
            string clipboardHtml = GenerateClipboardHtml(htmlContent);
            dataObject.SetData(DataFormats.Html, clipboardHtml);
            
            Clipboard.SetDataObject(dataObject, true);
        }

        public static string AddHyperlinkMetadata(string content, List<HyperlinkModel> hyperlinks)
        {
            if (hyperlinks == null || hyperlinks.Count == 0)
                return content;

            content = RemoveHyperlinkMetadata(content);

            var metadata = new List<object>();
            foreach (var hyperlink in hyperlinks)
            {
                metadata.Add(new
                {
                    start = hyperlink.StartIndex,
                    length = hyperlink.Length,
                    url = hyperlink.Url,
                    text = hyperlink.DisplayText
                });
            }

            string json = JsonSerializer.Serialize(metadata);
            return content + Environment.NewLine + Environment.NewLine + HyperlinkMetadataStart + json + HyperlinkMetadataEnd;
        }

        public static (string content, List<HyperlinkModel> hyperlinks) ExtractHyperlinkMetadata(string content)
        {
            if (string.IsNullOrEmpty(content))
                return (content, new List<HyperlinkModel>());

            int metadataStart = content.LastIndexOf(HyperlinkMetadataStart);
            if (metadataStart == -1)
                return (content, new List<HyperlinkModel>());

            int metadataEnd = content.IndexOf(HyperlinkMetadataEnd, metadataStart);
            if (metadataEnd == -1)
                return (content, new List<HyperlinkModel>());

            string cleanContent = content.Substring(0, metadataStart).TrimEnd();
            string metadataJson = content.Substring(metadataStart + HyperlinkMetadataStart.Length, 
                metadataEnd - metadataStart - HyperlinkMetadataStart.Length);

            try
            {
                var metadataList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(metadataJson);
                var hyperlinks = new List<HyperlinkModel>();

                if (metadataList != null)
                {
                    foreach (var item in metadataList)
                    {
                        hyperlinks.Add(new HyperlinkModel
                        {
                            StartIndex = item["start"].GetInt32(),
                            Length = item["length"].GetInt32(),
                            Url = item["url"].GetString() ?? string.Empty,
                            DisplayText = item["text"].GetString() ?? string.Empty
                        });
                    }
                }

                return (cleanContent, hyperlinks);
            }
            catch
            {
                return (content, new List<HyperlinkModel>());
            }
        }

        public static string RemoveHyperlinkMetadata(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            int metadataStart = content.LastIndexOf(HyperlinkMetadataStart);
            if (metadataStart == -1)
                return content;

            int metadataEnd = content.IndexOf(HyperlinkMetadataEnd, metadataStart);
            if (metadataEnd == -1)
                return content;

            return content.Substring(0, metadataStart).TrimEnd();
        }

        private static string EscapeRtfText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("\r\n", "\\par ")
                .Replace("\n", "\\par ")
                .Replace("\r", "\\par ");
        }

        public static bool IsValidUrl(string url)
        {
            // Allow any non-empty URL - browsers will handle protocol addition automatically
            return !string.IsNullOrWhiteSpace(url);
        }
        
        /// <summary>
        /// Represents formatting operations to be applied to text
        /// </summary>
        public class FormattingOperation
        {
            public int StartIndex { get; set; }
            public int Length { get; set; }
            public Color? Color { get; set; }
            public bool? IsUnderlined { get; set; }
        }
        
        /// <summary>
        /// Processes hyperlinks in background and returns formatting operations
        /// </summary>
        /// <param name="text">The text to process</param>
        /// <param name="hyperlinks">List of hyperlinks to format</param>
        /// <param name="isDarkMode">Whether dark mode is enabled</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of formatting operations to apply</returns>
        public static async Task<List<FormattingOperation>> ProcessHyperlinksAsync(
            string text, 
            List<HyperlinkModel> hyperlinks, 
            bool isDarkMode, 
            CancellationToken cancellationToken = default)
        {
            var operations = new List<FormattingOperation>();
            
            if (string.IsNullOrEmpty(text) || hyperlinks == null || hyperlinks.Count == 0)
                return operations;
            
            // Process in background thread to avoid UI blocking
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Determine colors based on theme
                var hyperlinkColor = isDarkMode ? Color.FromArgb(77, 166, 255) : Color.Blue;
                var defaultColor = isDarkMode ? Color.FromArgb(220, 220, 220) : Color.Black;
                
                // Process text in chunks to improve responsiveness
                var textLength = text.Length;
                var validHyperlinks = hyperlinks
                    .Where(h => h.StartIndex >= 0 && h.EndIndex <= textLength)
                    .OrderBy(h => h.StartIndex)
                    .ToList();
                
                // First, create operation to reset all text to default formatting
                operations.Add(new FormattingOperation
                {
                    StartIndex = 0,
                    Length = textLength,
                    Color = defaultColor,
                    IsUnderlined = false
                });
                
                // Then add hyperlink formatting operations
                foreach (var hyperlink in validHyperlinks)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    operations.Add(new FormattingOperation
                    {
                        StartIndex = hyperlink.StartIndex,
                        Length = hyperlink.Length,
                        Color = hyperlinkColor,
                        IsUnderlined = true
                    });
                }
                
            }, cancellationToken).ConfigureAwait(false);
            
            return operations;
        }
        
        /// <summary>
        /// Applies formatting operations to a RichTextBox efficiently
        /// </summary>
        /// <param name="textBox">The RichTextBox to format</param>
        /// <param name="operations">Formatting operations to apply</param>
        /// <param name="baseFont">Base font for the text</param>
        public static void ApplyFormattingOperations(RichTextBox textBox, List<FormattingOperation> operations, Font baseFont)
        {
            if (textBox == null || operations == null || operations.Count == 0)
                return;
            
            // Save current selection
            int savedSelectionStart = textBox.SelectionStart;
            int savedSelectionLength = textBox.SelectionLength;
            
            try
            {
                // Disable redraw during formatting to prevent flicker
                SendMessage(textBox.Handle, WM_SETREDRAW, 0, 0);
                
                // Apply formatting operations in batches
                foreach (var operation in operations)
                {
                    if (operation.StartIndex >= 0 && operation.StartIndex < textBox.TextLength &&
                        operation.Length > 0 && operation.StartIndex + operation.Length <= textBox.TextLength)
                    {
                        textBox.Select(operation.StartIndex, operation.Length);
                        
                        // Apply color if specified
                        if (operation.Color.HasValue)
                        {
                            textBox.SelectionColor = operation.Color.Value;
                        }
                        
                        // Apply underline if specified
                        if (operation.IsUnderlined.HasValue)
                        {
                            var currentFont = textBox.SelectionFont ?? baseFont;
                            var newStyle = operation.IsUnderlined.Value 
                                ? currentFont.Style | FontStyle.Underline
                                : currentFont.Style & ~FontStyle.Underline;
                            
                            if (currentFont.Style != newStyle)
                            {
                                using var newFont = new Font(currentFont, newStyle);
                                textBox.SelectionFont = newFont;
                            }
                        }
                    }
                }
            }
            finally
            {
                // Restore original selection
                textBox.Select(savedSelectionStart, savedSelectionLength);
                
                // Re-enable drawing
                SendMessage(textBox.Handle, WM_SETREDRAW, 1, 0);
                textBox.Invalidate();
            }
        }
    }
}