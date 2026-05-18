using UnityEngine.UIElements;

namespace UI.Panels
{
    internal sealed class LocalizedTextBinding
    {
        public LocalizedTextBinding(TextElement element, string key)
        {
            Element = element;
            Key = key?.Trim() ?? string.Empty;
        }

        public TextElement Element { get; }
        public string Key { get; }
    }
}
