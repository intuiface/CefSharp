using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using Rect = CefSharp.Structs.Rect;

namespace CefSharp.Wpf.Rendering
{
    class RenderInfo
    {
        public RenderInfo()
        {

        }

        public RenderInfo(bool isPopup, Rect dirtyRect, IntPtr buffer, int width, int height, Image image)
        {
            IsPopup = isPopup;
            DirtyRect = dirtyRect;
            Buffer = buffer;
            Width = width;
            Height = height;
            Image = image;
        }

        public bool IsPopup { get; set; }
        public Rect DirtyRect { get; set; }
        public IntPtr Buffer { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Image Image { get; set; }

        public int NumberOfDirtyBytes
        {
            get
            {
                int pixels = DirtyRect.Width * DirtyRect.Height;
                return pixels * BytesPerPixel;
            }
        }

        public int NumberOfBytes
        {
            get
            {
                int pixels = DirtyRect.Width * DirtyRect.Height;
                return pixels * BytesPerPixel;
            }
        }

        public readonly PixelFormat PixelFormat = PixelFormats.Bgra32;
        public int BytesPerPixel = PixelFormats.Bgra32.BitsPerPixel / 8;
    }
}
