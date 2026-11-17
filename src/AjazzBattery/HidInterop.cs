using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace AjazzBattery;

/// <summary>Минимальный набор Win32/HID-вызовов для работы с вендорским интерфейсом мыши.</summary>
internal static class HidInterop
{
    public const uint GENERIC_READ = 0x8000_0000;
    public const uint GENERIC_WRITE = 0x4000_0000;
    public const uint FILE_SHARE_READ = 0x1;
    public const uint FILE_SHARE_WRITE = 0x2;
    public const uint OPEN_EXISTING = 3;

    private const int DIGCF_PRESENT = 0x02;
    private const int DIGCF_DEVICEINTERFACE = 0x10;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct NativeGuid
    {
        public uint Data1;
        public ushort Data2;
        public ushort Data3;
        public ulong Data4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DeviceInterfaceData
    {
        public int cbSize;
        public NativeGuid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HidAttributes
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out NativeGuid guid);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref NativeGuid guid, IntPtr enumerator, IntPtr hwnd, int flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref NativeGuid guid, int index, ref DeviceInterfaceData data);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref DeviceInterfaceData data, IntPtr detail, int size, ref int needed, IntPtr info);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(IntPtr handle, ref HidAttributes attributes);

    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool HidD_GetProductString(IntPtr handle, StringBuilder buffer, int length);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(IntPtr handle, out IntPtr preparsed);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsed);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern int HidP_GetCaps(IntPtr preparsed, [Out] byte[] caps);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_SetFeature(IntPtr handle, byte[] buffer, int length);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetFeature(IntPtr handle, [Out] byte[] buffer, int length);

    public static List<string> EnumerateDevicePaths()
    {
        var result = new List<string>();
        HidD_GetHidGuid(out var hidGuid);
        var set = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == IntPtr.Zero || set == new IntPtr(-1))
        {
            return result;
        }

        try
        {
            var data = new DeviceInterfaceData();
            int structSize = Marshal.SizeOf<DeviceInterfaceData>();
            for (int index = 0; ; index++)
            {
                data.cbSize = structSize;
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, index, ref data))
                {
                    break;
                }

                int needed = 0;
                SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, ref needed, IntPtr.Zero);
                if (needed <= 8)
                {
                    continue;
                }

                var buffer = Marshal.AllocHGlobal(needed);
                try
                {
                    // cbSize структуры SP_DEVICE_INTERFACE_DETAIL_DATA: 8 на x64, 6 на x86.
                    Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref data, buffer, needed, ref needed, IntPtr.Zero))
                    {
                        continue;
                    }

                    var path = Marshal.PtrToStringUni(IntPtr.Add(buffer, 4)) ?? string.Empty;
                    if (!path.StartsWith(@"\\?\", StringComparison.Ordinal))
                    {
                        path = Marshal.PtrToStringUni(IntPtr.Add(buffer, 8)) ?? path;
                    }

                    if (path.Length > 0)
                    {
                        result.Add(path);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }

        return result;
    }

    public static SafeFileHandle Open(string path, bool writeAccess)
    {
        uint access = writeAccess ? GENERIC_READ | GENERIC_WRITE : GENERIC_READ;
        return CreateFile(path, access, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
    }

    public static bool TryGetAttributes(SafeFileHandle handle, out ushort vendorId, out ushort productId)
    {
        var attributes = new HidAttributes { Size = Marshal.SizeOf<HidAttributes>() };
        vendorId = productId = 0;
        if (!HidD_GetAttributes(handle.DangerousGetHandle(), ref attributes))
        {
            return false;
        }

        vendorId = attributes.VendorID;
        productId = attributes.ProductID;
        return true;
    }

    public static string GetProductString(SafeFileHandle handle)
    {
        var builder = new StringBuilder(256);
        return HidD_GetProductString(handle.DangerousGetHandle(), builder, builder.Capacity * 2)
            ? builder.ToString()
            : string.Empty;
    }

    public static bool TryGetCaps(SafeFileHandle handle, out ushort usagePage, out ushort usage, out ushort featureLength)
    {
        usagePage = usage = featureLength = 0;
        var native = handle.DangerousGetHandle();
        if (!HidD_GetPreparsedData(native, out var preparsed))
        {
            return false;
        }

        try
        {
            var caps = new byte[64];
            if (HidP_GetCaps(preparsed, caps) < 0)
            {
                return false;
            }

            usage = BitConverter.ToUInt16(caps, 0);
            usagePage = BitConverter.ToUInt16(caps, 2);
            featureLength = BitConverter.ToUInt16(caps, 8);
            return true;
        }
        finally
        {
            HidD_FreePreparsedData(preparsed);
        }
    }

    public static bool SetFeature(SafeFileHandle handle, byte[] buffer) =>
        HidD_SetFeature(handle.DangerousGetHandle(), buffer, buffer.Length);

    public static bool GetFeature(SafeFileHandle handle, byte[] buffer) =>
        HidD_GetFeature(handle.DangerousGetHandle(), buffer, buffer.Length);
}
