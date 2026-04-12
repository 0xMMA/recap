using System.Runtime.InteropServices;

namespace Recap.Interop;

public static class KeyState
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_R = 0x52;

    public static bool IsRKeyDown()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;
        return (GetAsyncKeyState(VK_R) & 0x8000) != 0;
    }
}
