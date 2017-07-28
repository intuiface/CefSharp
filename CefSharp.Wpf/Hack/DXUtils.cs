using SharpDX;
using SharpDX.Direct3D;
using SharpDX.WPF;
using System.Runtime.InteropServices;
using System.IO;
using System;
using SharpDX.Mathematics.Interop;

namespace SharpDX.WPF
{
    public static class DXUtils
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T GetOrThrow<T>(this T obj)
            where T : class, IDisposable
        {
            if (obj == null)
                throw new ObjectDisposedException(typeof(T).Name);
            return obj;
        } 

        /// <summary>
        /// 
        /// </summary>
        public static RawVector3 TransformNormal(this RawMatrix m, RawVector3 v)
        {            
            var v2 = Multiply(m, v.X, v.Y, v.Z, 0);
            return new RawVector3(v2.X, v2.Y, v2.Z);
        }

        /// <summary>
        /// 
        /// </summary>
        public static RawVector3 TransformCoord(this RawMatrix m, RawVector3 v)
        {
            var v2 = Multiply(m, v.X, v.Y, v.Z, 1);
            return new RawVector3(v2.X, v2.Y, v2.Z);
        }

        /// <summary>
        /// 
        /// </summary>
        public static RawVector3 Multiply(this RawMatrix m, float x, float y, float z, float w)
        {
            return new RawVector3(
                m.M11 * x + m.M12 * y + m.M13 * z + m.M14 * w
                , m.M21 * x + m.M22 * y + m.M23 * z + m.M24 * w
                , m.M31 * x + m.M32 * y + m.M33 * z + m.M34 * w
                );
        }

        /// <summary>
        /// 
        /// </summary>
        public static float DEG2RAD(this float degrees)
        {
            return degrees * (float)Math.PI / 180.0f;
        }
    
    }
}
