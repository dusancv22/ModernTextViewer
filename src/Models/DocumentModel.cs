using System;
using System.Collections.Generic;
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
    }
}