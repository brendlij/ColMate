using ColMate.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ColMate
{
    public partial class MainWindow : Window
    {
        // Windows API for dark titlebar
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        // Panning state
        private bool _isPanning;
        private Point _lastPanPosition;

        public MainWindow()
        {
            InitializeComponent();
            
            // Apply dark titlebar when window is loaded
            Loaded += (s, e) => EnableDarkTitleBar();
        }

        private void EnableDarkTitleBar()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int value = 1; // 1 = dark mode, 0 = light mode
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
            }
        }

        private void VideoArea_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                if (e.Delta > 0)
                    vm.ZoomIn();
                else
                    vm.ZoomOut();
            }
        }

        private void VideoArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.Zoom > 1.0)
            {
                _isPanning = true;
                _lastPanPosition = e.GetPosition(this);
                ((UIElement)sender).CaptureMouse();
            }
        }

        private void VideoArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            ((UIElement)sender).ReleaseMouseCapture();
        }

        private void VideoArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning && DataContext is MainViewModel vm)
            {
                var currentPos = e.GetPosition(this);
                var delta = currentPos - _lastPanPosition;
                
                // Scale delta by zoom factor for smooth panning
                vm.PanX += delta.X / vm.Zoom;
                vm.PanY += delta.Y / vm.Zoom;
                
                _lastPanPosition = currentPos;
            }
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.ResetZoom();
        }

        // Double-click reset handlers
        private void ResetAngle_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.ResetCrosshairAngle();
        }

        private void ResetLength_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.ResetCrosshairLength();
        }

        private void ResetThickness_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.ResetCrosshairThickness();
        }

        private void ResetOffsetX_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.ResetOffsetX();
        }

        private void ResetOffsetY_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.ResetOffsetY();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.OnClosing();
        }
    }
}
