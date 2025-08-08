using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernTextViewer.src.Models
{
    public class DocumentModel
    {
        private string content = string.Empty;
        private string filePath = string.Empty;
        private bool isDirty;
        private bool isPreviewMode = false;
        private List<HyperlinkModel> hyperlinks = new List<HyperlinkModel>();

        public string FilePath 
        { 
            get => filePath;
            set
            {
                if (filePath != value)
                {
                    filePath = value;
                    OnFilePathChanged();
                }
            }
        }

        public bool IsDirty 
        { 
            get => isDirty;
            set
            {
                if (isDirty != value)
                {
                    isDirty = value;
                    OnDirtyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the document is currently in preview mode.
        /// Preview mode displays rendered markdown in a WebView2 control instead of raw text.
        /// Only supported for markdown files (.md, .markdown).
        /// </summary>
        /// <value>
        /// <c>true</c> if the document is in preview mode; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>
        /// Changing this property triggers the <see cref="OnPreviewModeChanged"/> virtual method.
        /// Preview mode is automatically disabled for non-markdown files.
        /// </remarks>
        public bool IsPreviewMode
        {
            get => isPreviewMode;
            set
            {
                if (isPreviewMode != value)
                {
                    isPreviewMode = value;
                    OnPreviewModeChanged();
                }
            }
        }

        public string Content
        {
            get => content;
            set
            {
                if (content != value)
                {
                    content = value;
                    IsDirty = true;
                }
            }
        }

        public void ResetDirty()
        {
            IsDirty = false;
        }

        /// <summary>
        /// Determines whether the current document supports preview mode based on its file extension.
        /// Preview mode is available for markdown files with .md or .markdown extensions.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the document supports preview mode; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method performs case-insensitive comparison of file extensions.
        /// Supported extensions:
        /// <list type="bullet">
        /// <item>.md</item>
        /// <item>.markdown</item>
        /// </list>
        /// Returns <c>false</c> if <see cref="FilePath"/> is null, empty, or has no extension.
        /// </remarks>
        /// <example>
        /// <code>
        /// var doc = new DocumentModel();
        /// doc.FilePath = "readme.md";
        /// bool canPreview = doc.SupportsPreview(); // Returns true
        /// 
        /// doc.FilePath = "notes.txt";
        /// bool canPreview2 = doc.SupportsPreview(); // Returns false
        /// </code>
        /// </example>
        public bool SupportsPreview()
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            string extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension))
                return false;

            // Convert to lowercase for case-insensitive comparison
            extension = extension.ToLowerInvariant();

            return extension == ".md" || extension == ".markdown";
        }

        protected virtual void OnFilePathChanged()
        {
            // For future use (e.g., updating window title)
        }

        protected virtual void OnDirtyChanged()
        {
            // For future use (e.g., updating UI to show unsaved changes)
        }

        /// <summary>
        /// Called when the <see cref="IsPreviewMode"/> property changes.
        /// This virtual method can be overridden in derived classes to handle preview mode state changes.
        /// </summary>
        /// <remarks>
        /// The default implementation is empty and serves as an extension point for future functionality
        /// such as updating UI to reflect the current preview/raw mode state.
        /// This method is called automatically when <see cref="IsPreviewMode"/> is set to a different value.
        /// </remarks>
        protected virtual void OnPreviewModeChanged()
        {
            // For future use (e.g., updating UI to show preview/raw mode)
        }

        public List<HyperlinkModel> Hyperlinks
        {
            get => hyperlinks;
            set
            {
                hyperlinks = value ?? new List<HyperlinkModel>();
                IsDirty = true;
            }
        }

        public void AddHyperlink(HyperlinkModel hyperlink)
        {
            if (hyperlink != null)
            {
                hyperlinks.Add(hyperlink);
                IsDirty = true;
            }
        }

        public void RemoveHyperlink(HyperlinkModel hyperlink)
        {
            if (hyperlink != null && hyperlinks.Remove(hyperlink))
            {
                IsDirty = true;
            }
        }

        public HyperlinkModel? GetHyperlinkAtPosition(int position)
        {
            return hyperlinks.FirstOrDefault(h => h.ContainsPosition(position));
        }

        public void UpdateHyperlinksAfterTextChange(int changeIndex, int changeLength)
        {
            // Remove hyperlinks that are completely within the deleted range
            if (changeLength < 0)
            {
                int deleteEndIndex = changeIndex - changeLength;
                hyperlinks.RemoveAll(h => 
                    (h.StartIndex >= changeIndex && h.EndIndex <= deleteEndIndex) || // Completely within deleted range
                    (h.StartIndex < changeIndex && h.EndIndex > changeIndex)); // Partially overlaps with deleted range
            }
            
            // Update positions of remaining hyperlinks
            foreach (var hyperlink in hyperlinks.ToList())
            {
                // If change is before hyperlink, adjust position
                if (changeIndex <= hyperlink.StartIndex)
                {
                    hyperlink.StartIndex += changeLength;
                }
                // If change is within hyperlink, adjust length
                else if (changeIndex < hyperlink.EndIndex)
                {
                    if (changeLength > 0)
                    {
                        // Insertion within hyperlink - expand it
                        hyperlink.Length += changeLength;
                    }
                    else
                    {
                        // Deletion within hyperlink - shrink it
                        hyperlink.Length += changeLength;
                        if (hyperlink.Length <= 0)
                        {
                            hyperlinks.Remove(hyperlink);
                        }
                    }
                }
            }
            
            // Final cleanup - remove any invalid hyperlinks
            hyperlinks.RemoveAll(h => h.StartIndex < 0 || h.Length <= 0);
        }
    }
}