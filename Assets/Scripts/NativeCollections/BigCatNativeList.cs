using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace BigCat.NativeCollections
{
    [BurstCompile]
    public unsafe struct BigCatNativeList<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private BigCatNativeFixedCapacityList<T>* m_fixedCapacityList;

        /// <summary>
        /// 是否已创建
        /// </summary>
        public bool isCreated => m_fixedCapacityList != null;

        /// <summary>
        /// 当前长度
        /// </summary>
        public int length => m_fixedCapacityList->length;

        /// <summary>
        /// 容量
        /// </summary>
        public int capacity
        {
            get => m_fixedCapacityList->capacity;
            set => SetCapacity(value);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="initialCapacity">初始容量</param>
        public BigCatNativeList(int initialCapacity)
        {
            m_fixedCapacityList = BigCatMemoryManager.AllocateFclPointer<T>();
            *m_fixedCapacityList = new BigCatNativeFixedCapacityList<T>(initialCapacity, true);
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            if (m_fixedCapacityList != null)
            {
                // 先调用内部结构的Dispose
                m_fixedCapacityList->Dispose();

                // 释放指针指向的内存
                BigCatMemoryManager.ReleaseFclPointer(m_fixedCapacityList);
                m_fixedCapacityList = null;
            }
        }

        /// <summary>
        /// 获取内存指针（不安全操作）
        /// </summary>
        /// <returns>指向数据的内存指针</returns>
        public unsafe void* GetUnsafePtr()
        {
            return m_fixedCapacityList->GetUnsafePtr();
        }

        /// <summary>
        /// 索引访问
        /// </summary>
        public T this[int index]
        {
            get
            {
                return (*m_fixedCapacityList)[index];
            }
            set
            {
                (*m_fixedCapacityList)[index] = value;
            }
        }

        /// <summary>
        /// 获取指定索引的元素的原生指针
        /// </summary>
        public T* ElementAt(int index)
        {
            return m_fixedCapacityList->ElementAt(index);
        }

        /// <summary>
        /// 添加元素
        /// 非线程安全操作，如果需要多线程访问，请使用ParallelWriter
        /// </summary>
        public void Add(in T value)
        {
            var requiredCapacity = m_fixedCapacityList->length + 1;
            if (requiredCapacity > m_fixedCapacityList->capacity)
            {
                // 如果当前长度超过容量，则扩展容量
                SetCapacity(requiredCapacity);
            }
            m_fixedCapacityList->Add(in value);
        }

        /// <summary>
        /// 添加元素
        /// 非线程安全操作，如果需要多线程访问，请使用ParallelWriter
        /// </summary>
        public void AddRange(in BigCatNativeArray<T> array)
        {
            if (array.length == 0)
            {
                // 如果数组为空，则不进行任何操作
                return;
            }

            var requiredCapacity = m_fixedCapacityList->length + array.length;
            if (requiredCapacity > m_fixedCapacityList->capacity)
            {
                // 如果当前长度超过容量，则扩展容量
                SetCapacity(requiredCapacity);
            }
            m_fixedCapacityList->AddRange(array);
        }

        /// <summary>
        /// 添加元素
        /// 非线程安全操作，如果需要多线程访问，请使用ParallelWriter
        /// </summary>
        public void AddRange(in BigCatNativeList<T> list)
        {
            if (list.length == 0)
            {
                // 如果列表为空，则不进行任何操作
                return;
            }

            var requiredCapacity = m_fixedCapacityList->length + list.length;
            if (requiredCapacity > m_fixedCapacityList->capacity)
            {
                // 如果当前长度超过容量，则扩展容量
                SetCapacity(requiredCapacity);
            }
            m_fixedCapacityList->AddRange(list);
        }

        /// <summary>
        /// 添加元素
        /// 非线程安全操作，如果需要多线程访问，请使用ParallelWriter
        /// </summary>
        public void AddRange(in NativeList<T> list)
        {
            if (list.Length == 0)
            {
                // 如果列表为空，则不进行任何操作
                return;
            }

            var requiredCapacity = m_fixedCapacityList->length + list.Length;
            if (requiredCapacity > m_fixedCapacityList->capacity)
            {
                // 如果当前长度超过容量，则扩展容量
                SetCapacity(requiredCapacity);
            }
            m_fixedCapacityList->AddRange(list);
        }

        /// <summary>
        /// 查找指定值的索引
        /// </summary>
        /// <param name="value">值</param>
        /// <returns>指定值的索引，如果没有找到则返回-1</returns>
        public int IndexOf(in T value)
        {
            return m_fixedCapacityList->IndexOf(value);
        }

        /// <summary>
        /// 移除指定索引的元素（与最后一个元素交换）
        /// </summary>
        public void RemoveAtSwapBack(int index)
        {
            m_fixedCapacityList->RemoveAtSwapBack(index);
        }

        /// <summary>
        /// 移除列表中的最后一个元素
        /// </summary>
        public void RemoveLast()
        {
            m_fixedCapacityList->RemoveLast();
        }

        /// <summary>
        /// 清空列表
        /// </summary>
        public void Clear()
        {
            m_fixedCapacityList->Clear();
        }

        /// <summary>
        /// 设置容量
        /// </summary>
        public void SetCapacity(int requiredCapacity, bool needCopyOriginalData = true)
        {
            var currentCapacity = m_fixedCapacityList->capacity;
            if (currentCapacity >= requiredCapacity)
            {
                // 如果当前容量大于或等于所需容量，则不需要扩展
                return;
            }

            // 扩容
            var newFixedCapacityList = new BigCatNativeFixedCapacityList<T>(requiredCapacity, true);
            if (needCopyOriginalData)
            {
                if (m_fixedCapacityList != null && m_fixedCapacityList->length > 0)
                {
                    newFixedCapacityList.CopyFrom(*m_fixedCapacityList);
                }
            }

            // 释放旧的列表并更新指针
            m_fixedCapacityList->Dispose();
            *m_fixedCapacityList = newFixedCapacityList;
        }

        /// <summary>
        /// 从另一个BigCatNativeList复制数据到当前BigCatNativeList
        /// </summary>
        public void CopyFrom(in BigCatNativeList<T> other)
        {
            m_fixedCapacityList->Clear();
            if (other.isCreated)
            {
                var otherLength = other.length;
                if (otherLength > 0)
                {
                    SetCapacity(other.capacity, false);
                    UnsafeUtility.MemCpy(
                        m_fixedCapacityList->GetUnsafePtr(),
                        other.m_fixedCapacityList->GetUnsafePtr(),
                        otherLength * UnsafeUtility.SizeOf<T>());
                    m_fixedCapacityList->length = otherLength;
                }
            }
        }

        #region ParallelReader
        [BurstCompile]
        public struct ParallelReader
        {
            /// <summary>
            /// 指向底层固定容量列表的指针
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            [ReadOnly]
            private readonly BigCatNativeFixedCapacityList<T>* m_pFixedCapacityList;

            /// <summary>
            /// 缓存的长度值，避免重复访问指针
            /// </summary>
            [ReadOnly]
            private readonly int m_length;
            public int length => m_length;

            /// <summary>
            /// 内部构造函数
            /// </summary>
            internal unsafe ParallelReader(BigCatNativeFixedCapacityList<T>* pList, int length)
            {
                m_pFixedCapacityList = pList;
                m_length = length;
            }

            /// <summary>
            /// 线程安全的索引访问（只读）
            /// </summary>
            public T this[int index]
            {
                get
                {
                    return UnsafeUtility.ReadArrayElement<T>(m_pFixedCapacityList->GetUnsafePtr(), index);
                }
            }

            /// <summary>
            /// 线程安全的索引访问（只读）
            /// </summary>
            public T* ElementAt(int index)
            {
                return m_pFixedCapacityList->ElementAt(index);
            }
        }

        /// <summary>
        /// 创建用于并行读取的Reader
        /// </summary>
        /// <returns>ParallelReader实例</returns>
        public ParallelReader AsParallelReader()
        {
            return new ParallelReader(m_fixedCapacityList, m_fixedCapacityList->length);
        }
        #endregion

        #region ParallelWriter
        [BurstCompile]
        public struct ParallelWriter
        {
            /// <summary>
            /// 指向底层固定容量列表的指针
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            private readonly BigCatNativeFixedCapacityList<T>* m_pFixedCapacityList;

            /// <summary>
            /// 指向长度字段的指针，用于原子操作
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            private readonly int* m_pLength;

            /// <summary>
            /// 缓存的容量值，避免重复访问指针
            /// </summary>
            [ReadOnly]
            private readonly int m_capacity;
            public int capacity => m_capacity;

            /// <summary>
            /// 内部构造函数
            /// </summary>
            internal unsafe ParallelWriter(
                BigCatNativeFixedCapacityList<T>* pList,
                int* pLength,
                int capacity)
            {
                m_pFixedCapacityList = pList;
                m_pLength = pLength;
                m_capacity = capacity;
            }

            /// <summary>
            /// 线程安全的添加方法（不会自动扩容）
            /// </summary>
            /// <param name="value">要添加的元素</param>
            public void AddNoResize(in T value)
            {
                // 原子递增获取唯一索引
                int index = Interlocked.Increment(ref *m_pLength) - 1;

                // 检查容量边界
                if (index >= m_capacity)
                {
                    // 回滚长度计数器
                    Interlocked.Decrement(ref *m_pLength);
                    throw new System.InvalidOperationException($"容量溢出：尝试在索引 {index} 处添加元素，但容量仅为 {m_capacity}");
                }

                // 直接写入内存位置
                UnsafeUtility.WriteArrayElement(m_pFixedCapacityList->GetUnsafePtr(), index, value);
            }

            /// <summary>
            /// 线程安全的添加多个元素（不会自动扩容）
            /// </summary>
            /// <param name="list">要添加的List</param>
            public void AddRangeNoResize(NativeList<T> list)
            {
                var listCount = list.Length;
                if (listCount > 0)
                {
                    // 原子性地预留空间，获取起始索引
                    int startIndex = Interlocked.Add(ref *m_pLength, listCount) - listCount;

                    // 检查容量边界
                    if (startIndex + list.Length > m_capacity)
                    {
                        // 回滚长度计数器
                        Interlocked.Add(ref *m_pLength, -list.Length);
                        throw new System.InvalidOperationException($"容量溢出：尝试添加 {listCount} 个元素到索引 {startIndex}，但容量仅为 {m_capacity}");
                    }

                    // 批量内存拷贝
                    var elementSize = UnsafeUtility.SizeOf<T>();
                    var dstPtr = (byte*)m_pFixedCapacityList->GetUnsafePtr() + startIndex * elementSize;
                    var srcPtr = list.GetUnsafeReadOnlyPtr();
                    UnsafeUtility.MemCpy(dstPtr, srcPtr, listCount * elementSize);
                }
            }
        }

        /// <summary>
        /// 创建用于并行写入的Writer
        /// </summary>
        /// <returns>ParallelWriter实例</returns>
        public ParallelWriter AsParallelWriter()
        {
            int* lengthPtr = (int*)UnsafeUtility.AddressOf(ref m_fixedCapacityList->length);
            return new ParallelWriter(m_fixedCapacityList, lengthPtr, m_fixedCapacityList->capacity);
        }
        #endregion

        #region ParallelReadWriter
        [BurstCompile]
        public struct ParallelReadWriter
        {
            /// <summary>
            /// 指向底层固定容量列表的指针
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            private readonly BigCatNativeFixedCapacityList<T>* m_pFixedCapacityList;

            /// <summary>
            /// 指向长度字段的指针，用于原子操作
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            private readonly int* m_pLength;
            public int length => *m_pLength;

            /// <summary>
            /// 缓存的容量值，避免重复访问指针
            /// </summary>
            [ReadOnly]
            private readonly int m_capacity;
            public int capacity => m_capacity;

            /// <summary>
            /// 内部构造函数
            /// </summary>
            internal unsafe ParallelReadWriter(
                BigCatNativeFixedCapacityList<T>* pList,
                int* pLength,
                int capacity)
            {
                m_pFixedCapacityList = pList;
                m_pLength = pLength;
                m_capacity = capacity;
            }

            /// <summary>
            /// 线程安全的索引访问（只读）
            /// </summary>
            public T this[int index]
            {
                get
                {
                    return UnsafeUtility.ReadArrayElement<T>(m_pFixedCapacityList->GetUnsafePtr(), index);
                }
            }

            /// <summary>
            /// 线程安全的索引访问（只读）
            /// </summary>
            public T* ElementAt(int index)
            {
                return m_pFixedCapacityList->ElementAt(index);
            }

            /// <summary>
            /// 线程安全的添加方法（不会自动扩容）
            /// </summary>
            /// <param name="value">要添加的元素</param>
            public void AddNoResize(in T value)
            {
                // 原子递增获取唯一索引
                int index = Interlocked.Increment(ref *m_pLength) - 1;

                // 检查容量边界
                if (index >= m_capacity)
                {
                    // 回滚长度计数器
                    Interlocked.Decrement(ref *m_pLength);
                    throw new System.InvalidOperationException($"容量溢出：尝试在索引 {index} 处添加元素，但容量仅为 {m_capacity}");
                }

                // 直接写入内存位置
                UnsafeUtility.WriteArrayElement(m_pFixedCapacityList->GetUnsafePtr(), index, value);
            }

            /// <summary>
            /// 线程安全的添加多个元素（不会自动扩容）
            /// </summary>
            /// <param name="list">要添加的List</param>
            public void AddRangeNoResize(NativeList<T> list)
            {
                var listCount = list.Length;
                if (listCount > 0)
                {
                    // 原子性地预留空间，获取起始索引
                    int startIndex = Interlocked.Add(ref *m_pLength, listCount) - listCount;

                    // 检查容量边界
                    if (startIndex + list.Length > m_capacity)
                    {
                        // 回滚长度计数器
                        Interlocked.Add(ref *m_pLength, -list.Length);
                        throw new System.InvalidOperationException($"容量溢出：尝试添加 {listCount} 个元素到索引 {startIndex}，但容量仅为 {m_capacity}");
                    }

                    // 批量内存拷贝
                    var elementSize = UnsafeUtility.SizeOf<T>();
                    var dstPtr = (byte*)m_pFixedCapacityList->GetUnsafePtr() + startIndex * elementSize;
                    var srcPtr = list.GetUnsafeReadOnlyPtr();
                    UnsafeUtility.MemCpy(dstPtr, srcPtr, listCount * elementSize);
                }
            }
        }

        /// <summary>
        /// 创建用于并行写入的Writer
        /// </summary>
        /// <returns>ParallelWriter实例</returns>
        public ParallelReadWriter AsParallelReadWriter()
        {
            int* lengthPtr = (int*)UnsafeUtility.AddressOf(ref m_fixedCapacityList->length);
            return new ParallelReadWriter(m_fixedCapacityList, lengthPtr, m_fixedCapacityList->capacity);
        }
        #endregion
    }
}