using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace BigCat.Boids
{
    public class BoidsManager : MonoBehaviour
    {
        /// <summary>
        /// 单例
        /// </summary>
        private static BoidsManager s_instance;
        public static BoidsManager instance => s_instance;

        public static bool TryGetInstance(out BoidsManager i)
        {
            i = s_instance;
            return i != null;
        }

        /// <summary>
        /// 游戏是否已经销毁
        /// </summary>
        private bool m_hasApplicationQuit = false;

        /// <summary>
        /// BRG
        /// </summary>
        private BatchRendererGroup m_brg;
        public BatchRendererGroup brg => m_brg;

        /// <summary>
        /// Boids Batch Render Group ID
        /// </summary>
        private int m_boidsBrgID = 0;

        /// <summary>
        /// Boids Batch Render Groups
        /// </summary>
        private readonly List<BoidsGroup> m_boidsGroups = new();
        private readonly List<BoidsBatchRenderGroup> m_boidsBrgs = new();

        /// <summary>
        /// 被删除的Boids Batch Render Groups
        /// </summary>
        private readonly List<BoidsGroup> m_boidsBrgsToRemove = new();

        /// <summary>
        /// 预裁剪Job句柄
        /// </summary>
        private JobHandle m_preCullingJobHandle;

        private void Awake()
        {
            s_instance = this;

            m_brg = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);
        }

        private void Update()
        {
            // 结束上一帧的预裁剪Job
            CompeltedPreCullingJobs();
        }

        private void LateUpdate()
        {
            // 同步上一帧的预裁剪结果
            SyncPreCullingResults();

            // 创建新的预裁剪Job
            CreatePreCullingJobs();
        }

        /// <summary>
        /// 游戏销毁时调用
        /// </summary>
        private void OnApplicationQuit()
        {
            m_hasApplicationQuit = true;
        }

        private void OnDestroy()
        {
            if (m_brg != null)
            {
                m_brg.Dispose();
                m_brg = null;
            }
        }

        #region BoidsGroup Management
        /// <summary>
        /// 注册一个Boids Batch Render Group
        /// </summary>
        public void RegisterBoidsGroup(BoidsGroup boidsGroup)
        {
            if (m_boidsGroups.Contains(boidsGroup))
            {
                throw new Exception($"BoidsGroup {boidsGroup.name} is already registered.");
            }
            var boidsBrg = new BoidsBatchRenderGroup(++m_boidsBrgID);
            boidsBrg.Initialize(boidsGroup.spawnMaterial, boidsGroup.spawnMesh, boidsGroup.data.instanceMatrices, boidsGroup.graphicsBuffer, m_brg);
            m_boidsGroups.Add(boidsGroup);
            m_boidsBrgs.Add(boidsBrg);
        }

        /// <summary>
        /// 注销一个Boids Batch Render Group
        /// </summary>
        public void UnregisterBoidsBrg(BoidsGroup boidsGroup)
        {
            m_boidsBrgsToRemove.Add(boidsGroup);
        }
        #endregion

        #region PreCulling
        /// <summary>
        /// 创建预裁剪Job
        /// </summary>
        private void CreatePreCullingJobs()
        {
            var dependencies = new NativeArray<JobHandle>(m_boidsBrgs.Count, Allocator.Temp);
            for (var i = 0; i < m_boidsBrgs.Count; ++i)
            {
                var boidsBrg = m_boidsBrgs[i];
                if (boidsBrg.batches.Length > 0)
                {
                    var boidsGroup = m_boidsGroups[i];
                    var boidsGroupData = boidsGroup.data;
                    boidsGroupData.Clear();

                    // 首先创建BoidsGroupJob
                    var boidsGroupJob = new BoidsGroupJob(
                        boidsGroupData.instancePositions,
                        boidsGroupData.instanceRotations,
                        boidsGroup.macroGroupRange,
                        boidsGroupData.macroGroupInfos,
                        boidsGroupData.macroGroupIndices,
                        boidsGroupData.realGroupCounts);
                    var boidsGroupJobHandle = boidsGroupJob.Schedule();

                    // BoidsRefreshVelocityJob
                    var boidsRefreshVelocityJob = new BoidsRefreshVelocityJob(
                        boidsGroupData.instancePositions,
                        boidsGroupData.instanceRotations,
                        boidsGroupData.instanceVelocities,
                        boidsGroupData.macroGroupInfos,
                        boidsGroupData.macroGroupIndices,
                        boidsGroupData.goalPositions,
                        boidsGroup.rotateSpeed,
                        boidsGroup.groupSeparationWeight,
                        boidsGroup.groupAlignmentWeight,
                        boidsGroup.groupCohesionWeight,
                        boidsGroup.goalWeight,
                        Time.deltaTime);
#if UNITY_ANDROID || UNITY_IOS
                    var boidsRefreshVelocityJobHandle = boidsRefreshVelocityJob.Schedule(boidsGroup.spawnCount, 64, boidsGroupJobHandle);
#else
                    var boidsRefreshVelocityJobHandle = boidsRefreshVelocityJob.Schedule(boidsGroup.spawnCount, 128, boidsGroupJobHandle);
#endif

                    // BoidsRefreshTransformJob
                    var boidsRefreshTransformJob = new BoidsRefreshTransformJob(
                        boidsGroupData.instancePositions,
                        boidsGroupData.instanceRotations,
                        boidsGroupData.instanceScales,
                        boidsGroupData.instanceVelocities,
                        boidsGroupData.instanceMatrices,
                        boidsGroup.moveSpeed,
                        Time.deltaTime);

#if UNITY_ANDROID || UNITY_IOS
                    dependencies[i] = boidsRefreshTransformJob.Schedule(boidsGroup.spawnCount, 64, boidsGroupJobHandle);
#else
                    dependencies[i] = boidsRefreshTransformJob.Schedule(boidsGroup.spawnCount, 128, boidsGroupJobHandle);
#endif
                }
            }
            m_preCullingJobHandle = JobHandle.CombineDependencies(dependencies);
        }

        /// <summary>
        /// 结束预裁剪Job
        /// </summary>
        private void CompeltedPreCullingJobs()
        {
            m_preCullingJobHandle.Complete();
        }

        /// <summary>
        /// 同步预裁剪的结果
        /// </summary>
        private void SyncPreCullingResults()
        {
            if (BigCatGraphics.isOpenGLES)
            {
            }
            else
            {
                for (var i = 0; i < m_boidsGroups.Count; ++i)
                {
                    var boidsGroup = m_boidsGroups[i];
                    var boidsGroupData = boidsGroup.data;
                    var boidsBrg = m_boidsBrgs[i];
                    var graphicsBuffer = boidsGroup.graphicsBuffer;
                    graphicsBuffer.LockBufferForWrite();

                    var boidsBatches = boidsBrg.batches;
                    for (var batchIndex = 0; batchIndex < boidsBatches.Length; ++batchIndex)
                    {
                        var batch = boidsBatches[batchIndex];
                        graphicsBuffer.BeginFillData();
                        graphicsBuffer.FillData(boidsGroupData.instanceMatrices, batch.instanceOffset, batch.instanceCount);
                        graphicsBuffer.EndFillData();
                    }

                    graphicsBuffer.UnlockBufferAfterWrite();

                    // 重新调整实例数据的大小
                    boidsGroupData.Resize();
                }
            }
        }
#endregion

        #region Culling
        /// <summary>
        /// 裁剪回调
        /// </summary>
        [BurstCompile]
        private unsafe JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            if (!m_hasApplicationQuit && cullingContext.viewType == BatchCullingViewType.Camera)
            {
                CollectDrawCommandData(out var drawCommandCount, out var visibleInstanceCount);

                var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
                drawCommands->drawCommands = MallocTempJob<BatchDrawCommand>((uint)drawCommandCount);
                drawCommands->drawCommandCount = drawCommandCount;
                drawCommands->drawCommandPickingInstanceIDs = null;
                drawCommands->instanceSortingPositions = null;
                drawCommands->instanceSortingPositionFloatCount = 0;
                drawCommands->drawRanges = MallocTempJob<BatchDrawRange>(1);
                drawCommands->drawRangeCount = 1;

                //DefaultDrawRange
                var defaultDrawRange = drawCommands->drawRanges;
                defaultDrawRange->drawCommandsBegin = 0;
                defaultDrawRange->drawCommandsCount = (uint)drawCommandCount;
                defaultDrawRange->filterSettings = new BatchFilterSettings
                {
                    renderingLayerMask = 0xffffffff,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    staticShadowCaster = false,
                };

                //Draw Instances
                drawCommands->visibleInstances = MallocTempJob<int>((uint)visibleInstanceCount);
                drawCommands->visibleInstanceCount = visibleInstanceCount;
                var drawCommandOffset = 0;
                var instanceOffset = 0;
                foreach (var boidsBrg in m_boidsBrgs)
                {
                    for (var batchIndex = 0; batchIndex < boidsBrg.batches.Length; ++batchIndex)
                    {
                        var batch = boidsBrg.batches[batchIndex];
                        if (batch.instanceCount > 0)
                        {
                            var drawCommand = drawCommands->drawCommands + (drawCommandOffset++);
                            drawCommand->batchID = batch.batchID;
                            drawCommand->materialID = boidsBrg.materialID;
                            drawCommand->meshID = boidsBrg.meshID;
                            drawCommand->submeshIndex = 0;
                            drawCommand->splitVisibilityMask = 0xff;
                            drawCommand->flags = 0;
                            drawCommand->sortingPosition = 0;
                            drawCommand->visibleCount = (uint)batch.instanceCount;
                            drawCommand->visibleOffset = (uint)instanceOffset;

                            for (var i = 0; i < batch.instanceCount; ++i)
                            {
                                drawCommands->visibleInstances[instanceOffset++] = i;
                            }
                        }
                    }
                }

            }
            return new JobHandle();
        }

        /// <summary>
        /// 收集绘制命令数据
        /// </summary>
        private void CollectDrawCommandData(out int drawCommandCount, out int visibleInstanceCount)
        {
            drawCommandCount = 0;
            visibleInstanceCount = 0;
            for (var i = 0; i < m_boidsBrgs.Count; i++)
            {
                var boidsBrg = m_boidsBrgs[i];
                drawCommandCount += boidsBrg.batches.Length;
                foreach (var batch in boidsBrg.batches)
                {
                    visibleInstanceCount += batch.instanceCount;
                }
            }
        }
        #endregion

        #region Utility
        /// <summary>
        /// 分配内存
        /// </summary>
        public static unsafe T* MallocTempJob<T>(uint count) where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), Allocator.TempJob);
        }
        #endregion
    }
}