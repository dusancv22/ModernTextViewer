using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernTextViewer.src.Models
{
    public class DocumentModel
    {
        public string Content { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool IsDirty { get; set; }

        public void SetContent(string content)
        {
            Content = content;
            IsDirty = true;
        }

        public void ResetDirty()
        {
            IsDirty = false;
        }
    }
}