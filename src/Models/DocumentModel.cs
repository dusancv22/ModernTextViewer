using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModernTextViewer.src.Services;

namespace ModernTextViewer.src.Models
{
    public class DocumentModel
    {
        private string content = string.Empty;
        private string filePath = string.Empty;
        private bool isDirty;
        private List<HyperlinkModel> hyperlinks = new List<HyperlinkModel>();
        
        // Streaming support properties
        private bool isStreamingMode = false;
        private StreamingFileInfo? streamingFileInfo;
        private long fileSize;
        private long estimatedLineCount;

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

        protected virtual void OnFilePathChanged()
        {
            // For future use (e.g., updating window title)
        }

        protected virtual void OnDirtyChanged()
        {
            // For future use (e.g., updating UI to show unsaved changes)
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

        // Streaming mode properties and methods
        public bool IsStreamingMode 
        { 
            get => isStreamingMode;
            private set => isStreamingMode = value;
        }

        public StreamingFileInfo? StreamingFileInfo 
        { 
            get => streamingFileInfo;
            set => streamingFileInfo = value;
        }

        public long FileSize 
        { 
            get => fileSize;
            set => fileSize = value;
        }

        public long EstimatedLineCount 
        { 
            get => estimatedLineCount;
            set => estimatedLineCount = value;
        }

        public void EnableStreamingMode(StreamingFileInfo fileInfo)
        {
            IsStreamingMode = true;
            StreamingFileInfo = fileInfo;
            FileSize = fileInfo.FileSize;
            EstimatedLineCount = fileInfo.EstimatedLineCount;
            
            // Clear content since we're streaming
            content = string.Empty;
        }

        public void DisableStreamingMode()
        {
            IsStreamingMode = false;
            StreamingFileInfo = null;
            FileSize = 0;
            EstimatedLineCount = 0;
        }

        public bool RequiresStreaming => StreamingFileInfo?.RequiresStreaming ?? false;

        // Helper method for streaming mode content management
        public void SetStreamingContent(string newContent, bool markDirty = true)
        {
            if (IsStreamingMode && markDirty)
            {
                IsDirty = true;
            }
            else if (!IsStreamingMode)
            {
                Content = newContent;
            }
        }
    }
}