using System;
using System.Text;
using Markdig;

namespace ModernTextViewer.src.Services
{
    /// <summary>
    /// Service for converting markdown to HTML with theme support
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
        /// </summary>
        /// <param name="markdownText">The markdown text to convert. Can be null or empty.</param>
        /// <param name="isDarkMode">Initial theme state. When true, starts with dark theme; when false, starts with light theme.</param>
        /// <returns>Complete HTML5 document with universal CSS and theme switching support</returns>
        /// <remarks>
        /// This method is designed for performance-optimized theme switching. The generated HTML:
        /// <list type="bullet">
        /// <item>Uses CSS custom properties (variables) for all theme colors</item>
        /// <item>Includes smooth transitions between theme changes</item>
        /// <item>Supports instant theme switching via JavaScript</item>
        /// <item>Maintains full compatibility with all markdown elements</item>
        /// <item>Automatically sets the initial theme via data-theme attribute</item>
        /// </list>
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
            catch (Exception ex)
            {
                return GenerateErrorUniversalDocument(ex.Message, isDarkMode);
            }
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
        }";
        }

        #endregion
    }
}