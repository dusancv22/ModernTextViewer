using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ModernTextViewer.src.Models;

namespace ModernTextViewer.src.Services
{
    public class HyperlinkService
    {
        private const string HyperlinkMetadataStart = "<!--HYPERLINKS:";
        private const string HyperlinkMetadataEnd = "-->";
        
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
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? result))
            {
                return result.Scheme == Uri.UriSchemeHttp || 
                       result.Scheme == Uri.UriSchemeHttps ||
                       result.Scheme == Uri.UriSchemeMailto ||
                       result.Scheme == Uri.UriSchemeFtp;
            }

            return false;
        }
    }
}