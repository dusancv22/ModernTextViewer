using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Markdig;

namespace ModernTextViewer.src.Services
{
    /// <summary>
    /// Service for converting markdown to HTML with theme support and performance optimizations for large content
    /// </summary>
    public class PreviewService
    {
        // Static cached pipeline for performance - configured once and reused
        private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        // Cached CSS strings for better performance
        private static readonly string _darkModeCSS = GenerateDarkModeCSS();
        private static readonly string _lightModeCSS = GenerateLightModeCSS();
        
        // New: Universal CSS with custom properties for dynamic theme switching
        private static readonly string _universalCSS = GenerateUniversalCSS();
        
        // Performance thresholds for large content optimization
        private const int LARGE_CONTENT_THRESHOLD = 50000; // 50KB HTML threshold
        private const int CHUNK_SIZE = 25000; // 25KB per chunk
        
        // Emergency fallback: disable chunking if set to false (for debugging)
        private const bool ENABLE_CHUNKING = true;

        /// <summary>
        /// Converts markdown text to HTML using the cached Markdig pipeline.
        /// Supports advanced extensions including tables, task lists, footnotes, and more.
        /// </summary>
        /// <param name="markdownText">The markdown text to convert. Can be null or empty.</param>
        /// <returns>HTML representation of the markdown, or empty string if input is null/empty</returns>
        /// <exception cref="Exception">When markdown parsing fails, returns safe HTML error message instead of throwing</exception>
        /// <example>
        /// <code>
        /// string markdown = "# Hello World\n\nThis is **bold** text.";
        /// string html = PreviewService.ConvertMarkdownToHtml(markdown);
        /// // Returns: "&lt;h1&gt;Hello World&lt;/h1&gt;\n&lt;p&gt;This is &lt;strong&gt;bold&lt;/strong&gt; text.&lt;/p&gt;"
        /// </code>
        /// </example>
        public static string ConvertMarkdownToHtml(string markdownText)
        {
            if (string.IsNullOrEmpty(markdownText))
                return string.Empty;

            try
            {
                return Markdown.ToHtml(markdownText, _markdownPipeline);
            }
            catch (Exception ex)
            {
                // Return safe fallback HTML with error message
                return $"<div style=\"color: red; padding: 10px; border: 1px solid red; margin: 10px;\">" +
                       $"<h3>Error converting markdown:</h3>" +
                       $"<p>{System.Net.WebUtility.HtmlEncode(ex.Message)}</p>" +
                       $"</div>";
            }
        }

        /// <summary>
        /// Generates a complete HTML document with theme-aware CSS styling optimized for WebView2 rendering.
        /// This method creates a full HTML5 document with embedded CSS that adapts to the application's theme.
        /// </summary>
        /// <param name="markdownText">The markdown text to convert. Can be null or empty.</param>
        /// <param name="isDarkMode">Whether to apply dark mode styling. When true, uses dark theme colors; when false, uses light theme colors.</param>
        /// <returns>Complete HTML5 document ready for WebView2 navigation, including DOCTYPE, meta tags, and embedded CSS</returns>
        /// <exception cref="Exception">When markdown conversion fails, returns error document instead of throwing</exception>
        /// <remarks>
        /// The generated HTML includes:
        /// <list type="bullet">
        /// <item>DOCTYPE html declaration</item>
        /// <item>Responsive viewport meta tag</item>
        /// <item>Theme-appropriate CSS styling</item>
        /// <item>GitHub-style markdown rendering</item>
        /// <item>Proper encoding (UTF-8)</item>
        /// </list>
        /// CSS is cached for performance - the same CSS is reused across multiple calls.
        /// </remarks>
        /// <example>
        /// <code>
        /// string markdown = "# Welcome\n\nThis is a **test** document.";
        /// string html = PreviewService.GenerateThemeAwareHtml(markdown, true);
        /// // Returns complete HTML document with dark theme styling
        /// </code>
        /// </example>
        public static string GenerateThemeAwareHtml(string markdownText, bool isDarkMode)
        {
            if (string.IsNullOrEmpty(markdownText))
            {
                return GenerateEmptyDocument(isDarkMode);
            }

            try
            {
                string htmlContent = ConvertMarkdownToHtml(markdownText);
                string css = isDarkMode ? _darkModeCSS : _lightModeCSS;
                
                var htmlBuilder = new StringBuilder();
                htmlBuilder.AppendLine("<!DOCTYPE html>");
                htmlBuilder.AppendLine("<html lang=\"en\">");
                htmlBuilder.AppendLine("<head>");
                htmlBuilder.AppendLine("    <meta charset=\"UTF-8\">");
                htmlBuilder.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
                htmlBuilder.AppendLine("    <title>Markdown Preview</title>");
                htmlBuilder.AppendLine("    <style>");
                htmlBuilder.AppendLine(css);
                htmlBuilder.AppendLine("    </style>");
                htmlBuilder.AppendLine("</head>");
                htmlBuilder.AppendLine("<body>");
                htmlBuilder.AppendLine("    <div class=\"markdown-body\">");
                htmlBuilder.AppendLine(htmlContent);
                htmlBuilder.AppendLine("    </div>");
                htmlBuilder.AppendLine("</body>");
                htmlBuilder.AppendLine("</html>");
                
                return htmlBuilder.ToString();
            }
            catch (Exception ex)
            {
                return GenerateErrorDocument(ex.Message, isDarkMode);
            }
        }

        /// <summary>
        /// Gets the cached CSS for dark mode styling.
        /// This CSS provides GitHub-style dark theme colors optimized for readability.
        /// </summary>
        /// <returns>Dark mode CSS string with comprehensive styling for all markdown elements</returns>
        /// <remarks>
        /// The CSS includes styling for:
        /// <list type="bullet">
        /// <item>Headers (h1-h6) with appropriate hierarchy</item>
        /// <item>Code blocks and inline code with syntax highlighting colors</item>
        /// <item>Tables with hover effects</item>
        /// <item>Links with theme-appropriate colors</item>
        /// <item>Blockquotes with left border styling</item>
        /// <item>Task lists and regular lists</item>
        /// <item>Images with responsive sizing</item>
        /// </list>
        /// </remarks>
        public static string GetDarkModeCSS()
        {
            return _darkModeCSS;
        }

        /// <summary>
        /// Gets the cached CSS for light mode styling.
        /// This CSS provides GitHub-style light theme colors optimized for readability.
        /// </summary>
        /// <returns>Light mode CSS string with comprehensive styling for all markdown elements</returns>
        /// <remarks>
        /// The CSS includes styling for:
        /// <list type="bullet">
        /// <item>Headers (h1-h6) with appropriate hierarchy</item>
        /// <item>Code blocks and inline code with syntax highlighting colors</item>
        /// <item>Tables with hover effects</item>
        /// <item>Links with theme-appropriate colors</item>
        /// <item>Blockquotes with left border styling</item>
        /// <item>Task lists and regular lists</item>
        /// <item>Images with responsive sizing</item>
        /// </list>
        /// </remarks>
        public static string GetLightModeCSS()
        {
            return _lightModeCSS;
        }

        /// <summary>
        /// Generates a complete HTML document with universal CSS that supports dynamic theme switching.
        /// This method creates an HTML document that can switch themes instantly via JavaScript without page reloads.
        /// Includes performance optimizations for large content via lazy loading and content virtualization.
        /// </summary>
        /// <param name="markdownText">The markdown text to convert. Can be null or empty.</param>
        /// <param name="isDarkMode">Initial theme state. When true, starts with dark theme; when false, starts with light theme.</param>
        /// <returns>Complete HTML5 document with universal CSS and theme switching support</returns>
        /// <remarks>
        /// This method is designed for performance-optimized theme switching and large content handling. The generated HTML:
        /// <list type="bullet">
        /// <item>Uses CSS custom properties (variables) for all theme colors</item>
        /// <item>Includes smooth transitions between theme changes</item>
        /// <item>Supports instant theme switching via JavaScript</item>
        /// <item>Maintains full compatibility with all markdown elements</item>
        /// <item>Automatically sets the initial theme via data-theme attribute</item>
        /// <item>Optimizes large content with progressive loading and content virtualization</item>
        /// </list>
        /// For content larger than 50KB, implements lazy loading sections to improve initial render time.
        /// </remarks>
        public static string GenerateUniversalThemeHtml(string markdownText, bool isDarkMode)
        {
            if (string.IsNullOrEmpty(markdownText))
            {
                return GenerateEmptyUniversalDocument(isDarkMode);
            }

            try
            {
                string htmlContent = ConvertMarkdownToHtml(markdownText);
                
                // Check if content is large and needs optimization
                if (ENABLE_CHUNKING && htmlContent.Length > LARGE_CONTENT_THRESHOLD)
                {
                    return GenerateOptimizedLargeContentHtml(htmlContent, isDarkMode);
                }
                
                // Standard path for smaller content
                return GenerateStandardUniversalHtml(htmlContent, isDarkMode);
            }
            catch (Exception ex)
            {
                return GenerateErrorUniversalDocument(ex.Message, isDarkMode);
            }
        }

        /// <summary>
        /// Generates optimized HTML for large content using progressive loading and content virtualization.
        /// Splits large content into chunks and implements lazy loading to improve initial render performance.
        /// </summary>
        /// <param name="htmlContent">The converted HTML content (assumed to be large)</param>
        /// <param name="isDarkMode">Theme state for initial rendering</param>
        /// <returns>HTML document optimized for large content rendering</returns>
        private static string GenerateOptimizedLargeContentHtml(string htmlContent, bool isDarkMode)
        {
            string themeAttribute = isDarkMode ? "dark" : "light";
            
            // Split content into logical chunks (by sections/headers when possible)
            var chunks = SplitContentIntoChunks(htmlContent);
            
            var htmlBuilder = new StringBuilder();
            htmlBuilder.AppendLine("<!DOCTYPE html>");
            htmlBuilder.AppendLine($"<html lang=\"en\" data-theme=\"{themeAttribute}\">");
            htmlBuilder.AppendLine("<head>");
            htmlBuilder.AppendLine("    <meta charset=\"UTF-8\">");
            htmlBuilder.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            htmlBuilder.AppendLine("    <title>Markdown Preview</title>");
            htmlBuilder.AppendLine("    <style>");
            htmlBuilder.AppendLine(_universalCSS);
            
            // Add performance optimization CSS for large content
            htmlBuilder.AppendLine(@"
        /* Large Content Performance Optimizations */
        .content-chunk {
            contain: layout style paint;
            transform: translateZ(0); /* Force hardware acceleration */
        }
        
        .loading-chunk {
            min-height: 100px;
            display: flex;
            align-items: center;
            justify-content: center;
            color: var(--text-color);
            font-style: italic;
            opacity: 0.7;
        }
        
        .content-visible {
            animation: fadeIn 0.3s ease-in;
        }
        
        @keyframes fadeIn {
            from { opacity: 0; }
            to { opacity: 1; }
        }");
            
            htmlBuilder.AppendLine("    </style>");
            htmlBuilder.AppendLine("</head>");
            htmlBuilder.AppendLine($"<body data-theme=\"{themeAttribute}\">");
            htmlBuilder.AppendLine("    <div class=\"markdown-body\">");
            
            // Render first chunk immediately, others as lazy-loaded sections
            for (int i = 0; i < chunks.Count; i++)
            {
                if (i == 0)
                {
                    // First chunk loads immediately
                    htmlBuilder.AppendLine($"        <div class=\"content-chunk content-visible\" id=\"chunk-{i}\">");
                    htmlBuilder.AppendLine(chunks[i]);
                    htmlBuilder.AppendLine("        </div>");
                }
                else
                {
                    // Subsequent chunks use lazy loading placeholder
                    htmlBuilder.AppendLine($"        <div class=\"content-chunk loading-chunk\" id=\"chunk-{i}\" data-content-index=\"{i}\">");
                    htmlBuilder.AppendLine($"            <div>Loading section {i + 1}...</div>");
                    htmlBuilder.AppendLine($"            <script type=\"text/html\" id=\"chunk-data-{i}\">{chunks[i]}</script>");
                    htmlBuilder.AppendLine("        </div>");
                }
            }
            
            htmlBuilder.AppendLine("    </div>");
            
            // Add progressive loading JavaScript
            htmlBuilder.AppendLine("    <script>");
            htmlBuilder.AppendLine(GetProgressiveLoadingScript());
            htmlBuilder.AppendLine("    </script>");
            
            htmlBuilder.AppendLine("</body>");
            htmlBuilder.AppendLine("</html>");
            
            return htmlBuilder.ToString();
        }
        
        /// <summary>
        /// Generates standard HTML for normal-sized content without performance optimizations.
        /// </summary>
        /// <param name="htmlContent">The converted HTML content</param>
        /// <param name="isDarkMode">Theme state for rendering</param>
        /// <returns>Standard HTML document</returns>
        private static string GenerateStandardUniversalHtml(string htmlContent, bool isDarkMode)
        {
            string themeAttribute = isDarkMode ? "dark" : "light";
            
            var htmlBuilder = new StringBuilder();
            htmlBuilder.AppendLine("<!DOCTYPE html>");
            htmlBuilder.AppendLine($"<html lang=\"en\" data-theme=\"{themeAttribute}\">");
            htmlBuilder.AppendLine("<head>");
            htmlBuilder.AppendLine("    <meta charset=\"UTF-8\">");
            htmlBuilder.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            htmlBuilder.AppendLine("    <title>Markdown Preview</title>");
            htmlBuilder.AppendLine("    <style>");
            htmlBuilder.AppendLine(_universalCSS);
            htmlBuilder.AppendLine("    </style>");
            htmlBuilder.AppendLine("</head>");
            htmlBuilder.AppendLine($"<body data-theme=\"{themeAttribute}\">");
            htmlBuilder.AppendLine("    <div class=\"markdown-body\">");
            htmlBuilder.AppendLine(htmlContent);
            htmlBuilder.AppendLine("    </div>");
            htmlBuilder.AppendLine("</body>");
            htmlBuilder.AppendLine("</html>");
            
            return htmlBuilder.ToString();
        }
        
        /// <summary>
        /// Splits large HTML content into logical chunks for progressive loading.
        /// Attempts to split at section boundaries (h1, h2) when possible for better UX.
        /// </summary>
        /// <param name="htmlContent">The HTML content to split</param>
        /// <returns>List of HTML content chunks</returns>
        private static List<string> SplitContentIntoChunks(string htmlContent)
        {
            var chunks = new List<string>();
            
            // Try to split at logical boundaries (headers) first
            var headerSplits = System.Text.RegularExpressions.Regex.Split(
                htmlContent, 
                @"(<h[12][^>]*>.*?</h[12]>)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            ).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            
            if (headerSplits.Length > 1)
            {
                // Split by headers worked, group into appropriately sized chunks
                var currentChunk = new StringBuilder();
                
                foreach (var section in headerSplits)
                {
                    if (currentChunk.Length + section.Length > CHUNK_SIZE && currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString());
                        currentChunk.Clear();
                    }
                    currentChunk.Append(section);
                }
                
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString());
                }
            }
            else
            {
                // Fallback to simple character-based chunking
                for (int i = 0; i < htmlContent.Length; i += CHUNK_SIZE)
                {
                    int chunkLength = Math.Min(CHUNK_SIZE, htmlContent.Length - i);
                    chunks.Add(htmlContent.Substring(i, chunkLength));
                }
            }
            
            return chunks.Count > 0 ? chunks : new List<string> { htmlContent };
        }
        
        /// <summary>
        /// Generates JavaScript for progressive content loading and intersection observer optimization.
        /// </summary>
        /// <returns>JavaScript code for progressive loading functionality</returns>
        private static string GetProgressiveLoadingScript()
        {
            return @"
        (function() {
            'use strict';
            
            // Progressive loading with Intersection Observer for performance
            const observerOptions = {
                root: null,
                rootMargin: '100px',
                threshold: 0.1
            };
            
            const observer = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        const element = entry.target;
                        const contentIndex = element.dataset.contentIndex;
                        
                        if (contentIndex && element.classList.contains('loading-chunk')) {
                            loadChunkContent(element, contentIndex);
                            observer.unobserve(element);
                        }
                    }
                });
            }, observerOptions);
            
            function loadChunkContent(element, index) {
                try {
                    const dataScript = document.getElementById('chunk-data-' + index);
                    if (dataScript) {
                        const content = dataScript.textContent;
                        
                        // Replace loading content with actual content
                        element.innerHTML = content;
                        element.classList.remove('loading-chunk');
                        element.classList.add('content-visible');
                        
                        // Clean up data script
                        dataScript.remove();
                    }
                } catch (error) {
                    console.warn('Failed to load chunk content:', error);
                    // Fallback: try to load content directly without encoding
                    try {
                        const dataScript = document.getElementById('chunk-data-' + index);
                        if (dataScript && dataScript.textContent) {
                            element.innerHTML = dataScript.textContent;
                            element.classList.remove('loading-chunk');
                            element.classList.add('content-visible');
                            dataScript.remove();
                        } else {
                            element.innerHTML = '<div style=""color: var(--text-color); text-align: center; padding: 20px;"">Failed to load content section</div>';
                        }
                    } catch (fallbackError) {
                        console.error('Fallback loading also failed:', fallbackError);
                        element.innerHTML = '<div style=""color: var(--text-color); text-align: center; padding: 20px;"">Failed to load content section</div>';
                    }
                }
            }
            
            // Observe all loading chunks
            document.querySelectorAll('.loading-chunk').forEach(chunk => {
                observer.observe(chunk);
            });
            
            // Fallback: load all remaining chunks after 5 seconds if not loaded
            setTimeout(() => {
                document.querySelectorAll('.loading-chunk').forEach(chunk => {
                    const contentIndex = chunk.dataset.contentIndex;
                    if (contentIndex) {
                        loadChunkContent(chunk, contentIndex);
                        observer.unobserve(chunk);
                    }
                });
            }, 5000);
        })();
        ";
        }

        #region Private Helper Methods

        /// <summary>
        /// Generates an HTML document for empty or null content with appropriate theme styling.
        /// </summary>
        /// <param name="isDarkMode">Whether to apply dark mode styling</param>
        /// <returns>Complete HTML document showing "No content to preview" message</returns>
        private static string GenerateEmptyDocument(bool isDarkMode)
        {
            string css = isDarkMode ? _darkModeCSS : _lightModeCSS;
            string backgroundColor = isDarkMode ? "#2d2d30" : "#ffffff";
            string textColor = isDarkMode ? "#f1f1f1" : "#333333";

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Markdown Preview</title>
    <style>
{css}
    </style>
</head>
<body>
    <div class=""markdown-body"">
        <div style=""text-align: center; padding: 50px; color: {textColor}; background-color: {backgroundColor};"">
            <p><em>No content to preview</em></p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Generates an HTML document displaying an error message when markdown conversion fails.
        /// </summary>
        /// <param name="errorMessage">The error message to display (will be HTML-encoded for safety)</param>
        /// <param name="isDarkMode">Whether to apply dark mode styling</param>
        /// <returns>Complete HTML document with error styling and safe error message display</returns>
        private static string GenerateErrorDocument(string errorMessage, bool isDarkMode)
        {
            string css = isDarkMode ? _darkModeCSS : _lightModeCSS;
            string backgroundColor = isDarkMode ? "#2d2d30" : "#ffffff";
            string errorColor = isDarkMode ? "#ff6b6b" : "#d32f2f";

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Markdown Preview - Error</title>
    <style>
{css}
    </style>
</head>
<body>
    <div class=""markdown-body"">
        <div style=""padding: 20px; border: 1px solid {errorColor}; margin: 20px; background-color: {backgroundColor}; color: {errorColor};"">
            <h3>Error converting markdown:</h3>
            <p>{System.Net.WebUtility.HtmlEncode(errorMessage)}</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Generates comprehensive CSS styling for dark mode markdown rendering.
        /// Uses GitHub-inspired dark theme colors with high contrast for accessibility.
        /// </summary>
        /// <returns>CSS string with dark theme styling for all markdown elements</returns>
        private static string GenerateDarkModeCSS()
        {
            return @"
        html, body {
            margin: 0;
            padding: 0;
            background-color: #2d2d30;
            height: 100%;
        }
        
        .markdown-body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            font-size: 14px;
            line-height: 1.6;
            color: #f1f1f1;
            background-color: #2d2d30;
            padding: 20px;
            max-width: none;
            margin: 0;
            word-wrap: break-word;
            min-height: 100vh;
            box-sizing: border-box;
        }

        /* Headers */
        .markdown-body h1, .markdown-body h2, .markdown-body h3, 
        .markdown-body h4, .markdown-body h5, .markdown-body h6 {
            margin-top: 24px;
            margin-bottom: 16px;
            font-weight: 600;
            line-height: 1.25;
            color: #ffffff;
        }

        .markdown-body h1 { font-size: 2em; border-bottom: 1px solid #444; padding-bottom: 8px; }
        .markdown-body h2 { font-size: 1.5em; border-bottom: 1px solid #444; padding-bottom: 8px; }
        .markdown-body h3 { font-size: 1.25em; }
        .markdown-body h4 { font-size: 1em; }
        .markdown-body h5 { font-size: 0.875em; }
        .markdown-body h6 { font-size: 0.85em; color: #b3b3b3; }

        /* Paragraphs and text */
        .markdown-body p {
            margin-top: 0;
            margin-bottom: 16px;
        }

        .markdown-body strong {
            font-weight: 600;
            color: #ffffff;
        }

        .markdown-body em {
            font-style: italic;
        }

        /* Links */
        .markdown-body a {
            color: #4fc3f7;
            text-decoration: none;
        }

        .markdown-body a:hover {
            color: #81d4fa;
            text-decoration: underline;
        }

        /* Lists */
        .markdown-body ul, .markdown-body ol {
            margin-top: 0;
            margin-bottom: 16px;
            padding-left: 2em;
        }

        .markdown-body li {
            margin-bottom: 0.25em;
        }

        .markdown-body li > p {
            margin-top: 16px;
        }

        /* Code */
        .markdown-body code {
            background-color: #404040;
            color: #e6e6e6;
            padding: 0.2em 0.4em;
            margin: 0;
            font-size: 85%;
            border-radius: 3px;
            font-family: 'Consolas', 'Monaco', 'Lucida Console', monospace;
        }

        .markdown-body pre {
            background-color: #1e1e1e;
            color: #e6e6e6;
            padding: 16px;
            overflow: auto;
            font-size: 85%;
            line-height: 1.45;
            border-radius: 6px;
            margin-bottom: 16px;
            border: 1px solid #404040;
        }

        .markdown-body pre code {
            background-color: transparent;
            padding: 0;
            margin: 0;
            font-size: 100%;
            word-break: normal;
            white-space: pre;
            border: 0;
        }

        /* Blockquotes */
        .markdown-body blockquote {
            margin: 0 0 16px 0;
            padding: 0 1em;
            color: #b3b3b3;
            border-left: 0.25em solid #555;
            background-color: #383838;
            border-radius: 0 3px 3px 0;
        }

        .markdown-body blockquote > :first-child {
            margin-top: 0;
        }

        .markdown-body blockquote > :last-child {
            margin-bottom: 0;
        }

        /* Tables */
        .markdown-body table {
            border-collapse: collapse;
            border-spacing: 0;
            width: 100%;
            margin-bottom: 16px;
            border: 1px solid #555;
        }

        .markdown-body table th,
        .markdown-body table td {
            padding: 6px 13px;
            border: 1px solid #555;
            text-align: left;
        }

        .markdown-body table th {
            font-weight: 600;
            background-color: #404040;
            color: #ffffff;
        }

        .markdown-body table tr:nth-child(even) {
            background-color: #353535;
        }

        .markdown-body table tr:hover {
            background-color: #404040;
        }

        /* Horizontal rules */
        .markdown-body hr {
            height: 0.25em;
            padding: 0;
            margin: 24px 0;
            background-color: #555;
            border: 0;
        }

        /* Images */
        .markdown-body img {
            max-width: 100%;
            height: auto;
            border-radius: 3px;
        }

        /* Task lists */
        .markdown-body .task-list-item {
            list-style-type: none;
        }

        .markdown-body .task-list-item-checkbox {
            margin: 0 0.2em 0.25em -1.6em;
            vertical-align: middle;
        }

        /* Print-optimized CSS for PDF output */
        @media print {
            html, body {
                background-color: white !important;
                color: black !important;
                font-size: 12pt;
                line-height: 1.4;
                margin: 0;
                padding: 0;
            }
            
            .markdown-body {
                background-color: white !important;
                color: black !important;
                font-size: 12pt;
                line-height: 1.4;
                padding: 20px;
                min-height: auto;
                box-shadow: none;
            }
            
            /* Headers with proper spacing */
            .markdown-body h1, .markdown-body h2, .markdown-body h3,
            .markdown-body h4, .markdown-body h5, .markdown-body h6 {
                color: black !important;
                page-break-after: avoid;
                margin-top: 18pt;
                margin-bottom: 12pt;
            }
            
            .markdown-body h1 {
                font-size: 18pt;
                border-bottom: 2pt solid black;
                padding-bottom: 6pt;
            }
            
            .markdown-body h2 {
                font-size: 16pt;
                border-bottom: 1pt solid black;
                padding-bottom: 4pt;
            }
            
            .markdown-body h3 {
                font-size: 14pt;
            }
            
            .markdown-body h4, .markdown-body h5, .markdown-body h6 {
                font-size: 12pt;
            }
            
            /* Text elements */
            .markdown-body p {
                color: black !important;
                margin-bottom: 12pt;
                orphans: 3;
                widows: 3;
            }
            
            .markdown-body strong {
                color: black !important;
                font-weight: bold;
            }
            
            .markdown-body em {
                font-style: italic;
            }
            
            /* Links - show URLs in print */
            .markdown-body a {
                color: black !important;
                text-decoration: underline;
            }
            
            /* Code blocks - prevent page breaks */
            .markdown-body code {
                background-color: #f5f5f5 !important;
                color: black !important;
                border: 1pt solid #ccc;
                padding: 2pt 4pt;
                font-family: 'Courier New', Courier, monospace;
                font-size: 10pt;
            }
            
            .markdown-body pre {
                background-color: #f9f9f9 !important;
                color: black !important;
                border: 1pt solid #ccc;
                padding: 12pt;
                margin-bottom: 12pt;
                page-break-inside: avoid;
                font-family: 'Courier New', Courier, monospace;
                font-size: 10pt;
                line-height: 1.3;
                overflow: visible;
                white-space: pre-wrap;
            }
            
            .markdown-body pre code {
                background-color: transparent !important;
                border: none;
                padding: 0;
            }
            
            /* Blockquotes - prevent page breaks */
            .markdown-body blockquote {
                color: #333 !important;
                background-color: #f9f9f9 !important;
                border-left: 4pt solid #ccc;
                margin: 12pt 0;
                padding: 8pt 12pt;
                page-break-inside: avoid;
                font-style: italic;
            }
            
            /* Tables - optimize for print */
            .markdown-body table {
                border-collapse: collapse;
                width: 100%;
                margin-bottom: 12pt;
                border: 1pt solid black;
                page-break-inside: avoid;
            }
            
            .markdown-body table th,
            .markdown-body table td {
                border: 1pt solid black;
                padding: 6pt 8pt;
                text-align: left;
                color: black !important;
                background-color: white !important;
            }
            
            .markdown-body table th {
                background-color: #f0f0f0 !important;
                font-weight: bold;
            }
            
            .markdown-body table tr:nth-child(even) {
                background-color: #f9f9f9 !important;
            }
            
            .markdown-body table tr:hover {
                background-color: inherit !important;
            }
            
            /* Lists */
            .markdown-body ul, .markdown-body ol {
                margin-bottom: 12pt;
            }
            
            .markdown-body li {
                margin-bottom: 4pt;
            }
            
            /* Horizontal rules */
            .markdown-body hr {
                background-color: black !important;
                border: none;
                height: 1pt;
                margin: 18pt 0;
                page-break-after: avoid;
            }
            
            /* Images */
            .markdown-body img {
                max-width: 100%;
                height: auto;
                page-break-inside: avoid;
                margin: 12pt 0;
            }
            
            /* Hide UI elements that shouldn't appear in print */
            .no-print {
                display: none !important;
            }
            
            /* Task lists */
            .markdown-body .task-list-item {
                list-style-type: none;
            }
            
            .markdown-body .task-list-item-checkbox {
                margin-right: 6pt;
            }
            
            /* Page break helpers */
            .page-break-before {
                page-break-before: always;
            }
            
            .page-break-after {
                page-break-after: always;
            }
            
            .no-page-break {
                page-break-inside: avoid;
            }
        }";
        }

        /// <summary>
        /// Generates comprehensive CSS styling for light mode markdown rendering.
        /// Uses GitHub-inspired light theme colors with optimal readability.
        /// </summary>
        /// <returns>CSS string with light theme styling for all markdown elements</returns>
        private static string GenerateLightModeCSS()
        {
            return @"
        html, body {
            margin: 0;
            padding: 0;
            background-color: #ffffff;
            height: 100%;
        }
        
        .markdown-body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            font-size: 14px;
            line-height: 1.6;
            color: #333333;
            background-color: #ffffff;
            padding: 20px;
            max-width: none;
            margin: 0;
            word-wrap: break-word;
            min-height: 100vh;
            box-sizing: border-box;
        }

        /* Headers */
        .markdown-body h1, .markdown-body h2, .markdown-body h3, 
        .markdown-body h4, .markdown-body h5, .markdown-body h6 {
            margin-top: 24px;
            margin-bottom: 16px;
            font-weight: 600;
            line-height: 1.25;
            color: #1a1a1a;
        }

        .markdown-body h1 { font-size: 2em; border-bottom: 1px solid #eaecef; padding-bottom: 8px; }
        .markdown-body h2 { font-size: 1.5em; border-bottom: 1px solid #eaecef; padding-bottom: 8px; }
        .markdown-body h3 { font-size: 1.25em; }
        .markdown-body h4 { font-size: 1em; }
        .markdown-body h5 { font-size: 0.875em; }
        .markdown-body h6 { font-size: 0.85em; color: #6a737d; }

        /* Paragraphs and text */
        .markdown-body p {
            margin-top: 0;
            margin-bottom: 16px;
        }

        .markdown-body strong {
            font-weight: 600;
            color: #1a1a1a;
        }

        .markdown-body em {
            font-style: italic;
        }

        /* Links */
        .markdown-body a {
            color: #0366d6;
            text-decoration: none;
        }

        .markdown-body a:hover {
            color: #0366d6;
            text-decoration: underline;
        }

        /* Lists */
        .markdown-body ul, .markdown-body ol {
            margin-top: 0;
            margin-bottom: 16px;
            padding-left: 2em;
        }

        .markdown-body li {
            margin-bottom: 0.25em;
        }

        .markdown-body li > p {
            margin-top: 16px;
        }

        /* Code */
        .markdown-body code {
            background-color: #f6f8fa;
            color: #d73a49;
            padding: 0.2em 0.4em;
            margin: 0;
            font-size: 85%;
            border-radius: 3px;
            font-family: 'Consolas', 'Monaco', 'Lucida Console', monospace;
        }

        .markdown-body pre {
            background-color: #f6f8fa;
            color: #24292e;
            padding: 16px;
            overflow: auto;
            font-size: 85%;
            line-height: 1.45;
            border-radius: 6px;
            margin-bottom: 16px;
            border: 1px solid #e1e4e8;
        }

        .markdown-body pre code {
            background-color: transparent;
            color: inherit;
            padding: 0;
            margin: 0;
            font-size: 100%;
            word-break: normal;
            white-space: pre;
            border: 0;
        }

        /* Blockquotes */
        .markdown-body blockquote {
            margin: 0 0 16px 0;
            padding: 0 1em;
            color: #6a737d;
            border-left: 0.25em solid #dfe2e5;
            background-color: #f8f9fa;
            border-radius: 0 3px 3px 0;
        }

        .markdown-body blockquote > :first-child {
            margin-top: 0;
        }

        .markdown-body blockquote > :last-child {
            margin-bottom: 0;
        }

        /* Tables */
        .markdown-body table {
            border-collapse: collapse;
            border-spacing: 0;
            width: 100%;
            margin-bottom: 16px;
            border: 1px solid #d0d7de;
        }

        .markdown-body table th,
        .markdown-body table td {
            padding: 6px 13px;
            border: 1px solid #d0d7de;
            text-align: left;
        }

        .markdown-body table th {
            font-weight: 600;
            background-color: #f6f8fa;
            color: #24292e;
        }

        .markdown-body table tr:nth-child(even) {
            background-color: #f6f8fa;
        }

        .markdown-body table tr:hover {
            background-color: #f1f3f4;
        }

        /* Horizontal rules */
        .markdown-body hr {
            height: 0.25em;
            padding: 0;
            margin: 24px 0;
            background-color: #e1e4e8;
            border: 0;
        }

        /* Images */
        .markdown-body img {
            max-width: 100%;
            height: auto;
            border-radius: 3px;
        }

        /* Task lists */
        .markdown-body .task-list-item {
            list-style-type: none;
        }

        .markdown-body .task-list-item-checkbox {
            margin: 0 0.2em 0.25em -1.6em;
            vertical-align: middle;
        }

        /* Print-optimized CSS for PDF output */
        @media print {
            html, body {
                background-color: white !important;
                color: black !important;
                font-size: 12pt;
                line-height: 1.4;
                margin: 0;
                padding: 0;
            }
            
            .markdown-body {
                background-color: white !important;
                color: black !important;
                font-size: 12pt;
                line-height: 1.4;
                padding: 20px;
                min-height: auto;
                box-shadow: none;
            }
            
            /* Headers with proper spacing */
            .markdown-body h1, .markdown-body h2, .markdown-body h3,
            .markdown-body h4, .markdown-body h5, .markdown-body h6 {
                color: black !important;
                page-break-after: avoid;
                margin-top: 18pt;
                margin-bottom: 12pt;
            }
            
            .markdown-body h1 {
                font-size: 18pt;
                border-bottom: 2pt solid black;
                padding-bottom: 6pt;
            }
            
            .markdown-body h2 {
                font-size: 16pt;
                border-bottom: 1pt solid black;
                padding-bottom: 4pt;
            }
            
            .markdown-body h3 {
                font-size: 14pt;
            }
            
            .markdown-body h4, .markdown-body h5, .markdown-body h6 {
                font-size: 12pt;
            }
            
            /* Text elements */
            .markdown-body p {
                color: black !important;
                margin-bottom: 12pt;
                orphans: 3;
                widows: 3;
            }
            
            .markdown-body strong {
                color: black !important;
                font-weight: bold;
            }
            
            .markdown-body em {
                font-style: italic;
            }
            
            /* Links - show URLs in print */
            .markdown-body a {
                color: black !important;
                text-decoration: underline;
            }
            
            /* Code blocks - prevent page breaks */
            .markdown-body code {
                background-color: #f5f5f5 !important;
                color: black !important;
                border: 1pt solid #ccc;
                padding: 2pt 4pt;
                font-family: 'Courier New', Courier, monospace;
                font-size: 10pt;
            }
            
            .markdown-body pre {
                background-color: #f9f9f9 !important;
                color: black !important;
                border: 1pt solid #ccc;
                padding: 12pt;
                margin-bottom: 12pt;
                page-break-inside: avoid;
                font-family: 'Courier New', Courier, monospace;
                font-size: 10pt;
                line-height: 1.3;
                overflow: visible;
                white-space: pre-wrap;
            }
            
            .markdown-body pre code {
                background-color: transparent !important;
                border: none;
                padding: 0;
            }
            
            /* Blockquotes - prevent page breaks */
            .markdown-body blockquote {
                color: #333 !important;
                background-color: #f9f9f9 !important;
                border-left: 4pt solid #ccc;
                margin: 12pt 0;
                padding: 8pt 12pt;
                page-break-inside: avoid;
                font-style: italic;
            }
            
            /* Tables - optimize for print */
            .markdown-body table {
                border-collapse: collapse;
                width: 100%;
                margin-bottom: 12pt;
                border: 1pt solid black;
                page-break-inside: avoid;
            }
            
            .markdown-body table th,
            .markdown-body table td {
                border: 1pt solid black;
                padding: 6pt 8pt;
                text-align: left;
                color: black !important;
                background-color: white !important;
            }
            
            .markdown-body table th {
                background-color: #f0f0f0 !important;
                font-weight: bold;
            }
            
            .markdown-body table tr:nth-child(even) {
                background-color: #f9f9f9 !important;
            }
            
            .markdown-body table tr:hover {
                background-color: inherit !important;
            }
            
            /* Lists */
            .markdown-body ul, .markdown-body ol {
                margin-bottom: 12pt;
            }
            
            .markdown-body li {
                margin-bottom: 4pt;
            }
            
            /* Horizontal rules */
            .markdown-body hr {
                background-color: black !important;
                border: none;
                height: 1pt;
                margin: 18pt 0;
                page-break-after: avoid;
            }
            
            /* Images */
            .markdown-body img {
                max-width: 100%;
                height: auto;
                page-break-inside: avoid;
                margin: 12pt 0;
            }
            
            /* Hide UI elements that shouldn't appear in print */
            .no-print {
                display: none !important;
            }
            
            /* Task lists */
            .markdown-body .task-list-item {
                list-style-type: none;
            }
            
            .markdown-body .task-list-item-checkbox {
                margin-right: 6pt;
            }
            
            /* Page break helpers */
            .page-break-before {
                page-break-before: always;
            }
            
            .page-break-after {
                page-break-after: always;
            }
            
            .no-page-break {
                page-break-inside: avoid;
            }
        }";
        }

        /// <summary>
        /// Generates an HTML document for empty or null content with universal CSS theme support.
        /// </summary>
        /// <param name="isDarkMode">Whether to start with dark mode theme</param>
        /// <returns>Complete HTML document showing "No content to preview" message with theme switching support</returns>
        private static string GenerateEmptyUniversalDocument(bool isDarkMode)
        {
            string themeAttribute = isDarkMode ? "dark" : "light";

            return $@"<!DOCTYPE html>
<html lang=""en"" data-theme=""{themeAttribute}"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Markdown Preview</title>
    <style>
{_universalCSS}
    </style>
</head>
<body data-theme=""{themeAttribute}"">
    <div class=""markdown-body"">
        <div style=""text-align: center; padding: 50px; color: var(--text-color);"">
            <p><em>No content to preview</em></p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Generates an HTML document displaying an error message with universal CSS theme support.
        /// </summary>
        /// <param name="errorMessage">The error message to display (will be HTML-encoded for safety)</param>
        /// <param name="isDarkMode">Whether to start with dark mode theme</param>
        /// <returns>Complete HTML document with error styling and theme switching support</returns>
        private static string GenerateErrorUniversalDocument(string errorMessage, bool isDarkMode)
        {
            string themeAttribute = isDarkMode ? "dark" : "light";

            return $@"<!DOCTYPE html>
<html lang=""en"" data-theme=""{themeAttribute}"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Markdown Preview - Error</title>
    <style>
{_universalCSS}
        .error-container {{
            padding: 20px; 
            border: 1px solid #ff6b6b; 
            margin: 20px; 
            background-color: var(--bg-color); 
            color: #ff6b6b;
            border-radius: 3px;
            transition: background-color 0.3s ease;
        }}
    </style>
</head>
<body data-theme=""{themeAttribute}"">
    <div class=""markdown-body"">
        <div class=""error-container"">
            <h3>Error converting markdown:</h3>
            <p>{System.Net.WebUtility.HtmlEncode(errorMessage)}</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Generates universal CSS with custom properties (CSS variables) for dynamic theme switching.
        /// This approach allows instant theme changes via JavaScript without page reloads.
        /// </summary>
        /// <returns>CSS string with theme-agnostic styling using custom properties</returns>
        private static string GenerateUniversalCSS()
        {
            return @"
        :root {
            /* CSS Custom Properties for Dynamic Theme Switching */
            /* Default to dark theme values */
            --bg-color: #2d2d30;
            --text-color: #f1f1f1;
            --heading-color: #ffffff;
            --link-color: #4fc3f7;
            --link-hover-color: #81d4fa;
            --code-bg: #404040;
            --code-color: #e6e6e6;
            --pre-bg: #1e1e1e;
            --pre-border: #404040;
            --blockquote-color: #b3b3b3;
            --blockquote-border: #555;
            --blockquote-bg: #383838;
            --table-border: #555;
            --table-header-bg: #404040;
            --table-header-color: #ffffff;
            --table-row-even-bg: #353535;
            --table-row-hover-bg: #404040;
            --hr-color: #555;
            --h6-color: #b3b3b3;
            --border-color: #444;
            --strong-color: #ffffff;
            
            /* Light theme class overrides */
        }
        
        [data-theme=""light""] {
            --bg-color: #ffffff;
            --text-color: #333333;
            --heading-color: #1a1a1a;
            --link-color: #0366d6;
            --link-hover-color: #0366d6;
            --code-bg: #f6f8fa;
            --code-color: #d73a49;
            --pre-bg: #f6f8fa;
            --pre-border: #e1e4e8;
            --blockquote-color: #6a737d;
            --blockquote-border: #dfe2e5;
            --blockquote-bg: #f8f9fa;
            --table-border: #d0d7de;
            --table-header-bg: #f6f8fa;
            --table-header-color: #24292e;
            --table-row-even-bg: #f6f8fa;
            --table-row-hover-bg: #f1f3f4;
            --hr-color: #e1e4e8;
            --h6-color: #6a737d;
            --border-color: #eaecef;
            --strong-color: #1a1a1a;
        }
        
        html, body {
            margin: 0;
            padding: 0;
            background-color: var(--bg-color);
            height: 100%;
            /* Smooth transitions for theme switching */
            transition: background-color 0.3s ease, color 0.3s ease;
        }
        
        .markdown-body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            font-size: 14px;
            line-height: 1.6;
            color: var(--text-color);
            background-color: var(--bg-color);
            padding: 20px;
            max-width: none;
            margin: 0;
            word-wrap: break-word;
            min-height: 100vh;
            box-sizing: border-box;
            /* Smooth transitions */
            transition: background-color 0.3s ease, color 0.3s ease;
        }

        /* Headers */
        .markdown-body h1, .markdown-body h2, .markdown-body h3, 
        .markdown-body h4, .markdown-body h5, .markdown-body h6 {
            margin-top: 24px;
            margin-bottom: 16px;
            font-weight: 600;
            line-height: 1.25;
            color: var(--heading-color);
            transition: color 0.3s ease;
        }

        .markdown-body h1 { 
            font-size: 2em; 
            border-bottom: 1px solid var(--border-color); 
            padding-bottom: 8px; 
            transition: border-color 0.3s ease;
        }
        .markdown-body h2 { 
            font-size: 1.5em; 
            border-bottom: 1px solid var(--border-color); 
            padding-bottom: 8px; 
            transition: border-color 0.3s ease;
        }
        .markdown-body h3 { font-size: 1.25em; }
        .markdown-body h4 { font-size: 1em; }
        .markdown-body h5 { font-size: 0.875em; }
        .markdown-body h6 { font-size: 0.85em; color: var(--h6-color); transition: color 0.3s ease; }

        /* Paragraphs and text */
        .markdown-body p {
            margin-top: 0;
            margin-bottom: 16px;
        }

        .markdown-body strong {
            font-weight: 600;
            color: var(--strong-color);
            transition: color 0.3s ease;
        }

        .markdown-body em {
            font-style: italic;
        }

        /* Links */
        .markdown-body a {
            color: var(--link-color);
            text-decoration: none;
            transition: color 0.3s ease;
        }

        .markdown-body a:hover {
            color: var(--link-hover-color);
            text-decoration: underline;
        }

        /* Lists */
        .markdown-body ul, .markdown-body ol {
            margin-top: 0;
            margin-bottom: 16px;
            padding-left: 2em;
        }

        .markdown-body li {
            margin-bottom: 0.25em;
        }

        .markdown-body li > p {
            margin-top: 16px;
        }

        /* Code */
        .markdown-body code {
            background-color: var(--code-bg);
            color: var(--code-color);
            padding: 0.2em 0.4em;
            margin: 0;
            font-size: 85%;
            border-radius: 3px;
            font-family: 'Consolas', 'Monaco', 'Lucida Console', monospace;
            transition: background-color 0.3s ease, color 0.3s ease;
        }

        .markdown-body pre {
            background-color: var(--pre-bg);
            color: var(--code-color);
            padding: 16px;
            overflow: auto;
            font-size: 85%;
            line-height: 1.45;
            border-radius: 6px;
            margin-bottom: 16px;
            border: 1px solid var(--pre-border);
            transition: background-color 0.3s ease, color 0.3s ease, border-color 0.3s ease;
        }

        .markdown-body pre code {
            background-color: transparent;
            padding: 0;
            margin: 0;
            font-size: 100%;
            word-break: normal;
            white-space: pre;
            border: 0;
        }

        /* Blockquotes */
        .markdown-body blockquote {
            margin: 0 0 16px 0;
            padding: 0 1em;
            color: var(--blockquote-color);
            border-left: 0.25em solid var(--blockquote-border);
            background-color: var(--blockquote-bg);
            border-radius: 0 3px 3px 0;
            transition: color 0.3s ease, border-color 0.3s ease, background-color 0.3s ease;
        }

        .markdown-body blockquote > :first-child {
            margin-top: 0;
        }

        .markdown-body blockquote > :last-child {
            margin-bottom: 0;
        }

        /* Tables */
        .markdown-body table {
            border-collapse: collapse;
            border-spacing: 0;
            width: 100%;
            margin-bottom: 16px;
            border: 1px solid var(--table-border);
            transition: border-color 0.3s ease;
        }

        .markdown-body table th,
        .markdown-body table td {
            padding: 6px 13px;
            border: 1px solid var(--table-border);
            text-align: left;
            transition: border-color 0.3s ease;
        }

        .markdown-body table th {
            font-weight: 600;
            background-color: var(--table-header-bg);
            color: var(--table-header-color);
            transition: background-color 0.3s ease, color 0.3s ease;
        }

        .markdown-body table tr:nth-child(even) {
            background-color: var(--table-row-even-bg);
            transition: background-color 0.3s ease;
        }

        .markdown-body table tr:hover {
            background-color: var(--table-row-hover-bg);
        }

        /* Horizontal rules */
        .markdown-body hr {
            height: 0.25em;
            padding: 0;
            margin: 24px 0;
            background-color: var(--hr-color);
            border: 0;
            transition: background-color 0.3s ease;
        }

        /* Images */
        .markdown-body img {
            max-width: 100%;
            height: auto;
            border-radius: 3px;
        }

        /* Task lists */
        .markdown-body .task-list-item {
            list-style-type: none;
        }

        .markdown-body .task-list-item-checkbox {
            margin: 0 0.2em 0.25em -1.6em;
            vertical-align: middle;
        }

        /* Print-optimized CSS for PDF output */
        @media print {
            html, body {
                background-color: white !important;
                color: black !important;
                font-size: 12pt;
                line-height: 1.4;
                margin: 0;
                padding: 0;
            }
            
            .markdown-body {
                background-color: white !important;
                color: black !important;
                font-size: 12pt;
                line-height: 1.4;
                padding: 20px;
                min-height: auto;
                box-shadow: none;
            }
            
            /* Headers with proper spacing */
            .markdown-body h1, .markdown-body h2, .markdown-body h3,
            .markdown-body h4, .markdown-body h5, .markdown-body h6 {
                color: black !important;
                page-break-after: avoid;
                margin-top: 18pt;
                margin-bottom: 12pt;
            }
            
            .markdown-body h1 {
                font-size: 18pt;
                border-bottom: 2pt solid black;
                padding-bottom: 6pt;
            }
            
            .markdown-body h2 {
                font-size: 16pt;
                border-bottom: 1pt solid black;
                padding-bottom: 4pt;
            }
            
            .markdown-body h3 {
                font-size: 14pt;
            }
            
            .markdown-body h4, .markdown-body h5, .markdown-body h6 {
                font-size: 12pt;
            }
            
            /* Text elements */
            .markdown-body p {
                color: black !important;
                margin-bottom: 12pt;
                orphans: 3;
                widows: 3;
            }
            
            .markdown-body strong {
                color: black !important;
                font-weight: bold;
            }
            
            .markdown-body em {
                font-style: italic;
            }
            
            /* Links - show URLs in print */
            .markdown-body a {
                color: black !important;
                text-decoration: underline;
            }
            
            /* Code blocks - prevent page breaks */
            .markdown-body code {
                background-color: #f5f5f5 !important;
                color: black !important;
                border: 1pt solid #ccc;
                padding: 2pt 4pt;
                font-family: 'Courier New', Courier, monospace;
                font-size: 10pt;
            }
            
            .markdown-body pre {
                background-color: #f9f9f9 !important;
                color: black !important;
                border: 1pt solid #ccc;
                padding: 12pt;
                margin-bottom: 12pt;
                page-break-inside: avoid;
                font-family: 'Courier New', Courier, monospace;
                font-size: 10pt;
                line-height: 1.3;
                overflow: visible;
                white-space: pre-wrap;
            }
            
            .markdown-body pre code {
                background-color: transparent !important;
                border: none;
                padding: 0;
            }
            
            /* Blockquotes - prevent page breaks */
            .markdown-body blockquote {
                color: #333 !important;
                background-color: #f9f9f9 !important;
                border-left: 4pt solid #ccc;
                margin: 12pt 0;
                padding: 8pt 12pt;
                page-break-inside: avoid;
                font-style: italic;
            }
            
            /* Tables - optimize for print */
            .markdown-body table {
                border-collapse: collapse;
                width: 100%;
                margin-bottom: 12pt;
                border: 1pt solid black;
                page-break-inside: avoid;
            }
            
            .markdown-body table th,
            .markdown-body table td {
                border: 1pt solid black;
                padding: 6pt 8pt;
                text-align: left;
                color: black !important;
                background-color: white !important;
            }
            
            .markdown-body table th {
                background-color: #f0f0f0 !important;
                font-weight: bold;
            }
            
            .markdown-body table tr:nth-child(even) {
                background-color: #f9f9f9 !important;
            }
            
            .markdown-body table tr:hover {
                background-color: inherit !important;
            }
            
            /* Lists */
            .markdown-body ul, .markdown-body ol {
                margin-bottom: 12pt;
            }
            
            .markdown-body li {
                margin-bottom: 4pt;
            }
            
            /* Horizontal rules */
            .markdown-body hr {
                background-color: black !important;
                border: none;
                height: 1pt;
                margin: 18pt 0;
                page-break-after: avoid;
            }
            
            /* Images */
            .markdown-body img {
                max-width: 100%;
                height: auto;
                page-break-inside: avoid;
                margin: 12pt 0;
            }
            
            /* Hide UI elements that shouldn't appear in print */
            .no-print {
                display: none !important;
            }
            
            /* Task lists */
            .markdown-body .task-list-item {
                list-style-type: none;
            }
            
            .markdown-body .task-list-item-checkbox {
                margin-right: 6pt;
            }
            
            /* Page break helpers */
            .page-break-before {
                page-break-before: always;
            }
            
            .page-break-after {
                page-break-after: always;
            }
            
            .no-page-break {
                page-break-inside: avoid;
            }
        }";
        }

        #endregion
    }
}