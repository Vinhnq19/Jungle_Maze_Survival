using UnityEngine;
using System;
using System.Runtime.InteropServices;

public class SmoothAspectRatio : MonoBehaviour
{
    private const float TARGET_ASPECT = 1920.0f / 1080.0f;

    #region P/Invoke Declarations (64-bit safe)

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private static WndProcDelegate wndProcDelegate;
    private static IntPtr oldWndProc;

    private const int GWLP_WNDPROC = -4;

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    private const int WM_SIZING = 0x0214;
    private const int WMSZ_RIGHT = 2;
    private const int WMSZ_LEFT = 1;
    private const int WMSZ_TOP = 3;
    private const int WMSZ_BOTTOM = 6;
    private const int WMSZ_TOPLEFT = 4, WMSZ_TOPRIGHT = 5, WMSZ_BOTTOMLEFT = 7, WMSZ_BOTTOMRIGHT = 8;
    
    #endregion

    private void OnEnable()
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer || Application.isEditor) return;

        IntPtr hWnd = GetActiveWindow();
        if (hWnd == IntPtr.Zero) return;

        wndProcDelegate = WndProc;
        IntPtr newWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate);
        oldWndProc = SetWindowLongPtr(hWnd, GWLP_WNDPROC, newWndProc);
    }

    private void OnDisable()
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer || Application.isEditor) return;

        IntPtr hWnd = GetActiveWindow();
        if (hWnd == IntPtr.Zero || oldWndProc == IntPtr.Zero) return;

        SetWindowLongPtr(hWnd, GWLP_WNDPROC, oldWndProc);
        oldWndProc = IntPtr.Zero;
    }

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_SIZING)
        {
            RECT rect = (RECT)Marshal.PtrToStructure(lParam, typeof(RECT));
            
            int originalWidth = rect.right - rect.left;
            int originalHeight = rect.bottom - rect.top;
            
            int newWidth = originalWidth;
            int newHeight = originalHeight;

            int side = wParam.ToInt32();

            if (side == WMSZ_LEFT || side == WMSZ_RIGHT || side == WMSZ_TOPLEFT || side == WMSZ_TOPRIGHT || side == WMSZ_BOTTOMLEFT || side == WMSZ_BOTTOMRIGHT)
            {
                newHeight = Mathf.RoundToInt(newWidth / TARGET_ASPECT);
            }
            if (side == WMSZ_TOP || side == WMSZ_BOTTOM)
            {
                newWidth = Mathf.RoundToInt(newHeight * TARGET_ASPECT);
            }

            if(side == WMSZ_LEFT || side == WMSZ_TOPLEFT || side == WMSZ_BOTTOMLEFT)
                rect.left = rect.right - newWidth;
            if(side == WMSZ_RIGHT || side == WMSZ_TOPRIGHT || side == WMSZ_BOTTOMRIGHT)
                rect.right = rect.left + newWidth;
            if(side == WMSZ_TOP || side == WMSZ_TOPLEFT || side == WMSZ_TOPRIGHT)
                rect.top = rect.bottom - newHeight;
            if(side == WMSZ_BOTTOM || side == WMSZ_BOTTOMLEFT || side == WMSZ_BOTTOMRIGHT)
                rect.bottom = rect.top + newHeight;
            
            Marshal.StructureToPtr(rect, lParam, true);
            return (IntPtr)1; 
        }
        
        return CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
    }
}