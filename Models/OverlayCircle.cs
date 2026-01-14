using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ColMate.Models
{
    public sealed class OverlayCircle : INotifyPropertyChanged
    {
        private double _radius;
        private double _thickness;
        private Brush _stroke;
        private bool _isVisible = true;

        public OverlayCircle(double radius, Brush stroke, double thickness)
        {
            _radius = radius;
            _stroke = stroke;
            _thickness = thickness;
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public double Radius
        {
            get => _radius;
            set
            {
                _radius = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Diameter));
            }
        }

        public double Diameter => Radius * 2.0;

        public double Thickness
        {
            get => _thickness;
            set { _thickness = value; OnPropertyChanged(); }
        }

        public Brush Stroke
        {
            get => _stroke;
            set { _stroke = value; OnPropertyChanged(); OnPropertyChanged(nameof(StrokeName)); }
        }

        public string StrokeName
        {
            get
            {
                // Best-effort name for UI list. For custom brushes, falls back to type name.
                if (Stroke is SolidColorBrush scb)
                    return scb.Color.ToString();
                return Stroke?.GetType().Name ?? "Brush";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
