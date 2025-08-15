using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BigCat
{
    [Serializable]
    public struct PackedMatrix
    {
        public float c0x; public float c0y; public float c0z;
        public float c1x; public float c1y; public float c1z;
        public float c2x; public float c2y; public float c2z;
        public float c3x; public float c3y; public float c3z;

        public PackedMatrix(Matrix4x4 m)
        {
            c0x = m.m00; c0y = m.m10; c0z = m.m20;
            c1x = m.m01; c1y = m.m11; c1z = m.m21;
            c2x = m.m02; c2y = m.m12; c2z = m.m22;
            c3x = m.m03; c3y = m.m13; c3z = m.m23;
        }

        private static readonly PackedMatrix s_zeroPackedMatrix = new PackedMatrix(Matrix4x4.zero);
        public static PackedMatrix zero => s_zeroPackedMatrix;
    }

    public static class BigCatGraphics
    {
        /// <summary>
        /// 常量
        /// </summary>
        public const int sizeOfMatrix = sizeof(float) * 4 * 4;
        public const int sizeOfPackedMatrix = sizeof(float) * 4 * 3;
        public const int sizeOfFloat = sizeof(float);
        public const int sizeOfFloat3 = sizeof(float) * 3;
        public const int sizeOfFloat4 = sizeof(float) * 4;
        public const int sizeOfInt = sizeof(int);
        public const int sizeOfInt4 = sizeof(int) * 4;
        public const int sizeOfUInt4 = sizeof(uint) * 4;

        /// <summary>
        /// GraphicsBuffer头大小
        /// </summary>
        public const int sizeOfGraphicsBufferHead = sizeOfPackedMatrix * 2;

        /// <summary>
        /// 是否是使用OpenGLES渲染
        /// </summary>
        private static bool s_isOpenGLES;
        public static bool isOpenGLES => s_isOpenGLES;

        /// <summary>
        /// CBuffer字节对齐
        /// </summary>
        private static int s_constantBufferOffsetAlignment;
        public static int constantBufferOffsetAlignment => s_constantBufferOffsetAlignment;

        /// <summary>
        /// GraphicsBuffer的Slice的最大Size
        /// </summary>
        private static int s_graphicsBufferMaxSize;

        /// <summary>
        /// GraphicsBuffer Target
        /// OpenGLES下是Constant，代表使用CBuffer
        /// DX，Vulkan，Metal下使用Raw，代表使用SSBO
        /// </summary>
        private static GraphicsBuffer.Target s_graphicsBufferTarget;

        /// <summary>
        /// GraphicsBuffer Stride
        /// </summary>
        private static int s_graphicsBufferStride;

        /// <summary>
        /// GraphicsBuffer缓存池
        /// </summary>
        private static Dictionary<int, List<GraphicsBuffer>> s_graphicsBufferPoolMap1 = new();
        private static Dictionary<int, List<GraphicsBuffer>> s_graphicsBufferPoolMap2 = new();

        /// <summary>
        /// 初始化
        /// </summary>
        public static void Initialize()
        {
            //检查是否是OpenGLES
            s_isOpenGLES = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 ||
                           SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
            s_constantBufferOffsetAlignment = s_isOpenGLES ? BatchRendererGroup.GetConstantBufferOffsetAlignment() : 1;

            if (s_isOpenGLES)
            {
                s_graphicsBufferTarget = GraphicsBuffer.Target.Constant;
                s_graphicsBufferStride = 16;

                //检测SystemInfo.maxConstantBufferSize
                var minConstantBufferSize = 24 * 1024;
                if (SystemInfo.maxConstantBufferSize < minConstantBufferSize)
                {
                    throw new System.Exception("手机的SystemInfo.maxConstantBufferSize必须大于24*1024");
                }

                //计算48与constantBufferOffsetAlignment的最小公倍数
                var v = LeastCommonMultiple(48, SystemInfo.constantBufferOffsetAlignment);
                s_graphicsBufferMaxSize = (Mathf.Min(minConstantBufferSize, SystemInfo.maxConstantBufferSize) - v - 1) / v * v;
            }
            else
            {
                s_graphicsBufferTarget = GraphicsBuffer.Target.Raw;
                s_graphicsBufferStride = 4;
                s_graphicsBufferMaxSize = (64 * 1024 - 47) / 48 * 48;
            }
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public static void Destroy()
        {
            var bufferPoolMaps = new [] { s_graphicsBufferPoolMap1, s_graphicsBufferPoolMap2 };
            foreach (var bufferPoolMap in bufferPoolMaps)
            {
                foreach (var pool in bufferPoolMap.Values)
                {
                    foreach (var graphicsBuffer in pool)
                    {
                        graphicsBuffer.Dispose();
                    }
                }
                bufferPoolMap.Clear();
            }
        }

        /// <summary>
        /// 获取或创建GraphicsBuffer
        /// </summary>
        /// <returns></returns>
        public static GraphicsBuffer GetOrCreateGraphicsBuffer(GraphicsBuffer.UsageFlags usageFlags, int bufferSize)
        {
            var bufferPoolMap = usageFlags == GraphicsBuffer.UsageFlags.LockBufferForWrite
                ? s_graphicsBufferPoolMap1
                : s_graphicsBufferPoolMap2;
            if (bufferPoolMap.TryGetValue(bufferSize, out var bufferPool))
            {
                if (bufferPool.Count > 0)
                {
                    //从缓存池中取出最后一个GraphicsBuffer
                    var lastIndex = bufferPool.Count - 1;
                    var graphicsBuffer = bufferPool[lastIndex];
                    bufferPool.RemoveAt(lastIndex);
                    return graphicsBuffer;
                }
            }
            else
            {
                bufferPool = new List<GraphicsBuffer>();
                bufferPoolMap.Add(bufferSize, bufferPool);
            }

            //如果缓存池中没有，则创建一个新的GraphicsBuffer
            return new GraphicsBuffer(s_graphicsBufferTarget, usageFlags, bufferSize / s_graphicsBufferStride, s_graphicsBufferStride);
        }

        public static void ReleaseGraphicsBuffer(GraphicsBuffer graphicsBuffer, int bufferSize)
        {
            var bufferPoolMap = graphicsBuffer.usageFlags == GraphicsBuffer.UsageFlags.LockBufferForWrite
                ? s_graphicsBufferPoolMap1
                : s_graphicsBufferPoolMap2;
            if (bufferPoolMap.TryGetValue(bufferSize, out var bufferPool))
            {
                //将GraphicsBuffer放回缓存池
                bufferPool.Add(graphicsBuffer);
            }
            else
            {
                //如果缓存池中没有，则直接销毁GraphicsBuffer
                graphicsBuffer.Dispose();
            }
        }

        #region Utility
        /// <summary>
        /// 计算比target大的最小的2的幂
        /// </summary>
        public static int FindPowerOf2(int target)
        {
            int result = 2;
            while (result <= target)
            {
                result *= 2;
            }
            return result;
        }

        /// <summary>
        /// 计算大于等于v的下一个m的倍数
        /// </summary>
        public static int FindNextMultiple(int v, int m)
        {
            // 计算v除以m的余数
            int remainder = v % m;

            // 如果余数为0，说明v已经是m的倍数
            if (remainder == 0)
            {
                return v;
            }

            // 否则，计算下一个m的倍数
            int n = v + (m - remainder);
            return n;
        }

        /// <summary>
        /// 计算最大公约数
        /// </summary>
        public static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                int temp = a % b;
                a = b;
                b = temp;
            }
            return a;
        }

        /// <summary>
        /// 计算最小公倍数
        /// </summary>
        public static int LeastCommonMultiple(int num1, int num2)
        {
            return num1 * num2 / GreatestCommonDivisor(num1, num2);
        }

        /// <summary>
        /// 计算每个Batch的最大的Instance数量
        /// </summary>
        /// <param name="sizeOfPerInstance"></param>
        /// <returns></returns>
        public static int MaxInstanceCountPerBatch(int sizeOfPerInstance)
        {
#if UNITY_ANDROID || UNITY_IOS
            return Mathf.Min(128, s_graphicsBufferMaxSize / sizeOfPerInstance);
#else
            return s_graphicsBufferMaxSize / sizeOfPerInstance;
#endif
        }
        #endregion
    }
}