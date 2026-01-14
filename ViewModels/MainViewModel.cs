using ColMate.Helpers;
using ColMate.Models;
using ColMate.Services;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        private string _status = "Ready";

        // Frame size (native coordinate system for overlay)
        private double _frameWidth = 3840;
        private double _frameHeight = 2160;

        // Calibration center (editable)
        private double _centerX = 1935.49;
        private double _centerY = 1069.4;

        // Die Koordinaten (CenterX/CenterY) beziehen sich auf diese Basisauflösung (z.B. 3840×2160)
        private double _calibrationWidth = 3840;
        private double _calibrationHeight = 2160;

        // Wunschauflösung für den Stream (wird "best effort" gesetzt; echte Größe kommt aus den Frames)
        private int _requestedStreamWidth = 3840;
        private int _requestedStreamHeight = 2160;

        // Crosshair
        private double _crosshairAngle = 0;
        private double _crosshairLength = 900;
        private double _crosshairThickness = 2;
        private Brush _crosshairBrush = Brushes.Red;

        // Zoom
        private double _zoom = 1.0;
        private double _panX = 0;
        private double _panY = 0;

        // Manual Overlay Offset
        private double _overlayOffsetX = 0;
        private double _overlayOffsetY = 0;

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
            private set { _frameWidth = value; OnPropertyChanged(); OnPropertyChanged(nameof(OverlayCenterX)); OnPropertyChanged(nameof(FrameInfo)); }
        }

        public double FrameHeight
        {
            get => _frameHeight;
            private set { _frameHeight = value; OnPropertyChanged(); OnPropertyChanged(nameof(OverlayCenterY)); OnPropertyChanged(nameof(FrameInfo)); }
        }

                public string FrameInfo =>
            $"Frame: {FrameWidth:0}×{FrameHeight:0} | Center: ({OverlayCenterX:0.0}, {OverlayCenterY:0.0}) | Offset: ({OverlayOffsetX:+0.0;-0.0;0}, {OverlayOffsetY:+0.0;-0.0;0})";

        public double CenterX
        {
            get => _centerX;
            set { _centerX = value; OnPropertyChanged(); OnPropertyChanged(nameof(OverlayCenterX)); OnPropertyChanged(nameof(FrameInfo)); }
        }

        public double CenterY
        {
            get => _centerY;
            set { _centerY = value; OnPropertyChanged(); OnPropertyChanged(nameof(OverlayCenterY)); OnPropertyChanged(nameof(FrameInfo)); }
        }

        public double CalibrationWidth
        {
            get => _calibrationWidth;
            set
            {
                _calibrationWidth = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OverlayCenterX));
                OnPropertyChanged(nameof(FrameInfo));
            }
        }

        public double CalibrationHeight
        {
            get => _calibrationHeight;
            set
            {
                _calibrationHeight = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OverlayCenterY));
                OnPropertyChanged(nameof(FrameInfo));
            }
        }

        // Center in aktuellem Frame-Koordinatensystem (skaliert aus der Kalibrierbasis) + Manual Offset
        public double OverlayCenterX => ((CalibrationWidth > 0 && FrameWidth > 0)
            ? CenterX * (FrameWidth / CalibrationWidth)
            : CenterX) + OverlayOffsetX;

        public double OverlayCenterY => ((CalibrationHeight > 0 && FrameHeight > 0)
            ? CenterY * (FrameHeight / CalibrationHeight)
            : CenterY) + OverlayOffsetY;

        // Manual Overlay Offset Properties
        public double OverlayOffsetX
        {
            get => _overlayOffsetX;
            set 
            { 
                _overlayOffsetX = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(OverlayCenterX));
                OnPropertyChanged(nameof(CrosshairX1));
                OnPropertyChanged(nameof(CrosshairX2));
                OnPropertyChanged(nameof(FrameInfo));
            }
        }

        public double OverlayOffsetY
        {
            get => _overlayOffsetY;
            set 
            { 
                _overlayOffsetY = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(OverlayCenterY));
                OnPropertyChanged(nameof(CrosshairY1));
                OnPropertyChanged(nameof(CrosshairY2));
                OnPropertyChanged(nameof(FrameInfo));
            }
        }

        public int RequestedStreamWidth
        {
            get => _requestedStreamWidth;
            set { _requestedStreamWidth = value; OnPropertyChanged(); }
        }

        public int RequestedStreamHeight
        {
            get => _requestedStreamHeight;
            set { _requestedStreamHeight = value; OnPropertyChanged(); }
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

        // Zoom Properties
        public double Zoom
        {
            get => _zoom;
            set 
            { 
                _zoom = Math.Clamp(value, 0.5, 10.0); 
                OnPropertyChanged(); 
            }
        }

        public double PanX
        {
            get => _panX;
            set { _panX = value; OnPropertyChanged(); }
        }

        public double PanY
        {
            get => _panY;
            set { _panY = value; OnPropertyChanged(); }
        }

        public void ZoomIn() => Zoom *= 1.15;
        public void ZoomOut() => Zoom /= 1.15;
        public void ResetZoom() { Zoom = 1.0; PanX = 0; PanY = 0; }

        private double CrosshairHalfLength => CrosshairLength / 2.0;

                // Crosshair should use the *overlay* center (scaled to current frame)
        public double CrosshairX1 => OverlayCenterX - CrosshairHalfLength;
        public double CrosshairX2 => OverlayCenterX + CrosshairHalfLength;
        public double CrosshairY1 => OverlayCenterY - CrosshairHalfLength;
        public double CrosshairY2 => OverlayCenterY + CrosshairHalfLength;
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
            new NamedBrush("Red", Brushes.Red),
            new NamedBrush("Green", Brushes.Lime),
            new NamedBrush("Blue", Brushes.DeepSkyBlue),
            new NamedBrush("Yellow", Brushes.Gold),
            new NamedBrush("White", Brushes.White),
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

        // Crosshair Fine Tuning
        public RelayCommand CrosshairAngleUp { get; }
        public RelayCommand CrosshairAngleDown { get; }
        public RelayCommand CrosshairLengthUp { get; }
        public RelayCommand CrosshairLengthDown { get; }
        public RelayCommand CrosshairThicknessUp { get; }
        public RelayCommand CrosshairThicknessDown { get; }

        // Circle Fine Tuning
        public RelayCommand CircleRadiusUp { get; }
        public RelayCommand CircleRadiusDown { get; }
        public RelayCommand CircleThicknessUp { get; }
        public RelayCommand CircleThicknessDown { get; }

        // Overlay Offset Fine Tuning
        public RelayCommand OffsetXUp { get; }
        public RelayCommand OffsetXDown { get; }
        public RelayCommand OffsetYUp { get; }
        public RelayCommand OffsetYDown { get; }

        // Reset Commands
        public void ResetCrosshairAngle() => CrosshairAngle = 0;
        public void ResetCrosshairLength() => CrosshairLength = 900;
        public void ResetCrosshairThickness() => CrosshairThickness = 2;
        public void ResetOffsetX() => OverlayOffsetX = 0;
        public void ResetOffsetY() => OverlayOffsetY = 0;
        public void ResetFocus() => Focus = 0;
        public void ResetExposure() => Exposure = -5;

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
                    // Setzt sowohl den Center als auch die Kalibrierbasis auf den aktuellen Frame,
                    // damit Overlay-Koordinaten 1:1 stimmen.
                    CalibrationWidth = FrameWidth;
                    CalibrationHeight = FrameHeight;
                    CenterX = FrameWidth / 2.0;
                    CenterY = FrameHeight / 2.0;
                }
            });

            // Fine Tuning Commands
            CrosshairAngleUp = new RelayCommand(() => CrosshairAngle += 0.5);
            CrosshairAngleDown = new RelayCommand(() => CrosshairAngle -= 0.5);
            CrosshairLengthUp = new RelayCommand(() => CrosshairLength += 10);
            CrosshairLengthDown = new RelayCommand(() => CrosshairLength -= 10);
            CrosshairThicknessUp = new RelayCommand(() => CrosshairThickness += 0.5);
            CrosshairThicknessDown = new RelayCommand(() => CrosshairThickness -= 0.5);

            CircleRadiusUp = new RelayCommand(() => { if (SelectedCircle != null) SelectedCircle.Radius += 5; });
            CircleRadiusDown = new RelayCommand(() => { if (SelectedCircle != null) SelectedCircle.Radius -= 5; });
            CircleThicknessUp = new RelayCommand(() => { if (SelectedCircle != null) SelectedCircle.Thickness += 0.5; });
            CircleThicknessDown = new RelayCommand(() => { if (SelectedCircle != null) SelectedCircle.Thickness -= 0.5; });

            // Offset Fine Tuning
            OffsetXUp = new RelayCommand(() => OverlayOffsetX += 0.1);
            OffsetXDown = new RelayCommand(() => OverlayOffsetX -= 0.1);
            OffsetYUp = new RelayCommand(() => OverlayOffsetY += 0.1);
            OffsetYDown = new RelayCommand(() => OverlayOffsetY -= 0.1);

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
            Status = Cameras.Count > 0 ? $"{Cameras.Count} camera(s) found" : "No camera found";
        }

        private void Start()
        {
            if (SelectedCamera == null) return;

            try
            {
                _stream.Start(SelectedCamera.Index, RequestedStreamWidth, RequestedStreamHeight);

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
                Status = $"Start failed: {ex.Message}";
            }
        }

        public void Stop()
        {
            _stream.Stop();
            PreviewFrame = null;

            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            Status = "Stopped";
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

                // Convert Mat -> BitmapSource with a fixed, correct stride (avoids "top-left only" artifacts)
using var bgr = EnsureBgr24(mat);
var bmp = CreateBitmapSourceBgr24(bgr);
bmp.Freeze();
App.Current.Dispatcher.Invoke(() => PreviewFrame = bmp);
}
            finally
            {
                mat.Dispose();
            }
        }


        private static Mat EnsureBgr24(Mat src)
        {
            // Returns a *new* Mat in BGR24 (CV_8UC3). Caller disposes it.
            if (src.Type() == MatType.CV_8UC3)
                return src.IsContinuous() ? src.Clone() : src.Clone();

            var dst = new Mat();
            if (src.Type() == MatType.CV_8UC1)
                Cv2.CvtColor(src, dst, ColorConversionCodes.GRAY2BGR);
            else if (src.Type() == MatType.CV_8UC4)
                Cv2.CvtColor(src, dst, ColorConversionCodes.BGRA2BGR);
            else
            {
                // Fallback: try to convert to 8-bit then to BGR
                using var tmp8 = new Mat();
                src.ConvertTo(tmp8, MatType.CV_8U);
                Cv2.CvtColor(tmp8, dst, ColorConversionCodes.GRAY2BGR);
            }

            if (!dst.IsContinuous())
                dst = dst.Clone();

            return dst;
        }

        private static BitmapSource CreateBitmapSourceBgr24(Mat bgr)
        {
            var width = bgr.Cols;
            var height = bgr.Rows;
            const int bytesPerPixel = 3;
            var stride = width * bytesPerPixel;

            // Copy managed buffer (stable + correct)
            var buffer = new byte[stride * height];
            Marshal.Copy(bgr.Data, buffer, 0, buffer.Length);

            return BitmapSource.Create(
                width, height,
                96, 96,
                PixelFormats.Bgr24,
                null,
                buffer,
                stride
            );
        }


        public void OnClosing() => _stream.Dispose();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
