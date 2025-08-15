using BigCat.NativeCollections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace BigCat.Boids
{
    public class BoidsBatch
    {
        /// <summary>
        /// ShaderProperty
        /// </summary>
        private static readonly int s_shaderPropertyLtw = Shader.PropertyToID("unity_ObjectToWorld");

        /// <summary>
        /// 该Batch在BRG中的ID
        /// </summary>
        public BatchID batchID;

        /// <summary>
        /// Instance在所属的BatchGroup里的偏移
        /// </summary>
        public readonly int instanceOffset;

        /// <summary>
        /// 该Batch绘制的所有的Instance的数量
        /// </summary>
        public readonly int instanceCount;

        /// <summary>
        /// 构造函数
        /// </summary>
        public BoidsBatch(int inInstanceOffset, int inInstanceCount)
        {
            instanceOffset = inInstanceOffset;
            instanceCount = inInstanceCount;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(
            in BigCatNativeArray<PackedMatrix> boidsInstanceMatrices,
            BigCatGraphicsBuffer graphicsBuffer,
            BatchRendererGroup brg)
        {
            if (BigCatGraphics.isOpenGLES)
            {

            }
            else
            {
                // 填充GraphicsBuffer数据
                var dataStartAddress = graphicsBuffer.BeginFillData();
                var dataSizeLtw = graphicsBuffer.FillData(boidsInstanceMatrices, instanceOffset, instanceCount);
                var windowSize = graphicsBuffer.EndFillData();

                //Fill Metadata & Add Batch
                var metadata = new NativeArray<MetadataValue>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                uint offset = dataStartAddress;
                metadata[0] = new MetadataValue { NameID = s_shaderPropertyLtw, Value = 0x80000000 | offset };
                batchID = brg.AddBatch(metadata, graphicsBuffer.bufferHandle);
            }
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Destroy(BatchRendererGroup brg)
        {
            brg.RemoveBatch(batchID);
        }
    }

    public class BoidsBatchRenderGroup
    {
        /// <summary>
        /// Instance数据的大小
        /// </summary>
        public const int sizeOfPerInstance = (BigCatGraphics.sizeOfPackedMatrix);

        /// <summary>
        /// BatchGroup的唯一ID
        /// </summary>
        private readonly int m_id;
        public int id => m_id;

        /// <summary>
        /// Batch
        /// </summary>
        private BoidsBatch[] m_batches;
        public BoidsBatch[] batches => m_batches;

        /// <summary>
        /// 材质
        /// </summary>
        private Material m_material;

        /// <summary>
        /// 材质在BRG中的注册ID
        /// </summary>
        private BatchMaterialID m_materialID;
        public BatchMaterialID materialID => m_materialID;

        /// <summary>
        /// Mesh
        /// </summary>
        private Mesh m_mesh;

        /// <summary>
        /// Mesh在BRG中的注册ID
        /// </summary>
        private BatchMeshID m_meshID;
        public BatchMeshID meshID => m_meshID;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="inId">唯一ID</param>
        public BoidsBatchRenderGroup(int inId)
        {
            m_id = inId;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(
            Material inMaterial,
            Mesh inMesh,
            in BigCatNativeArray<PackedMatrix> boidsInstanceMatrices,
            BigCatGraphicsBuffer graphicsBuffer,
            BatchRendererGroup brg)
        {
            // 注册材质和Mesh
            m_material = inMaterial;
            m_materialID = brg.RegisterMaterial(m_material);
            m_mesh = inMesh;
            m_meshID = brg.RegisterMesh(m_mesh);

            // 初始化Batch数据
            var maxInstanceCountPerBatch = BigCatGraphics.MaxInstanceCountPerBatch(sizeOfPerInstance);
            var batchCount = (boidsInstanceMatrices.length + maxInstanceCountPerBatch - 1) / maxInstanceCountPerBatch;
            m_batches = new BoidsBatch[batchCount];
            for (var batchIndex = 0; batchIndex < batchCount; ++batchIndex)
            {
                var instanceOffset = batchIndex * maxInstanceCountPerBatch;
                var instanceCount = Mathf.Min(maxInstanceCountPerBatch, boidsInstanceMatrices.length - instanceOffset);
                var batch = new BoidsBatch(instanceOffset, instanceCount);
                batch.Initialize(boidsInstanceMatrices, graphicsBuffer, brg);
                m_batches[batchIndex] = batch;
            }
        }

        public void Destroy(BatchRendererGroup brg)
        {
            // 销毁材质和Mesh
            brg.UnregisterMaterial(m_materialID);
            brg.UnregisterMesh(m_meshID);

            // 销毁所有的Batch
            if (m_batches != null)
            {
                foreach (var batch in m_batches)
                {
                    batch.Destroy(brg);
                }
            }
            m_batches = null;
        }
    }
}