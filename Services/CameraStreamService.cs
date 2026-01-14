using OpenCvSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColMate.Services
{
    public sealed class CameraStreamService : IDisposable
    {
        private VideoCapture? _capture;
        private CancellationTokenSource? _cts;

        public bool IsStreaming { get; private set; }
        public int VideoWidth { get; private set; }
        public int VideoHeight { get; private set; }

        public event Action<Mat>? FrameReady;
        public event Action<string>? Status;

        public void Start(int deviceIndex, int? requestedWidth = null, int? requestedHeight = null)
        {
            Stop();
            // MSMF (Media Foundation) handles 4K/High-Res webcams better on modern Windows than DSHOW
            _capture = new VideoCapture(deviceIndex, VideoCaptureAPIs.MSMF);

            if (!_capture.IsOpened())
            {
                Status?.Invoke("Fehler: Kamera konnte nicht geöffnet werden.");
                return;
            }

            _capture.Set(VideoCaptureProperties.AutoFocus, 0);
            _capture.Set(VideoCaptureProperties.AutoExposure, 0);

            // Gewünschte Auflösung versuchen (falls angegeben)
            if (requestedWidth.HasValue && requestedHeight.HasValue)
            {
                // MJPG kann höhere Auflösungen/FPS ermöglichen (abhängig vom Treiber)
                _capture.Set(VideoCaptureProperties.FourCC, FourCC.MJPG);
                _capture.Set(VideoCaptureProperties.FrameWidth, requestedWidth.Value);
                _capture.Set(VideoCaptureProperties.FrameHeight, requestedHeight.Value);
            }

            // Auflösung sofort auslesen
            VideoWidth = (int)_capture.Get(VideoCaptureProperties.FrameWidth);
            VideoHeight = (int)_capture.Get(VideoCaptureProperties.FrameHeight);

            // Sicherheits-Fallback für Ocal
            if (VideoWidth <= 0) VideoWidth = requestedWidth ?? 640;
            if (VideoHeight <= 0) VideoHeight = requestedHeight ?? 480;

            _cts = new CancellationTokenSource();
            IsStreaming = true;
            Status?.Invoke($"Stream aktiv: {VideoWidth}×{VideoHeight}");
            Task.Run(() => Loop(_cts.Token));
        }

        public (double Min, double Max, double Default) GetPropertyRange(VideoCaptureProperties prop)
        {
            if (_capture == null || !_capture.IsOpened()) return (0, 100, 0);
            return prop switch
            {
                VideoCaptureProperties.Exposure => (-13, -1, -5),
                VideoCaptureProperties.Focus => (0, 1024, 0),
                _ => (0, 100, 0)
            };
        }

        public void SetFocus(double value) => _capture?.Set(VideoCaptureProperties.Focus, value);
        public void SetExposure(double value) => _capture?.Set(VideoCaptureProperties.Exposure, value);

        public void Stop()
        {
            _cts?.Cancel();
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
            IsStreaming = false;
            Status?.Invoke("Gestoppt.");
        }

        private void Loop(CancellationToken token)
        {
            try
            {
                using var frame = new Mat();
                while (!token.IsCancellationRequested && _capture != null)
                {
                    if (_capture.Read(frame) && !frame.Empty())
                        FrameReady?.Invoke(frame.Clone());
                    Thread.Sleep(10);
                }
            }
            catch { }
        }

        public void Dispose() => Stop();
    }
}