using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace BigCat.NativeCollections
{
    public static unsafe class BigCatMemoryManager
    {
        #region Pool
        [BurstCompile]
        public struct MemorySlice
        {
            /// <summary>
            /// 内存指针
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            private unsafe void* m_buffer;

            /// <summary>
            /// 切片的内存地址
            /// </summary>
            public int address => (int)((byte*)pointer - (byte*)m_buffer);

            /// <summary>
            /// 该Slice的起始地址指针
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            public unsafe void* pointer;

            /// <summary>
            /// 切片大小
            /// </summary>
            private int m_sliceSize;
            public int sliceSize => m_sliceSize;

            /// <summary>
            /// Slice所在的Block的ID
            /// 前8位是Block所属的Chunk的ID
            /// 后8位是Block在Chunk中的ID
            /// </summary>
            private int m_blockID;
            public int blockID => m_blockID;

            /// <summary>
            /// 构造函数
            /// </summary>
            public MemorySlice(void* inBuffer, int inAddress, int inSliceSize, int inBlockID)
            {
                m_buffer = inBuffer;
                m_sliceSize = inSliceSize;
                m_blockID = inBlockID;

                pointer = m_buffer == null ? null : (byte*)m_buffer + (long)inAddress;
            }

            /// <summary>
            /// 初始化
            /// </summary>
            public unsafe void Initialize(void* inBuffer, int inAddress, int inSliceSize, int inBlockID)
            {
                m_buffer = inBuffer;
                m_sliceSize = inSliceSize;
                m_blockID = inBlockID;

                pointer = m_buffer == null ? null : (byte*)m_buffer + (long)inAddress;
            }
        }

        [BurstCompile]
        public struct MemoryBlock
        {
            /// <summary>
            /// Block的ID
            /// </summary>
            public int id;

            /// <summary>
            /// 内存地址
            /// </summary>
            private void* m_buffer;

            /// <summary>
            /// 该Block的Slice大小
            /// </summary>
            public int sliceSize;

            /// <summary>
            /// 剩余可用的Slice地址列表
            /// </summary>
            private UnsafeList<int> m_freeSliceAddresses;

            /// <summary>
            /// 是否还有未使用的切片
            /// </summary>
            public bool hasFreeSlice => m_freeSliceAddresses.Length > 0;

            /// <summary>
            /// 是否还有使用中的切片
            /// </summary>
            public bool hasUsingSlice => m_freeSliceAddresses.Length < (blockSize / sliceSize);

            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="blockID">BlockID</param>
            /// <param name="blockBuffer">Block地址</param>
            public MemoryBlock(int blockID, void* blockBuffer)
            {
                id = blockID;
                m_buffer = blockBuffer;
                sliceSize = 0;
                m_freeSliceAddresses = default;
            }

            /// <summary>
            /// Dispose函数
            /// </summary>
            [BurstCompile]
            public void Dispose()
            {
                if (m_freeSliceAddresses.IsCreated)
                {
                    m_freeSliceAddresses.Dispose();
                }
            }

            /// <summary>
            /// 设置Slice的大小
            /// </summary>
            [BurstCompile]
            public void SetSliceSize(int size)
            {
                sliceSize = size;

                // 计算该Block可以容纳的Slice数量
                var sliceCount = blockSize / sliceSize;
                if (m_freeSliceAddresses.IsCreated)
                {
                    m_freeSliceAddresses.Resize(sliceCount);
                    m_freeSliceAddresses.Clear();
                }
                else
                {
                    m_freeSliceAddresses = new UnsafeList<int>(sliceCount, Allocator.Persistent);
                }
                for (var sliceIndex = 0; sliceIndex < sliceCount; ++sliceIndex)
                {
                    m_freeSliceAddresses.AddNoResize(sliceIndex * sliceSize);
                }
            }

            /// <summary>
            /// 从指定的Block中分配内存切片
            /// </summary>
            [BurstCompile]
            public void AllocateSlice(ref MemorySlice slice)
            {
                var freeSliceIndex = m_freeSliceAddresses.Length - 1;
                var freeSliceAddress = m_freeSliceAddresses[freeSliceIndex];
                m_freeSliceAddresses.RemoveAt(freeSliceIndex);
                slice.Initialize(m_buffer, freeSliceAddress, sliceSize, id);
            }

            /// <summary>
            /// 释放内存切片
            /// </summary>
            [BurstCompile]
            public void ReleaseSlice(MemorySlice slice)
            {
                m_freeSliceAddresses.AddNoResize(slice.address);
            }
        }

        [BurstCompile]
        public struct MemoryChunk
        {
            /// <summary>
            /// ChunkID
            /// </summary>
            public int id;

            /// <summary>
            /// Chunk内存对齐方式
            /// </summary>
            public int alignment;

            /// <summary>
            /// Chunk内存地址
            /// </summary>
            private void* m_buffer;

            /// <summary>
            /// Block数组，固定数量
            /// 按照Block的SliceSize从小到大排序
            /// </summary>
            private UnsafeList<MemoryBlock> m_blocks;

            /// <summary>
            /// 未使用的Block索引
            /// </summary>
            private int m_freeBlockIndex;

            /// <summary>
            /// 是否还有未使用的Block
            /// </summary>
            private bool hasFreeBlock => m_freeBlockIndex < m_blocks.Length;

            /// <summary>
            /// 构造函数
            /// </summary>
            public MemoryChunk(int chunkID, int chunkAlignment)
            {
                id = chunkID;
                alignment = chunkAlignment;
                m_freeBlockIndex = 0;

                // 分配Chunk内存
                m_buffer = UnsafeUtility.Malloc(chunkSize, alignment, Allocator.Persistent);
                if (m_buffer == null)
                {
                    throw new System.Exception("BigWorldMemoryManager: Failed to allocate memory chunk.");
                }

                // 创建Block
                var blockCount = chunkSize / blockSize;
                m_blocks = new UnsafeList<MemoryBlock>(blockCount, Allocator.Persistent);
                for (var blockIndex = 0; blockIndex < blockCount; ++blockIndex)
                {
                    var blockID = (id << 8) | blockIndex;
                    var blockBuffer = (byte*)m_buffer + (blockIndex * blockSize);
                    m_blocks.AddNoResize(new MemoryBlock(blockID, blockBuffer));
                }
            }

            /// <summary>
            /// Dispose函数
            /// </summary>
            [BurstCompile]
            public void Dispose()
            {
                // 释放Chunk内存
                if (m_buffer != null)
                {
                    UnsafeUtility.Free(m_buffer, Allocator.Persistent);
                    m_buffer = null;
                }

                // 释放Block内存
                if (m_blocks.IsCreated)
                {
                    for (var i = 0; i < m_blocks.Length; ++i)
                    {
                        var pBlock = m_blocks.Ptr + i;
                        pBlock->Dispose();
                    }
                    m_blocks.Dispose();
                }
            }

            /// <summary>
            /// 尝试从Chunk中分配内存切片
            /// </summary>
            /// <param name="sliceSize">需要分配的内存切片的大小</param>
            /// <param name="slice">内存切片</param>
            /// <returns>是否分配成功</returns>
            [BurstCompile]
            public bool TryAllocateSlice(int sliceSize, ref MemorySlice slice)
            {
                for (var i = 0; i < m_freeBlockIndex; ++i)
                {
                    var pBlock = m_blocks.Ptr + i;
                    if (pBlock->hasFreeSlice)
                    {
                        if (pBlock->sliceSize == sliceSize)
                        {
                            // 分配内存切片
                            pBlock->AllocateSlice(ref slice);
                            return true;
                        }
                        else if (pBlock->sliceSize > sliceSize)
                        {
                            // 没有找到与该SliceSize大小一致并且有剩余Slice的Block，则按照下面逻辑从大于该SliceSize的Block中分配Slice
                            // 如果该Chunk还有未分配的Block，则不从SliceSize大于所需SliceSize的Block中分配Slice
                            // 如果该Chunk没有未分配的Block，则从SliceSize大于所需SliceSize的Block中分配Slice
                            if (!hasFreeBlock)
                            {
                                // 分配内存切片
                                pBlock->AllocateSlice(ref slice);
                                return true;
                            }
                        }
                    }
                }

                // 从已经使用的Block中没有找到合适的Block用来分配Slice，需要从未使用的Block中分配Slice
                if (hasFreeBlock)
                {
                    // 获取未使用的Block，并设置Slice大小
                    var blockIndex = m_freeBlockIndex++;
                    var pBlock = m_blocks.Ptr + blockIndex;
                    pBlock->SetSliceSize(sliceSize);

                    // 分配内存切片
                    pBlock->AllocateSlice(ref slice);

                    // 对已经使用的Block按照SliceSize从小到大排序
                    SortUsingBlocks();
                    return true;
                }
                return false;
            }

            /// <summary>
            /// 释放内存切片
            /// </summary>
            [BurstCompile]
            public void ReleaseSlice(MemorySlice slice)
            {
                for (var i = 0; i < m_freeBlockIndex; ++i)
                {
                    var pBlock = m_blocks.Ptr + i;
                    if (pBlock->id == slice.blockID)
                    {
                        pBlock->ReleaseSlice(slice);

                        // 如果该Block的Slice全部释放完毕，则将其标记为未使用
                        if (!pBlock->hasUsingSlice)
                        {
                            var block = *pBlock;
                            m_blocks.RemoveAt(i);
                            m_blocks.AddNoResize(block);
                            --m_freeBlockIndex;
                        }
                        return;
                    }
                }
            }

            /// <summary>
            /// 对已经使用的Block按照SliceSize从小到大排序
            /// </summary>
            [BurstCompile]
            public void SortUsingBlocks()
            {
                // 使用插入排序对索引0到freeBlockIndex-1之间的Block按sliceSize排序
                // 插入排序对小数组很高效，且Burst友好
                var blocksPtr = m_blocks.Ptr;
                for (int i = 1; i < m_freeBlockIndex; i++)
                {
                    var currentBlock = blocksPtr[i];
                    var currentSliceSize = currentBlock.sliceSize;
                    int j = i - 1;

                    // 向后移动所有sliceSize大于currentSliceSize的元素
                    while (j >= 0 && (blocksPtr +j)->sliceSize > currentSliceSize)
                    {
                        blocksPtr[j + 1] = blocksPtr[j];
                        j--;
                    }

                    // 将当前Block插入到正确位置
                    blocksPtr[j + 1] = currentBlock;
                }
            }
        }

        [BurstCompile]
        public struct MemoryChunkOfFcl
        {
            /// <summary>
            /// Chunk内存对齐方式
            /// </summary>
            public int alignment;

            /// <summary>
            /// 切片大小
            /// </summary>
            public int sliceSize;

            /// <summary>
            /// Chunk内存地址
            /// </summary>
            private void* m_buffer;

            /// <summary>
            /// 剩余可用的Slice地址列表
            /// </summary>
            private UnsafeList<int> m_freeSliceAddresses;

            /// <summary>
            /// 构造函数
            /// </summary>
            public MemoryChunkOfFcl(int inAlignment, int inSliceSize)
            {
                alignment = inAlignment;
                sliceSize = inSliceSize;

                // 分配Chunk内存
                m_buffer = UnsafeUtility.Malloc(sliceSize * fclSliceCount, alignment, Allocator.Persistent);

                // 初始化未使用的Slice列表
                m_freeSliceAddresses = new UnsafeList<int>(fclSliceCount, Allocator.Persistent);
                for (var sliceIndex = 0; sliceIndex < fclSliceCount; ++sliceIndex)
                {
                    m_freeSliceAddresses.AddNoResize(sliceIndex * sliceSize);
                }
            }

            /// <summary>
            /// Dispose函数
            /// </summary>
            [BurstCompile]
            public void Dispose()
            {
                // 释放未使用的Slice列表
                if (m_freeSliceAddresses.IsCreated)
                {
                    m_freeSliceAddresses.Dispose();
                }

                // 释放Chunk内存
                if (m_buffer != null)
                {
                    UnsafeUtility.Free(m_buffer, Allocator.Persistent);
                    m_buffer = null;
                }
            }

            /// <summary>
            /// 分配内存指针
            /// </summary>
            [BurstCompile]
            public void* AllocatePointer()
            {
                if (m_freeSliceAddresses.Length == 0)
                {
                    throw new System.Exception("BigCatMemoryManager: No free slices available in FCL chunk.");
                }

                // 获取未使用的Slice地址
                var freeSliceIndex = m_freeSliceAddresses.Length - 1;
                var freeSliceAddress = m_freeSliceAddresses[freeSliceIndex];
                m_freeSliceAddresses.RemoveAt(freeSliceIndex);

                // 返回分配的内存指针
                return (byte*)m_buffer + (long)freeSliceAddress;
            }

            /// <summary>
            /// 释放内存指针
            /// </summary>
            [BurstCompile]
            public void ReleasePointer(void* pointer)
            {
                // 计算Slice地址
                var sliceAddress = (byte*)pointer - (byte*)m_buffer;
                m_freeSliceAddresses.AddNoResize((int)sliceAddress);
            }
        }

        [BurstCompile]
        public struct MemoryPool
        {
            /// <summary>
            /// 下一个Chunk的ID
            /// </summary>
            public int nextChunkID;

            /// <summary>
            /// MemoryChunk列表
            /// </summary>
            public UnsafeList<MemoryChunk> chunks;

            /// <summary>
            /// FCL专用的MemoryChunk
            /// </summary>
            public MemoryChunkOfFcl fclChunk;
        }
        #endregion

        /// <summary>
        /// 每个Chunk的大小
        /// </summary>
#if UNITY_ANDROID || UNITY_IOS
        private const int chunkSize = 1024 * 1024 * 2; // 2MB
#else
        private const int chunkSize = 1024 * 1024 * 4; // 4MB
#endif

        /// <summary>
        /// 每个Block的大小
        /// </summary>
        private const int blockSize = 1024 * 128; // 128K

        /// <summary>
        /// FCL所有Slice的数量
        /// </summary>
#if UNITY_ANDROID || UNITY_IOS
        private const int fclSliceCount = 512;
#else
        private const int fclSliceCount = 1024;
#endif

        /// <summary>
        /// 内存池
        /// </summary>
        private static MemoryPool* s_memoryPool;

        /// <summary>
        /// 引用计数
        /// </summary>
        private static int s_refCount = 0;

        /// <summary>
        /// 初始化
        /// </summary>
        public static void Initialize()
        {
            if (s_refCount++ == 0)
            {
#if UNITY_EDITOR
                NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
#endif

                // 分配内存池
                s_memoryPool = (MemoryPool*)UnsafeUtility.Malloc(
                    UnsafeUtility.SizeOf<MemoryPool>(),
                    UnsafeUtility.AlignOf<MemoryPool>(),
                    Allocator.Persistent);

                // 初始化内存池
                var fclAlignment = UnsafeUtility.AlignOf<BigCatNativeFixedCapacityList<int>>();
                var fclSliceSize = UnsafeUtility.SizeOf<BigCatNativeFixedCapacityList<int>>();
                *s_memoryPool = new MemoryPool
                {
                    nextChunkID = 0,
#if UNITY_ANDROID || UNITY_IOS
                    chunks = new UnsafeList<MemoryChunk>(8, Allocator.Persistent),
#else
                    chunks = new UnsafeList<MemoryChunk>(16, Allocator.Persistent),
#endif
                    fclChunk = new MemoryChunkOfFcl(fclAlignment, fclSliceSize),
                };
            }
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public static void Destroy()
        {
            if (--s_refCount == 0)
            {
                if (s_memoryPool != null)
                {
                    for (var i = 0; i < s_memoryPool->chunks.Length; ++i)
                    {
                        var pChunk = s_memoryPool->chunks.Ptr + i;
                        pChunk->Dispose();
                    }
                    s_memoryPool->chunks.Dispose();
                    s_memoryPool->fclChunk.Dispose();

                    UnsafeUtility.Free(s_memoryPool, Allocator.Persistent);
                    s_memoryPool = null;
                }
            }
        }

        /// <summary>
        /// 分配内存切片
        /// </summary>
        /// <param name="size">内存切片所需容纳的内存大小</param>
        /// <param name="alignment">内存对齐</param>
        [BurstCompile]
        public static void AllocateSlice(int size, int alignment, out MemorySlice slice)
        {
            // 计算标准的Slice大小
            var sliceSize = CalculateStandardSliceSize(size);
            if (sliceSize > blockSize)
            {
                // 请求的内存大小超过了Block的最大大小，则直接向Unity分配内存
                unsafe
                {
                    var sliceBuffer = UnsafeUtility.Malloc(size, alignment, Allocator.Persistent); 
                    slice = new MemorySlice(sliceBuffer, 0, sliceSize, -1);
                }
            }
            else
            {
                // 初始化输出的内存切片
                slice = default;

                // 从现有的MemoryChunk中获取alignment匹配的MemoryChunk
                for (var i = 0; i < s_memoryPool->chunks.Length; ++i)
                {
                    var pChunk = s_memoryPool->chunks.Ptr + i;
                    if (pChunk->alignment == alignment && pChunk->TryAllocateSlice(sliceSize, ref slice))
                    {
                        return;
                    }
                }

                // 如果没有找到合适的Chunk，则创建一个新的Chunk
                var newChunk = new MemoryChunk(s_memoryPool->nextChunkID++, alignment);
                newChunk.TryAllocateSlice(sliceSize, ref slice);
                s_memoryPool->chunks.Add(newChunk);
            }
        }

        /// <summary>
        /// 释放内存切片
        /// </summary>
        /// <param name="slice">内存切片</param>
        [BurstCompile]
        public static void ReleaseSlice(MemorySlice slice)
        {
            if (s_memoryPool != null)
            {
                if (slice.blockID == -1)
                {
                    // 如果Slice的BlockID为-1，则说明是直接向Unity分配的内存
                    UnsafeUtility.Free(slice.pointer, Allocator.Persistent);
                }
                else
                {
                    var chunkID = slice.blockID >> 8;
                    for (var i = 0; i < s_memoryPool->chunks.Length; ++i)
                    {
                        var pChunk = s_memoryPool->chunks.Ptr + i;
                        if (pChunk->id == chunkID)
                        {
                            pChunk->ReleaseSlice(slice);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 分配FCL的指针
        /// </summary>
        [BurstCompile]
        public static BigCatNativeFixedCapacityList<T>* AllocateFclPointer<T>() where T : unmanaged
        {
            if (s_memoryPool->fclChunk.alignment != UnsafeUtility.AlignOf<BigCatNativeFixedCapacityList<T>>())
            {
                throw new System.Exception($"BigCatMemoryManager: AllocateFclPointer alignment mismatch for type {typeof(T)}");
            }

            if (s_memoryPool->fclChunk.sliceSize != UnsafeUtility.SizeOf<BigCatNativeFixedCapacityList<T>>())
            {
                throw new System.Exception($"BigCatMemoryManager: AllocateFclPointer size mismatch for type {typeof(T)}");
            }

            return (BigCatNativeFixedCapacityList<T>*)s_memoryPool->fclChunk.AllocatePointer();
        }

        /// <summary>
        /// 释放FCL的指针
        /// </summary>
        [BurstCompile]
        public static void ReleaseFclPointer<T>(BigCatNativeFixedCapacityList<T>* fclPointer) where T : unmanaged
        {
            if (s_memoryPool != null)
            {
                s_memoryPool->fclChunk.ReleasePointer(fclPointer);
            }
        }

        #region Utility Methods
        /// <summary>
        /// 计算标准的Slice的大小
        /// </summary>
        [BurstCompile]
        public static int CalculateStandardSliceSize(int size)
        {
            if (size > blockSize)
            {
                return size;
            }
            else
            {
                var standardSliceSize = 8;
                while (standardSliceSize < size)
                {
                    standardSliceSize *= 4;
                }
                return standardSliceSize;
            }
        }
        #endregion
    }
}