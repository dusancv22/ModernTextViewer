using System;

namespace ModernTextViewer.src.Models
{
    public class HyperlinkModel
    {
        public int StartIndex { get; set; }
        public int Length { get; set; }
        public string Url { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public Guid Id { get; set; } = Guid.NewGuid();

        public int EndIndex => StartIndex + Length;

        public bool ContainsPosition(int position)
        {
            return position >= StartIndex && position < EndIndex;
        }

        public HyperlinkModel Clone()
        {
            return new HyperlinkModel
            {
                StartIndex = StartIndex,
                Length = Length,
                Url = Url,
                DisplayText = DisplayText,
                Id = Id
            };
        }
    }
}