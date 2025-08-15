using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace BigCat.NativeCollections
{
    public enum BigCatNativeArrayOptions
    {
        UninitializedMemory,
        ClearMemory,
    }

    [BurstCompile]
    public struct BigCatNativeArray<T> where T : unmanaged
    {
        /// <summary>
        /// 内存切片
        /// </summary>
        private BigCatMemoryManager.MemorySlice m_slice;

        /// <summary>
        /// Array长度
        /// </summary>
        private readonly int m_length;
        public int length => m_length;

        /// <summary>
        /// 是否被创建
        /// </summary>
        public unsafe bool isCreated => m_slice.pointer != null;

        /// <summary>
        /// 构造函数
        /// </summary>
        public unsafe BigCatNativeArray(int length, BigCatNativeArrayOptions options = BigCatNativeArrayOptions.UninitializedMemory)
        {
            m_length = length;

            // 分配内存切片
            BigCatMemoryManager.AllocateSlice(
                UnsafeUtility.SizeOf<T>() * m_length,
                UnsafeUtility.AlignOf<T>(),
                out m_slice);

            // 是否清除内存
            if (options == BigCatNativeArrayOptions.ClearMemory)
            {
                UnsafeUtility.MemClear(m_slice.pointer, m_length * UnsafeUtility.SizeOf<T>());
            }
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            BigCatMemoryManager.ReleaseSlice(m_slice);
        }

        /// <summary>
        /// 获取内存指针（不安全操作）
        /// </summary>
        /// <returns>指向数据的内存指针</returns>
        public unsafe void* GetUnsafePtr()
        {
            return m_slice.pointer;
        }

        /// <summary>
        /// 索引访问
        /// </summary>
        public unsafe T this[int index]
        {
            get
            {
                return UnsafeUtility.ReadArrayElement<T>(m_slice.pointer, index);
            }
            set
            {
                UnsafeUtility.WriteArrayElement(m_slice.pointer, index, value);
            }
        }

        /// <summary>
        /// 获取指定索引的元素的原生指针
        /// </summary>
        public unsafe T* ElementAt(int index)
        {
            if (index < 0 || index >= m_length)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range for length {m_length}.");
            }
            return (T*)m_slice.pointer + index;
        }

        /// <summary>
        /// 查找指定值的索引
        /// </summary>
        /// <param name="value">值</param>
        /// <returns>指定值的索引，如果没有找到则返回-1</returns>
        public unsafe int IndexOf(in T value)
        {
            T* ptr = (T*)m_slice.pointer;
            for (int i = 0; i < length; i++)
            {
                if ((ptr + i)->Equals(value))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public unsafe void Init(byte value)
        {
            UnsafeUtility.MemSet(m_slice.pointer, value, m_length * UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// 从数组复制数据到NativeArray
        /// </summary>
        /// <param name="managedArray">C#托管数组</param>
        public unsafe void CopyFrom(T[] managedArray)
        {
            var stride = UnsafeUtility.SizeOf<T>();
            var managedArrayGCHandle = GCHandle.Alloc(managedArray, GCHandleType.Pinned);
            var managedArrayPointer = managedArrayGCHandle.AddrOfPinnedObject();
            UnsafeUtility.MemCpy(m_slice.pointer, (byte*)(void*)managedArrayPointer, managedArray.Length * stride);
            managedArrayGCHandle.Free();
        }

        /// <summary>
        /// 从另一个BigCatNativeArray复制数据到当前NativeArray
        /// </summary>
        public unsafe void CopyFrom(in BigCatNativeArray<T> other)
        {
            if (m_length < other.length)
            {
                throw new System.ArgumentException("Destination array must be larger than or equal to source array.");
            }
            UnsafeUtility.MemCpy(m_slice.pointer, other.m_slice.pointer, other.length * UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// 转换为Unity原生NativeArray，共享相同的内存数据
        /// 注意：返回的NativeArray不拥有内存，请勿对其调用Dispose()
        /// </summary>
        /// <returns>与当前BigCatNativeArray共享内存的Unity NativeArray</returns>
        public unsafe NativeArray<T> AsNativeArray()
        {
            var nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
                m_slice.pointer, 
                m_length, 
                Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // 设置安全句柄，防止意外释放
            var safetyHandle = AtomicSafetyHandle.Create();
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, safetyHandle);
#endif

            return nativeArray;
        }
    }
}