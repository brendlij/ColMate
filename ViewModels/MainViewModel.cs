using ColMate.Helpers;
using ColMate.Models;
using ColMate.Services;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColMate.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly CameraDiscoveryService _discovery = new();
        private readonly CameraStreamService _stream = new();

        private CameraDevice? _selectedCamera;
        private BitmapSource? _previewFrame;
        private string _status = "Bereit";

        // Frame size (native coordinate system for overlay)
        private double _frameWidth = 3840;
        private double _frameHeight = 2160;

        // Calibration center (editable)
        private double _centerX = 1935.49;
        private double _centerY = 1069.4;

        // Crosshair
        private double _crosshairAngle = 0;
        private double _crosshairLength = 900;
        private double _crosshairThickness = 2;
        private Brush _crosshairBrush = Brushes.Red;

        // Focus/Exposure
        private double _focus;
        private double _exposure;
        private double _fMin;
        private double _fMax = 255;
        private double _eMin;
        private double _eMax = 255;

        public ObservableCollection<CameraDevice> Cameras { get; } = new();

        public CameraDevice? SelectedCamera
        {
            get => _selectedCamera;
            set
            {
                _selectedCamera = value;
                OnPropertyChanged();
                StartCommand.RaiseCanExecuteChanged();
            }
        }

        public BitmapSource? PreviewFrame
        {
            get => _previewFrame;
            private set { _previewFrame = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); }
        }

        public double FrameWidth
        {
            get => _frameWidth;
            private set { _frameWidth = value; OnPropertyChanged(); OnPropertyChanged(nameof(FrameInfo)); }
        }

        public double FrameHeight
        {
            get => _frameHeight;
            private set { _frameHeight = value; OnPropertyChanged(); OnPropertyChanged(nameof(FrameInfo)); }
        }

        public string FrameInfo => $"Frame: {FrameWidth:0}×{FrameHeight:0} | Center: ({CenterX:0.00}, {CenterY:0.00})";

        public double CenterX
        {
            get => _centerX;
            set { _centerX = value; OnPropertyChanged(); }
        }

        public double CenterY
        {
            get => _centerY;
            set { _centerY = value; OnPropertyChanged(); }
        }

        public double CrosshairAngle
        {
            get => _crosshairAngle;
            set { _crosshairAngle = value; OnPropertyChanged(); }
        }

        public double CrosshairLength
        {
            get => _crosshairLength;
            set { _crosshairLength = value; OnPropertyChanged(); OnPropertyChanged(nameof(CrosshairHalfLength)); OnPropertyChanged(nameof(CrosshairX1)); OnPropertyChanged(nameof(CrosshairX2)); OnPropertyChanged(nameof(CrosshairY1)); OnPropertyChanged(nameof(CrosshairY2)); }
        }

        public double CrosshairThickness
        {
            get => _crosshairThickness;
            set { _crosshairThickness = value; OnPropertyChanged(); }
        }

        public Brush CrosshairBrush
        {
            get => _crosshairBrush;
            set { _crosshairBrush = value; OnPropertyChanged(); }
        }

        private double CrosshairHalfLength => CrosshairLength / 2.0;

        public double CrosshairX1 => CenterX - CrosshairHalfLength;
        public double CrosshairX2 => CenterX + CrosshairHalfLength;
        public double CrosshairY1 => CenterY - CrosshairHalfLength;
        public double CrosshairY2 => CenterY + CrosshairHalfLength;

        public double Focus
        {
            get => _focus;
            set { _focus = value; _stream.SetFocus(value); OnPropertyChanged(); }
        }

        public double Exposure
        {
            get => _exposure;
            set { _exposure = value; _stream.SetExposure(value); OnPropertyChanged(); }
        }

        public double FocMin { get => _fMin; private set { _fMin = value; OnPropertyChanged(); } }
        public double FocMax { get => _fMax; private set { _fMax = value; OnPropertyChanged(); } }
        public double ExpMin { get => _eMin; private set { _eMin = value; OnPropertyChanged(); } }
        public double ExpMax { get => _eMax; private set { _eMax = value; OnPropertyChanged(); } }

        public ObservableCollection<NamedBrush> BrushOptions { get; } = new()
        {
            new NamedBrush("Rot", Brushes.Red),
            new NamedBrush("Grün", Brushes.Lime),
            new NamedBrush("Blau", Brushes.DeepSkyBlue),
            new NamedBrush("Gelb", Brushes.Gold),
            new NamedBrush("Weiß", Brushes.White),
            new NamedBrush("Cyan", Brushes.Cyan),
            new NamedBrush("Magenta", Brushes.Magenta),
        };

        public ObservableCollection<OverlayCircle> Circles { get; } = new();
        private OverlayCircle? _selectedCircle;
        public OverlayCircle? SelectedCircle
        {
            get => _selectedCircle;
            set { _selectedCircle = value; OnPropertyChanged(); RemoveCircleCommand.RaiseCanExecuteChanged(); }
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand StartCommand { get; }
        public RelayCommand StopCommand { get; }

        public RelayCommand AddCircleCommand { get; }
        public RelayCommand RemoveCircleCommand { get; }

        public RelayCommand SetCenterToFrameCommand { get; }

        public RelayCommand FocUp => new(() => Focus += 5);
        public RelayCommand FocDown => new(() => Focus -= 5);
        public RelayCommand ExpUp => new(() => Exposure += 1);
        public RelayCommand ExpDown => new(() => Exposure -= 1);

        public MainViewModel()
        {
            _stream.FrameReady += OnFrameReady;

            RefreshCommand = new RelayCommand(Refresh);
            StartCommand = new RelayCommand(Start, () => SelectedCamera != null && !_stream.IsStreaming);
            StopCommand = new RelayCommand(Stop, () => _stream.IsStreaming);

            AddCircleCommand = new RelayCommand(AddCircle);
            RemoveCircleCommand = new RelayCommand(RemoveSelectedCircle, () => SelectedCircle != null);

            SetCenterToFrameCommand = new RelayCommand(() =>
            {
                if (FrameWidth > 0 && FrameHeight > 0)
                {
                    CenterX = FrameWidth / 2.0;
                    CenterY = FrameHeight / 2.0;
                }
            });

            // Default circles (user can edit/add/remove)
            Circles.Add(new OverlayCircle(radius: 250, stroke: Brushes.Lime, thickness: 2));
            Circles.Add(new OverlayCircle(radius: 600, stroke: Brushes.Lime, thickness: 2));
            Circles.Add(new OverlayCircle(radius: 1100, stroke: Brushes.Lime, thickness: 2));
            SelectedCircle = Circles.Count > 0 ? Circles[0] : null;

            Refresh();
        }

        private void Refresh()
        {
            Stop();
            Cameras.Clear();

            foreach (var cam in _discovery.Discover())
                Cameras.Add(cam);

            SelectedCamera = Cameras.Count > 0 ? Cameras[0] : null;
            Status = Cameras.Count > 0 ? $"{Cameras.Count} Kamera(s) gefunden" : "Keine Kamera gefunden";
        }

        private void Start()
        {
            if (SelectedCamera == null) return;

            try
            {
                _stream.Start(SelectedCamera.Index);

                // set frame size immediately if available
                if (_stream.VideoWidth > 0 && _stream.VideoHeight > 0)
                {
                    FrameWidth = _stream.VideoWidth;
                    FrameHeight = _stream.VideoHeight;
                }

                // update slider ranges
                var fr = _stream.GetPropertyRange(VideoCaptureProperties.Focus);
                FocMin = fr.Min; FocMax = fr.Max;

                var er = _stream.GetPropertyRange(VideoCaptureProperties.Exposure);
                ExpMin = er.Min; ExpMax = er.Max;

                Status = $"Streaming: {SelectedCamera.DisplayName}";
                StartCommand.RaiseCanExecuteChanged();
                StopCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                Status = $"Start fehlgeschlagen: {ex.Message}";
            }
        }

        public void Stop()
        {
            _stream.Stop();
            PreviewFrame = null;

            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            Status = "Gestoppt";
        }

        private void AddCircle()
        {
            var stroke = Brushes.Lime;
            var c = new OverlayCircle(radius: 800, stroke: stroke, thickness: 2);
            Circles.Add(c);
            SelectedCircle = c;
        }

        private void RemoveSelectedCircle()
        {
            if (SelectedCircle == null) return;
            var idx = Circles.IndexOf(SelectedCircle);
            Circles.Remove(SelectedCircle);
            SelectedCircle = (Circles.Count == 0) ? null : Circles[Math.Clamp(idx, 0, Circles.Count - 1)];
        }

        private void OnFrameReady(Mat mat)
        {
            try
            {
                // Capture size update (first frames can reveal it)
                if (mat.Width > 0 && mat.Height > 0 && (FrameWidth != mat.Width || FrameHeight != mat.Height))
                {
                    FrameWidth = mat.Width;
                    FrameHeight = mat.Height;
                }

                var bmp = mat.ToBitmapSource();
                bmp.Freeze();
                App.Current.Dispatcher.Invoke(() => PreviewFrame = bmp);
            }
            finally
            {
                mat.Dispose();
            }
        }

        public void OnClosing() => _stream.Dispose();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
