using Unity.Collections.LowLevel.Unsafe;

namespace BigCat.NativeCollections
{
    public struct BigCatMemorySlice
    {
        /// <summary>
        /// 内存指针
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        private unsafe readonly void* m_buffer;

        /// <summary>
        /// 切片的内存地址
        /// </summary>
        private readonly int m_address;
        public readonly int address => m_address;

        /// <summary>
        /// 切片大小
        /// </summary>
        private readonly int m_sliceSize;
        public readonly int sliceSize => m_sliceSize;

        /// <summary>
        /// Slice所在的Block的ID
        /// 前8位是Block所属的Chunk的ID
        /// 后8位是Block在Chunk中的ID
        /// </summary>
        private readonly int m_blockID;
        public readonly int blockID => m_blockID;

        /// <summary>
        /// 该Slice的起始地址指针
        /// </summary>
        public readonly unsafe void* pointer => (byte*)m_buffer + (long)m_address;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="inBuffer">Buffer指针</param>
        /// <param name="inAddress">该Slice的起始的地址</param>
        /// <param name="inBlockID">Slice所属的Block的ID</param>
        public unsafe BigCatMemorySlice(void* inBuffer, int inAddress, int inSliceSize, int inBlockID)
        {
            m_buffer = inBuffer;
            m_address = inAddress;
            m_sliceSize = inSliceSize;
            m_blockID = inBlockID;
        }
    }
}