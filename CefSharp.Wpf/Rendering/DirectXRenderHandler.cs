// Copyright © 2010-2017 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using SharpDX.Direct3D9;
using SharpDX.WPF;
using System;
using System.Linq;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Rect = CefSharp.Structs.Rect;
using System.Windows.Interop;
using System.IO;

namespace CefSharp.Wpf.Rendering
{
    /// <summary>
    /// DirectXRenderHandler - creates/updates a Dx texture
    /// Uses a MemoryMappedFile for double buffering when the size matches
    /// or creates a new WritableBitmap when required
    /// </summary>
    /// <seealso cref="CefSharp.Wpf.IRenderHandler" />
    public class DirectXRenderHandler : IRenderHandler
    {
        //////// Direct X mod start now
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        /// <summary>
        /// In case of DirectX rendering, Hold Dx9 texture.
        /// </summary>
        private DXImageSource src;

        /// <summary>
        /// Try to render in a DirectX texture.
        /// </summary>
        private bool IsDirectXInitialized = false;

        /// <summary>
        /// Texture whee data is injected (dynamic)
        /// </summary>
        private SharpDX.Direct3D9.Texture texA;

        /// <summary>
        /// Texture RenderTarget compliant
        /// </summary>
        private SharpDX.Direct3D9.Texture tex;

        /// <summary>
        /// Texture Height when created
        /// </summary>
        private int texHeight;

        /// <summary>
        /// Texture Height when created
        /// </summary>
        private int texWidth;

        /// <summary>
        /// DirectX device. One to rule them all.
        /// </summary>
        private static D3D9 device9 = new D3D9();

        //Framerate rendering stuff
        public int Framerate_LastSecond = 0;
        public int Framerate_FrameCountByDelta = 0;
        public int Framerate_FramerateValue = 0;

        //Popup management visibility
        public bool PopupVisibility = false;
        public int PopupPositionX = 0;
        public int PopupPositionY = 0;

        private MemoryMappedFile memoryMappedFile;
        private MemoryMappedViewAccessor memoryMappedViewAccessor;

        private MemoryMappedFile popupMemoryMappedFile;
        private MemoryMappedViewAccessor popupMemoryMappedViewAccessor;

        void ReInitTextures(bool recreateTextures, bool redraw = true)
        {
            //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " ReInitTextures" + Environment.NewLine);
            lock (lockObject)
            {
                //Display white screen after lock. Do not recreate textures.
                if (recreateTextures)
                {
                    var oldTex = tex;
                    var oldTexA = texA;
                    InitTextures(redraw);
                }
                CurrentRenderInfo.Image.Dispatcher.BeginInvoke((Action)(() =>
                {
                    //Force creating source, once again, sometimes, Dx losing context just after run & break the source (king of a render pipe)...
                    InvalidateImageSource(true);
                }),
        DispatcherPriority.Render);
            }
        }

        void InitTextures(bool redraw = true)
        {
            if (CurrentRenderInfo == null)
                return;

            //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " InitTextures" + Environment.NewLine);
            texA = new SharpDX.Direct3D9.Texture(
                      device9.Device,
                      CurrentRenderInfo.Width,
                      CurrentRenderInfo.Height,
                              0,
                              SharpDX.Direct3D9.Usage.Dynamic,
                              SharpDX.Direct3D9.Format.A8R8G8B8,
                              Pool.SystemMemory);
            var data = texA.LockRectangle(0, LockFlags.None);

            //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " " + CurrentRenderInfo.Width + "x" + CurrentRenderInfo.Height + Environment.NewLine);

            if (CurrentRenderInfo.Buffer != IntPtr.Zero && redraw)
                CopyMemory(data.DataPointer, CurrentRenderInfo.Buffer, (uint)CurrentRenderInfo.NumberOfBytes);
            else
                CopyMemory(data.DataPointer, CurrentRenderInfo.Buffer, (uint)CurrentRenderInfo.NumberOfBytes);

            texA.UnlockRectangle(0);

            tex = new SharpDX.Direct3D9.Texture(
               device9.Device,
               CurrentRenderInfo.Width,
               CurrentRenderInfo.Height,
                       0,
                       SharpDX.Direct3D9.Usage.RenderTarget,
                       SharpDX.Direct3D9.Format.A8R8G8B8,
                       Pool.Default);
            texHeight = CurrentRenderInfo.Height;
            texWidth = CurrentRenderInfo.Width;

            Debug.WriteLine("InitTextures end called");
            IsDirectXInitialized = true;
        }



        private void DirectXRender(RenderInfo renderInfo)
        {
            //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " DirectXRender" + Environment.NewLine);

            Debug.WriteLine("Render called");
            if (renderInfo == null)
                return;

            if (!IsDirectXInitialized)
            {
                Debug.WriteLine("InitTextures called");
                InitTextures();
                RenderData(renderInfo, true);
            }
            else if (!renderInfo.IsPopup && (texHeight != renderInfo.Height || texWidth != renderInfo.Width))
            {
                InitTextures();
                RenderData(renderInfo, true);
            }
            else
            {
                RenderData(renderInfo, false);
            }
        }

        private void RenderData(RenderInfo renderInfo, bool forceChangeSource)
        {

            //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " RenderData" + Environment.NewLine);

            var sec = DateTime.Now.Second;
            if (Framerate_LastSecond != sec)
            {
                Framerate_FramerateValue = Framerate_FrameCountByDelta;
                Framerate_LastSecond = sec;
                Framerate_FrameCountByDelta = 1;
            }
            else
            {
                Framerate_FrameCountByDelta++;
            }

            var data = texA.LockRectangle(0, LockFlags.None);

            if (!renderInfo.IsPopup)
            {
                //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " !popup" + Environment.NewLine);
                //Case of popup
                if (popupMemoryMappedFile != null && PopupVisibility == true)
                {
                    CopyMemoryGentle(memoryMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle(), data.DataPointer, CurrentRenderInfo.NumberOfBytes);
                    CopyMemoryGentle(popupMemoryMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle(), data.DataPointer, CurrentPopupRenderInfo, renderInfo);
                    Debug.WriteLine("Popup case over redeaw");
                }
                ///After closing popup
                else if (popupMemoryMappedFile != null && PopupVisibility == false)
                {
                    CopyMemoryGentle(renderInfo.Buffer, data.DataPointer, renderInfo.NumberOfBytes);
                    ReleaseMemoryMappedView(ref popupMemoryMappedFile, ref popupMemoryMappedViewAccessor);
                    Debug.WriteLine("After closing popup " + renderInfo.NumberOfBytes);
                }
                else
                {
                    //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + "Global case posX:" + renderInfo.DirtyRect.X + " posY:" + renderInfo.DirtyRect.Y + " w:" + renderInfo.DirtyRect.Width + " h:" + renderInfo.DirtyRect.Height + Environment.NewLine);
                    CopyMemoryGentle(renderInfo.Buffer, data.DataPointer, renderInfo.DirtyRect, renderInfo);
                    Debug.WriteLine("Global case posX:" + renderInfo.DirtyRect.X + " posY:" + renderInfo.DirtyRect.Y + " w:" + renderInfo.DirtyRect.Width + " h:" + renderInfo.DirtyRect.Height);
                }
            }
            else
            {
                //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " popup" + Environment.NewLine);
                CopyMemoryGentle(popupMemoryMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle(), data.DataPointer, CurrentPopupRenderInfo, CurrentRenderInfo);
                Debug.WriteLine("Popup case");
            }

            texA.UnlockRectangle(0);


            device9.Device.UpdateTexture(texA, tex);

            CurrentRenderInfo.Image.Dispatcher.BeginInvoke((Action)(() =>
            {
                //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " InvalidateImageSource" + Environment.NewLine);

                InvalidateImageSource(forceChangeSource);
            }),
        DispatcherPriority.Render);
        }

        private void InvalidateImageSource(bool forceChangeSource)
        {
            //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " InvalidateImageSource INSIDE" + Environment.NewLine);

            Debug.WriteLine("InvalidateImageSource calling ...");
            if (!(CurrentRenderInfo.Image.Source is DXImageSource) || forceChangeSource)
            {
                lock (lockObject)
                {
                    //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " InvalidateImageSource DXImageSource " + CurrentRenderInfo.Image.GetHashCode() + Environment.NewLine);
                    src = new DXImageSource();
                    src.OnContextRetreived += Src_OnContextRetreived;
                    src.SetBackBuffer(tex);
                    CurrentRenderInfo.Image.Source = src;

                    Debug.WriteLine("InvalidateImageSource called ...");
                }
            }
            else
            {
                src.Invalidate();
            }
        }

        private void ReleaseMemoryMappedView(ref MemoryMappedFile mappedFile, ref MemoryMappedViewAccessor stream)
        {
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }

            if (mappedFile != null)
            {
                mappedFile.Dispose();
                mappedFile = null;
            }
        }

        private void Src_OnContextRetreived(object sender, EventArgs e)
        {
            //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " Src_OnContextRetreived" + Environment.NewLine);
            ReInitTextures(false, false);
        }

        private void CopyMemoryGentle(IntPtr source, IntPtr destination, long startIndexSource, long startIndexDestination, int length)
        {
            CopyMemory(new IntPtr(destination.ToInt64() + startIndexDestination), new IntPtr(source.ToInt64() + startIndexSource), (uint)length);
        }

        private void CopyMemoryGentle(IntPtr source, IntPtr destination, long startIndexDestination, int length)
        {
            CopyMemory(new IntPtr(destination.ToInt64() + startIndexDestination), source, (uint)length);
        }

        private void CopyMemoryGentle(IntPtr source, IntPtr destination, int length)
        {
            CopyMemory(destination, source, (uint)length);
        }

        private void CopyMemoryGentle(IntPtr source, IntPtr destination, Rect dirtyRect, RenderInfo info)
        {
            IntPtr newDestination = new IntPtr(destination.ToInt64() + dirtyRect.Y * info.Width * info.BytesPerPixel + dirtyRect.X * info.BytesPerPixel);
            IntPtr newSource = new IntPtr(source.ToInt64() + dirtyRect.Y * info.Width * info.BytesPerPixel + dirtyRect.X * info.BytesPerPixel);
            int length = (dirtyRect.Height - 1) * info.Width * info.BytesPerPixel + dirtyRect.Width * info.BytesPerPixel;

            CopyMemory(newDestination, newSource, (uint)length);
        }

        private void CopyMemoryGentle(IntPtr source, IntPtr destination, RenderInfo popup, RenderInfo info)
        {
            for (int i = 0; i < popup.Height; i++)
            {
                CopyMemory(
                    new IntPtr(destination.ToInt64() + (PopupPositionY + i) * info.Width * info.BytesPerPixel + PopupPositionX * popup.BytesPerPixel),
                    new IntPtr(popup.Buffer.ToInt64() + i * popup.Width * popup.BytesPerPixel),
                    (uint)(popup.Width * popup.BytesPerPixel));
            }
        }

        //////// Direct X mod end now







        /// <summary>
        /// The pixel format
        /// </summary>
        private static readonly PixelFormat PixelFormat = PixelFormats.Bgra32;
        private static int BytesPerPixel = PixelFormat.BitsPerPixel / 8;

        private object lockObject = new object();

        private Size viewSize;
        private Size popupSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectXRenderHandler"/> class.
        /// </summary>
        public DirectXRenderHandler()
        {
        }

        public void Dispose()
        {
        }

        private RenderInfo CurrentRenderInfo = new RenderInfo();
        private RenderInfo CurrentPopupRenderInfo = new RenderInfo();

        void IRenderHandler.OnPaint(bool isPopup, Rect dirtyRect, IntPtr buffer, int width, int height, Image image)
        {
            //File.AppendAllText("DEBUG.txt", DateTime.Now.ToLongTimeString() + this.GetHashCode() + " ONPAINT " + isPopup + " " + buffer + " - " + width + "x" + height + " " + image.GetHashCode() + Environment.NewLine);
            if (image.Dispatcher.HasShutdownStarted ||
                (dirtyRect.X == 0 && dirtyRect.Y == 0 && dirtyRect.Width == 0 && dirtyRect.Height == 0))
            {
                return;
            }

            if (isPopup)
            {

                CurrentPopupRenderInfo = new RenderInfo(isPopup, dirtyRect, buffer, width, height, image);

                ReleaseMemoryMappedView(ref memoryMappedFile, ref memoryMappedViewAccessor);
                memoryMappedFile = MemoryMappedFile.CreateNew(null, CurrentRenderInfo.NumberOfBytes, MemoryMappedFileAccess.ReadWrite);
                memoryMappedViewAccessor = memoryMappedFile.CreateViewAccessor();

                var data = texA.LockRectangle(0, LockFlags.ReadOnly);
                CopyMemoryGentle(data.DataPointer, memoryMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle(), CurrentRenderInfo.NumberOfBytes);
                texA.UnlockRectangle(0);

                ReleaseMemoryMappedView(ref popupMemoryMappedFile, ref popupMemoryMappedViewAccessor);
                popupMemoryMappedFile = MemoryMappedFile.CreateNew(null, CurrentPopupRenderInfo.NumberOfBytes, MemoryMappedFileAccess.ReadWrite);
                popupMemoryMappedViewAccessor = popupMemoryMappedFile.CreateViewAccessor();
                CopyMemoryGentle(CurrentPopupRenderInfo.Buffer, popupMemoryMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle(), CurrentPopupRenderInfo.NumberOfBytes);

                DirectXRender(CurrentPopupRenderInfo);
            }
            else
            {
                CurrentRenderInfo.IsPopup = isPopup;
                CurrentRenderInfo.DirtyRect = dirtyRect;
                CurrentRenderInfo.Buffer = buffer;
                CurrentRenderInfo.Width = width;
                CurrentRenderInfo.Height = height;
                CurrentRenderInfo.Image = image;
                DirectXRender(CurrentRenderInfo);
            }


        }

        //private void CreateOrUpdateBitmap(bool isPopup, Rect dirtyRect, IntPtr buffer, int width, int height, Image image, ref Size currentSize, ref MemoryMappedFile mappedFile, ref MemoryMappedViewAccessor viewAccessor)
        //{
        //    bool createNewBitmap = false;

        //    if (image.Dispatcher.HasShutdownStarted)
        //    {
        //        return;
        //    }

        //    lock (lockObject)
        //    {

        //        int pixels = width * height;
        //        int numberOfBytes = pixels * BytesPerPixel;

        //        createNewBitmap = mappedFile == null || currentSize.Height != height || currentSize.Width != width;

        //        if (createNewBitmap)
        //        {
        //            ReleaseMemoryMappedView(ref mappedFile, ref viewAccessor);

        //            mappedFile = MemoryMappedFile.CreateNew(null, numberOfBytes, MemoryMappedFileAccess.ReadWrite);

        //            viewAccessor = mappedFile.CreateViewAccessor();

        //            currentSize.Height = height;
        //            currentSize.Width = width;
        //        }

        //        //TODO: Performance analysis to determine which is the fastest memory copy function
        //        //NativeMethodWrapper.CopyMemoryUsingHandle(viewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle(), buffer, numberOfBytes);
        //        CopyMemory(viewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle(), buffer, (uint)numberOfBytes);

        //        //Take a reference to the sourceBuffer that's used to update our WritableBitmap,
        //        //once we're on the UI thread we need to check if it's still valid
        //        var sourceBuffer = viewAccessor.SafeMemoryMappedViewHandle;

        //        image.Dispatcher.BeginInvoke((Action)(() =>
        //        {
        //            lock (lockObject)
        //            {
        //                if (sourceBuffer.IsClosed || sourceBuffer.IsInvalid)
        //                {
        //                    return;
        //                }

        //                if (createNewBitmap)
        //                {
        //                    if (image.Source != null)
        //                    {
        //                        image.Source = null;
        //                        GC.Collect(1);
        //                    }

        //                    image.Source = new WriteableBitmap(width, height, dpiX, dpiY, PixelFormat, null);
        //                }

        //                var stride = width * BytesPerPixel;
        //                var noOfBytes = stride * height;

        //                var bitmap = (WriteableBitmap)image.Source;

        //                //By default we'll only update the dirty rect, for those that run into a MILERR_WIN32ERROR Exception (#2035)
        //                //it's desirably to either upgrade to a newer .Net version (only client runtime needs to be installed, not compiled
        //                //against a newer version. Or invalidate the whole bitmap
        //                if (invalidateDirtyRect)
        //                {
        //                    // Update the dirty region
        //                    var sourceRect = new Int32Rect(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);

        //                    bitmap.Lock();
        //                    bitmap.WritePixels(sourceRect, sourceBuffer.DangerousGetHandle(), noOfBytes, stride, dirtyRect.X, dirtyRect.Y);
        //                    bitmap.Unlock();
        //                }
        //                else
        //                {
        //                    // Update whole bitmap
        //                    var sourceRect = new Int32Rect(0, 0, width, height);

        //                    bitmap.Lock();
        //                    bitmap.WritePixels(sourceRect, sourceBuffer.DangerousGetHandle(), noOfBytes, stride);
        //                    bitmap.Unlock();
        //                }
        //            }
        //        }), dispatcherPriority);
        //    }
        //}

        //private void ReleaseMemoryMappedView(ref MemoryMappedFile mappedFile, ref MemoryMappedViewAccessor stream)
        //{
        //    if (stream != null)
        //    {
        //        stream.Dispose();
        //        stream = null;
        //    }

        //    if (mappedFile != null)
        //    {
        //        mappedFile.Dispose();
        //        mappedFile = null;
        //    }
        //}
    }
}
