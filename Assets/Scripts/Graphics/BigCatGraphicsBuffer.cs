using BigCat.NativeCollections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace BigCat
{
    public class BigCatGraphicsBuffer
    {
        /// <summary>
        /// Unity原生的GraphicsBuffer
        /// </summary>
        private readonly GraphicsBuffer m_nativeGraphicsBuffer;

        /// <summary>
        /// GraphicsBuffer最大尺寸
        /// </summary>
        private readonly int m_nativeGraphicsBufferSize;
        public int nativeGraphicsBufferSize => m_nativeGraphicsBufferSize;

        /// <summary>
        /// GraphicsBuffer
        /// </summary>
        private NativeArray<byte> m_nativeGraphicsBufferWriter;

        /// <summary>
        /// Handle
        /// </summary>
        public GraphicsBufferHandle bufferHandle => m_nativeGraphicsBuffer.bufferHandle;

        /// <summary>
        /// 填充的数据大小
        /// </summary>
        private uint m_filledDataSize = 0;

        /// <summary>
        /// 写入数据的起始偏移
        /// </summary>
        private uint m_dataStartOffset;

#if UNITY_EDITOR
        /// <summary>
        /// Global ID，用于在编辑器中唯一标识GraphicsBuffer
        /// </summary>
        private static int s_globalID = 0;

        /// <summary>
        /// 唯一标识符
        /// </summary>
        private readonly int m_id;
#endif

        /// <summary>
        /// 构造函数
        /// </summary>
        public BigCatGraphicsBuffer(int bufferSize) : this(GraphicsBuffer.UsageFlags.LockBufferForWrite, bufferSize) { }

        /// <summary>
        /// 构造函数
        /// </summary>
        public BigCatGraphicsBuffer(GraphicsBuffer.UsageFlags usageFlags, int bufferSize)
        {
#if UNITY_EDITOR
            m_id = ++s_globalID;
#endif

            m_nativeGraphicsBufferSize = BigCatGraphics.FindPowerOf2(bufferSize);
            m_nativeGraphicsBuffer = BigCatGraphics.GetOrCreateGraphicsBuffer(usageFlags, m_nativeGraphicsBufferSize);
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            BigCatGraphics.ReleaseGraphicsBuffer(m_nativeGraphicsBuffer, m_nativeGraphicsBufferSize);
        }

        /// <summary>
        /// 开始写入数据
        /// </summary>
        public void LockBufferForWrite()
        {
            m_nativeGraphicsBufferWriter = m_nativeGraphicsBuffer.LockBufferForWrite<byte>(0, nativeGraphicsBufferSize / sizeof(byte));
            m_dataStartOffset = 0;
        }

        /// <summary>
        /// 停止写入数据
        /// </summary>
        public void UnlockBufferAfterWrite()
        {
            m_nativeGraphicsBuffer.UnlockBufferAfterWrite<byte>(nativeGraphicsBufferSize / sizeof(byte));
        }

        /// <summary>
        /// 开始填充数据
        /// </summary>
        public uint BeginFillData()
        {
            m_filledDataSize = 0;
            return m_dataStartOffset;
        }

        /// <summary>
        /// 结束填充数据
        /// </summary>
        public uint EndFillData()
        {
            var dataSize = (uint)BigCatGraphics.FindNextMultiple((int)m_filledDataSize, BigCatGraphics.constantBufferOffsetAlignment);
            m_dataStartOffset += dataSize;
            return dataSize;
        }

        /// <summary>
        /// 填充数据
        /// </summary>
        /// <typeparam name="T">填充的数据类型</typeparam>
        /// <param name="data">数据</param>
        /// <param name="count">数据数量</param>
        /// <returns>填充数据的总字节长度</returns>
        public unsafe uint FillData<T>(in BigCatNativeArray<T> data, int count) where T : unmanaged
        {
            var stride = UnsafeUtility.SizeOf<T>();
            var dataSize = (uint)(count * stride);

            //Copy
            var dstPtr = (byte*)m_nativeGraphicsBufferWriter.GetUnsafePtr();
            var srcPtr = (byte*)data.GetUnsafePtr();
            UnsafeUtility.MemCpy(dstPtr + (m_dataStartOffset + m_filledDataSize), srcPtr, dataSize);

            //更新StartOffset
            m_filledDataSize += dataSize;
            return dataSize;
        }

        public unsafe uint FillData<T>(in BigCatNativeArray<T> data, int startIndex, int count) where T : unmanaged
        {
            var stride = UnsafeUtility.SizeOf<T>();
            var dataSize = (uint)(count * stride);

            //Copy
            var dstPtr = (byte*)m_nativeGraphicsBufferWriter.GetUnsafePtr();
            var srcPtr = (byte*)data.GetUnsafePtr() + startIndex * stride;
            UnsafeUtility.MemCpy(dstPtr + (m_dataStartOffset + m_filledDataSize), srcPtr, dataSize);

            //更新StartOffset
            m_filledDataSize += dataSize;
            return dataSize;
        }

        public unsafe uint FillData<T>(in BigCatNativeList<T> data, int count) where T : unmanaged
        {
            var stride = UnsafeUtility.SizeOf<T>();
            var dataSize = (uint)(count * stride);

            //Copy
            var dstPtr = (byte*)m_nativeGraphicsBufferWriter.GetUnsafePtr();
            var srcPtr = (byte*)data.GetUnsafePtr();
            UnsafeUtility.MemCpy(dstPtr + (m_dataStartOffset + m_filledDataSize), srcPtr, dataSize);

            //更新StartOffset
            m_filledDataSize += dataSize;
            return dataSize;
        }

        public unsafe uint FillDataWithOffset<T>(in BigCatNativeArray<T> data, int count, uint addressOffset) where T : unmanaged
        {
            var stride = UnsafeUtility.SizeOf<T>();
            var dataSize = (uint)(count * stride);

            //Copy
            var dstPtr = (byte*)m_nativeGraphicsBufferWriter.GetUnsafePtr();
            var srcPtr = (byte*)data.GetUnsafePtr();
            UnsafeUtility.MemCpy(dstPtr + addressOffset, srcPtr, dataSize);

            return dataSize;
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        public unsafe void SetData<T>(in BigCatNativeArray<T> data, int count, uint addressOffset) where T : unmanaged
        {
            var stride = UnsafeUtility.SizeOf<T>();

            //SetData
            m_nativeGraphicsBuffer.SetData(data.AsNativeArray(), 0, (int)(addressOffset / stride), count);
        }
    }
}