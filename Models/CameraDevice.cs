namespace ColMate.Models
{
    public sealed class CameraDevice
    {
        public int Index { get; }
        public string DisplayName { get; }
        public string DevicePath { get; }

        public CameraDevice(int index, string displayName, string devicePath)
        {
            Index = index;
            DisplayName = displayName;
            DevicePath = devicePath;
        }

        public override string ToString() => DisplayName;
    }
}