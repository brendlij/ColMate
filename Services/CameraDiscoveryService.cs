using ColMate.Models;
using DirectShowLib;
using System.Collections.Generic;

namespace ColMate.Services
{
    public sealed class CameraDiscoveryService
    {
        // Keine Parameter mehr nötig, da DirectShow alle Geräte auflistet
        public List<CameraDevice> Discover()
        {
            var result = new List<CameraDevice>();
            DsDevice[] devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);

            for (int i = 0; i < devices.Length; i++)
            {
                result.Add(new CameraDevice(i, devices[i].Name, devices[i].DevicePath));
            }

            return result;
        }
    }
}