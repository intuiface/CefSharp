using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CefSharp.Wpf.Example.Controls
{

    /// <summary>
    /// Only to handle "default" touch/mouse event management
    /// </summary>
    public class ChromiumBrowser : ChromiumWebBrowser
    {
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.MouseMove(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.MouseDown(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.MouseLeave(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.MouseUp(e);
        }

        protected override void OnTouchDown(TouchEventArgs e)
        {
            Console.WriteLine(e.GetTouchPoint(this).Position.X);
            base.TouchDown(e);
        }

        protected override void OnTouchMove(TouchEventArgs e)
        {
            base.TouchMove(e);
        }

        protected override void OnTouchUp(TouchEventArgs e)
        {
            base.TouchUp(e);
        }
    }
}
