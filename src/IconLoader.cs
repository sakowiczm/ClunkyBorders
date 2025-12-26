using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClunkyBorders;

internal class IconLoader
{
    public unsafe HICON LoadFromResources(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Logger.Error("IconLoader. Error loading icon. Invalid icon name");
            return default;
        }

        try
        {
            var assembly = typeof(TrayManager).Assembly;
            var resourceName = assembly.GetName().Name + "." + fileName;
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
                return default;

            var iconData = new byte[stream.Length];
            _ = stream.Read(iconData, 0, iconData.Length);

            // https://en.wikipedia.org/wiki/ICO_(file_format)

            // ICO file structure:
            //   ICONDIR (6 bytes): Reserved, Type, Count
            //   ICONDIRENTRY[] (16 bytes each): Width, Height, Colors, Reserved, Planes, BitCount, Size, Offset
            //   Icon data: Actual bitmap/PNG data for each icon

            // Minimum size for a valid ICO file
            if (iconData.Length <= 22)
                return default;

            // ICONDIRENTRY
            // dwBytesInRes (4 bytes) - Size of icon data - startIndex 14 (6+8)
            // dwImageOffset (4 bytes) - Offset to icon data in file - startIndex 18 (6+12)
            int size = BitConverter.ToInt32(iconData, 14);
            int offset = BitConverter.ToInt32(iconData, 18);

            // Validate that offset and size are within the file bounds
            if (offset <= 0 || offset + size > iconData.Length)
                return default;

            fixed (byte* pIconData = &iconData[offset])
            {
                var hIcon = PInvoke.CreateIconFromResourceEx(
                    pIconData,
                    (uint)size,
                    true,
                    0x00030000,
                    0, 0,
                    0
                );

                if(hIcon.IsNull)
                {
                    Logger.Error($"IconLoader. Error loading icon. Error code: {Marshal.GetLastWin32Error()}");
                }

                return hIcon.IsNull ? default : hIcon;
            }
        }
        catch(Exception ex)
        {
            Logger.Error("IconLoader. Error loading icon.", ex);
            return default;
        }
    }

}
