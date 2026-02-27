using System.Runtime.InteropServices;
using Tactadile.Native;

namespace Tactadile.Core;

/// <summary>
/// Sends synthetic keystroke sequences via SendInput.
/// All injected events carry EdgeSnapHelper.Signature so the keyboard hook ignores them.
/// Before sending, any physically-held modifiers that are NOT part of the requested
/// combination are temporarily released to prevent unintended modifier combos.
/// </summary>
public static class KeystrokeSender
{
    public static void Send(ushort[] modifierVks, ushort mainVk)
    {
        var inputs = new List<INPUT>();

        bool shiftHeld = (NativeMethods.GetAsyncKeyState(NativeConstants.VK_SHIFT) & 0x8000) != 0;
        bool ctrlHeld  = (NativeMethods.GetAsyncKeyState(NativeConstants.VK_CONTROL) & 0x8000) != 0;
        bool altHeld   = (NativeMethods.GetAsyncKeyState(NativeConstants.VK_MENU) & 0x8000) != 0;
        bool winHeld   = (NativeMethods.GetAsyncKeyState(NativeConstants.VK_LWIN) & 0x8000) != 0;

        var requested = new HashSet<ushort>(modifierVks);

        // Release physically-held modifiers not part of the requested combo
        if (shiftHeld && !requested.Contains(NativeConstants.VK_SHIFT))
            inputs.Add(MakeKeyInput(NativeConstants.VK_SHIFT, keyUp: true));
        if (ctrlHeld && !requested.Contains(NativeConstants.VK_CONTROL))
            inputs.Add(MakeKeyInput(NativeConstants.VK_CONTROL, keyUp: true));
        if (altHeld && !requested.Contains(NativeConstants.VK_MENU))
            inputs.Add(MakeKeyInput(NativeConstants.VK_MENU, keyUp: true));
        if (winHeld && !requested.Contains(NativeConstants.VK_LWIN))
            inputs.Add(MakeKeyInput(NativeConstants.VK_LWIN, keyUp: true));

        // Press requested modifiers
        foreach (var mod in modifierVks)
            inputs.Add(MakeKeyInput(mod, keyUp: false));

        // Press and release the main key
        inputs.Add(MakeKeyInput(mainVk, keyUp: false));
        inputs.Add(MakeKeyInput(mainVk, keyUp: true));

        // Release requested modifiers
        foreach (var mod in modifierVks)
            inputs.Add(MakeKeyInput(mod, keyUp: true));

        var inputArray = inputs.ToArray();
        NativeMethods.SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf<INPUT>());
    }

    public static void Send(ushort modifierVk, ushort mainVk)
    {
        Send([modifierVk], mainVk);
    }

    private static INPUT MakeKeyInput(ushort vk, bool keyUp)
    {
        return new INPUT
        {
            type = NativeConstants.INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = keyUp ? NativeConstants.KEYEVENTF_KEYUP : 0u,
                    time = 0,
                    dwExtraInfo = EdgeSnapHelper.Signature
                }
            }
        };
    }
}
