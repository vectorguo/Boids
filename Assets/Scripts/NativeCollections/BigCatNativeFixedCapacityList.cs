using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace BigCat.NativeCollections
{
    /// <summary>
    /// 固定容量的原生列表
    /// 创建时需要指定容量，不能动态扩展
    /// </summary>
    public struct BigCatNativeFixedCapacityList<T> where T : unmanaged
    {
        /// <summary>
        /// 内存切片
        /// </summary>
        private BigCatMemoryManager.MemorySlice m_slice;

        /// <summary>
        /// 类型大小
        /// </summary>
        private int m_typeSize;

        /// <summary>
        /// List容量
        /// </summary>
        public int capacity;

        /// <summary>
        /// List长度
        /// </summary>
        public int length;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="initialCapacity">List存储容量</param>
        /// <param name="recalculateCapacity">是否需要重新计算容量</param>
        public BigCatNativeFixedCapacityList(int initialCapacity, bool recalculateCapacity = false)
        {
            capacity = initialCapacity;
            length = 0;

            // 分配内存切片
            m_typeSize = UnsafeUtility.SizeOf<T>();
            BigCatMemoryManager.AllocateSlice(
                m_typeSize * capacity,
                UnsafeUtility.AlignOf<T>(),
                out m_slice);

            // 如果需要重新计算容量，则根据切片大小计算
            if (recalculateCapacity)
            {
                capacity = m_slice.sliceSize / m_typeSize;
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
        /// 索引访问
        /// </summary>
        public unsafe T this[int index]
        {
            get
            {
                if (index < 0 || index >= length)
                {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for length {length}.");
                }
                return UnsafeUtility.ReadArrayElement<T>(m_slice.pointer, index);
            }
            set
            {
                if (index < 0 || index >= length)
                {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for length {length}.");
                }
                UnsafeUtility.WriteArrayElement(m_slice.pointer, index, value);
            }
        }

        /// <summary>
        /// 获取指定索引的元素的原生指针
        /// </summary>
        public unsafe T* ElementAt(int index)
        {
            if (index < 0 || index >= length)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range for length {length}.");
            }
            return (T*)m_slice.pointer + index;
        }

        /// <summary>
        /// 添加元素
        /// 非线程安全操作，如果需要多线程访问，请使用BigCatNativeList.Parallel
        /// </summary>
        public void Add(in T value)
        {
            if (length + 1 > capacity)
            {
                throw new Exception("超过存储容量");
            }
            unsafe
            {
                UnsafeUtility.WriteArrayElement(m_slice.pointer, length++, value);
            }
        }

        /// <summary>
        /// 添加元素
        /// 非线程安全操作，如果需要多线程访问，请使用BigCatNativeList.Parallel
        /// </summary>
        public void AddRange(in BigCatNativeArray<T> array)
        {
            var arrayLength = array.length;
            if (arrayLength > 0)
            {
                if (length + arrayLength > capacity)
                {
                    throw new Exception("超过存储容量");
                }
                unsafe
                {
                    var dstPtr = (byte*)m_slice.pointer + length * m_typeSize;
                    var srcPtr = array.GetUnsafePtr();
                    UnsafeUtility.MemCpy(dstPtr, srcPtr, arrayLength * m_typeSize);
                    length += arrayLength;
                }
            }
        }

        /// <summary>
        /// 添加元素
        /// 非线程安全操作，如果需要多线程访问，请使用BigCatNativeList.Parallel
        /// </summary>
        public void AddRange(in BigCatNativeList<T> list)
        {
            var listLength = list.length;
            if (listLength > 0)
            {
                if (length + listLength > capacity)
                {
                    throw new Exception("超过存储容量");
                }
                unsafe
                {
                    var dstPtr = (byte*)m_slice.pointer + length * m_typeSize;
                    var srcPtr = list.GetUnsafePtr();
                    UnsafeUtility.MemCpy(dstPtr, srcPtr, listLength * m_typeSize);
                    length += listLength;
                }
            }
        }

        /// <summary>
        /// 添加元素
        /// 非线程安全操作，如果需要多线程访问，请使用BigCatNativeList.Parallel
        /// </summary>
        public void AddRange(in NativeList<T> list)
        {
            var listLength = list.Length;
            if (listLength > 0)
            {
                if (length + listLength > capacity)
                {
                    throw new Exception("超过存储容量");
                }
                unsafe
                {
                    var dstPtr = (byte*)m_slice.pointer + length * m_typeSize;
                    var srcPtr = list.GetUnsafePtr();
                    UnsafeUtility.MemCpy(dstPtr, srcPtr, listLength * m_typeSize);
                    length += listLength;
                }
            }
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
        /// 移除指定索引的元素（与最后一个元素交换）
        /// </summary>
        public void RemoveAtSwapBack(int index)
        {
            if (index < 0 || index >= length)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range for length {length}.");
            }
            var lastIndex = --length;
            if (index < lastIndex)
            {
                unsafe
                {
                    var dstPtr = (byte*)m_slice.pointer + index * m_typeSize;
                    var srcPtr = (byte*)m_slice.pointer + lastIndex * m_typeSize;
                    UnsafeUtility.MemCpy(dstPtr, srcPtr, m_typeSize);
                }
            }
        }

        /// <summary>
        /// 移除最后一个元素
        /// </summary>
        public void RemoveLast()
        {
            if (length > 0)
            {
                --length;
            }
        }

        public void Clear()
        {
            length = 0;
        }

        public unsafe void CopyFrom(in BigCatNativeFixedCapacityList<T> sourceList)
        {
            if (capacity < sourceList.length)
            {
                throw new ArgumentException("目标列表容量不足以容纳源列表的内容");
            }
            if (sourceList.length == 0)
            {
                length = 0;
            }
            else
            {
                length = sourceList.length;
                UnsafeUtility.MemCpy(m_slice.pointer, sourceList.m_slice.pointer, sourceList.length * m_typeSize);
            }
        }

        /// <summary>
        /// 根据Slice的大小重新计算容量
        /// </summary>
        public void RecalculateCapacity()
        {
            capacity = m_slice.sliceSize / m_typeSize;
        }

        /// <summary>
        /// 获取原生指针
        /// </summary>
        public unsafe void* GetUnsafePtr()
        {
            return m_slice.pointer;
        }
    }
}