//  ---------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// 
//  The MIT License (MIT)
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
//  ---------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using Windows.UI.Composition;

namespace Composition.WindowsRuntimeHelpers
{
    /// <summary>
    /// 提供 Windows.UI.Composition 与桌面窗口之间的互操作辅助方法。
    /// </summary>
    public static class CompositionHelper
    {
        [ComImport]
        [Guid("25297D5C-3AD4-4C9C-B5CF-E36A38512330")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComVisible(true)]
        interface ICompositorInterop
        {
            ICompositionSurface CreateCompositionSurfaceForHandle(
                IntPtr swapChain);

            ICompositionSurface CreateCompositionSurfaceForSwapChain(
                IntPtr swapChain);

            CompositionGraphicsDevice CreateGraphicsDevice(
                IntPtr renderingDevice);
        }

        [ComImport]
        [Guid("29E691FA-4567-4DCA-B319-D0F207EB6807")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComVisible(true)]
        interface ICompositorDesktopInterop
        {
            Windows.UI.Composition.Desktop.DesktopWindowTarget CreateDesktopWindowTarget(
                IntPtr hwnd,
        /// <summary>
        /// 为指定桌面窗口创建 CompositionTarget。
        /// </summary>
        /// <param name="compositor">Compositor 实例。</param>
        /// <param name="hwnd">目标窗口句柄。</param>
        /// <param name="isTopmost">是否为最顶层窗口。</param>
        /// <returns>创建的 CompositionTarget。</returns>
                bool isTopmost);
        }

        public static CompositionTarget CreateDesktopWindowTarget(this Compositor compositor, IntPtr hwnd, bool isTopmost)
        {
            var desktopInterop = (ICompositorDesktopInterop)((object)compositor);
            return desktopInterop.CreateDesktopWindowTarget(hwnd, isTopmost);
        }

        /// <summary>
        /// 为 SwapChain 创建 CompositionSurface。
        /// </summary>
        /// <param name="compositor">Compositor 实例。</param>
        /// <param name="swapChain">SharpDX DXGI SwapChain1。</param>
        /// <returns>创建的 ICompositionSurface。</returns>
        public static ICompositionSurface CreateCompositionSurfaceForSwapChain(this Compositor compositor, SharpDX.DXGI.SwapChain1 swapChain)
        {
            var interop = (ICompositorInterop)(object)compositor;
            return interop.CreateCompositionSurfaceForSwapChain(swapChain.NativePointer);
        }
    }
}
