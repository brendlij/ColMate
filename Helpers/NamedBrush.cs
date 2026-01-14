using System.Windows.Media;

namespace ColMate.Helpers
{
    public sealed class NamedBrush
    {
        public NamedBrush(string name, Brush brush)
        {
            Name = name;
            Brush = brush;
        }

        public string Name { get; }
        public Brush Brush { get; }
    }
}
