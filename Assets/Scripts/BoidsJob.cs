using BigCat.NativeCollections;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace BigCat.Boids
{
    //[BurstCompile]
    public struct BoidsMacroGroupInfo
    {
        /// <summary>
        /// 该Group的Hash值
        /// </summary>
        public uint groupHashValue;

        /// <summary>
        /// 所有Instance的数量
        /// </summary>
        public int instanceCount;

        /// <summary>
        /// 零头Instance的索引
        /// </summary>
        public int leaderInstanceIndex;

        /// <summary>
        /// 对齐
        /// </summary>
        public float3 alignment;

        /// <summary>
        /// 聚集
        /// </summary>
        public float3 cohesion;
    }

    //[BurstCompile]
    public struct BoidsMicroGroupInfo
    {
        /// <summary>
        /// 该Group的Hash值
        /// </summary>
        public uint groupHashValue;

        /// <summary>
        /// 所有Instance的数量
        /// </summary>
        public int instanceCount;
    }

    /// <summary>
    /// 给BoidsGroup中所有Instance按照空间划分进行分组
    /// </summary>
    //[BurstCompile]
    public struct BoidsGroupJob : IJob
    {
        /// <summary>
        /// 每个Instance的位置
        /// </summary>
        [ReadOnly]
        private BigCatNativeArray<float3> m_positions;

        /// <summary>
        /// 每个Instance的旋转
        /// </summary>
        [ReadOnly]
        private BigCatNativeArray<quaternion> m_rotations;

        /// <summary>
        /// 每个大组的范围
        /// </summary>
        [ReadOnly]
        private float m_macroGroupRange;

        /// <summary>
        /// Instance划分的大组的信息
        /// </summary>
        private BigCatNativeList<BoidsMacroGroupInfo>.ParallelReadWriter m_macroGroupInfos;

        /// <summary>
        /// 每个Instance划分的大组的索引
        /// </summary>
        private BigCatNativeArray<int> m_macroGroupIndices;

        /// <summary>
        /// 用于存储大组的真实数量的数组
        /// </summary>
        private BigCatNativeArray<int> m_realGroupCounts;

        public BoidsGroupJob(
            in BigCatNativeArray<float3> positions,
            in BigCatNativeArray<quaternion> rotations,
            float macroGroupRange,
            in BigCatNativeList<BoidsMacroGroupInfo> macroGroupInfos,
            in BigCatNativeArray<int> macroGroupIndices,
            in BigCatNativeArray<int> realGroupCounts)
        {
            m_positions = positions;
            m_rotations = rotations;
            m_macroGroupRange = macroGroupRange;
            m_macroGroupInfos = macroGroupInfos.AsParallelReadWriter();
            m_macroGroupIndices = macroGroupIndices;
            m_realGroupCounts = realGroupCounts;
        }

        //[BurstCompile]
        public void Execute()
        {
            var realMacroGroupCount = 0;
            for (var i = 0; i < m_positions.length; ++i)
            {
                var position = m_positions[i];
                var forward = math.forward(m_rotations[i]);
                var macroGroupHash = math.hash(new int3(math.floor(position / m_macroGroupRange)));
                var macroGroupIndex = GetMacroGroupIndex(macroGroupHash);
                if (macroGroupIndex < 0)
                {
                    //首先检测当前的数组数量是否已经超过上限
                    if (++realMacroGroupCount >= m_macroGroupInfos.capacity)
                    {
                        // 如果超过了容量，则设置为-1
                        m_macroGroupIndices[i] = -1;
                        continue;
                    }

                    // 如果没有超过容量，则添加新的大组信息
                    m_macroGroupIndices[i] = m_macroGroupInfos.length;
                    m_macroGroupInfos.AddNoResize(new BoidsMacroGroupInfo
                    {
                        groupHashValue = macroGroupHash,
                        instanceCount = 1,
                        leaderInstanceIndex = i,
                        alignment = forward,
                        cohesion = position
                    });
                }
                else
                {
                    m_macroGroupIndices[i] = macroGroupIndex;
                    unsafe
                    {
                        var pMacroGroupInfo = m_macroGroupInfos.ElementAt(macroGroupIndex);
                        ++pMacroGroupInfo->instanceCount;
                        pMacroGroupInfo->alignment += forward;
                        pMacroGroupInfo->cohesion += position;
                    }
                }
            }
            m_realGroupCounts[0] = realMacroGroupCount;
        }

        //[BurstCompile]
        private unsafe int GetMacroGroupIndex(uint macroGroupHash)
        {
            for (var i = 0; i < m_macroGroupInfos.length; ++i)
            {
                var pMacroGroupInfo = m_macroGroupInfos.ElementAt(i);
                if (pMacroGroupInfo->groupHashValue == macroGroupHash)
                {
                    // 返回找到的宏组索引
                    return i;
                }
            }

            // 未找到对应的宏组
            return -1;
        }
    }

    /// <summary>
    /// 刷新Boids的速度
    /// </summary>
    //[BurstCompile]
    public struct BoidsRefreshVelocityJob : IJobParallelFor
    {
        /// <summary>
        /// Instance的位置
        /// </summary>
        [ReadOnly]
        private BigCatNativeArray<float3> m_positions;

        /// <summary>
        /// Instance的旋转
        /// </summary>
        [ReadOnly]
        private BigCatNativeArray<quaternion> m_rotations;

        /// <summary>
        /// Instance的速度
        /// </summary>
        private BigCatNativeArray<float3> m_velocities;

        /// <summary>
        /// Instance划分的大组的信息
        /// </summary>
        [ReadOnly]
        private BigCatNativeList<BoidsMacroGroupInfo>.ParallelReader m_macroGroupInfos;

        /// <summary>
        /// 每个Instance划分的大组的索引
        /// </summary>
        [ReadOnly]
        private BigCatNativeArray<int> m_macroGroupIndices;

        /// <summary>
        /// Goal位置
        /// </summary>
        [ReadOnly]
        private BigCatNativeArray<float3> m_goalPositions;

        /// <summary>
        /// 最大转向速度
        /// </summary>
        [ReadOnly]
        private float m_rotateSpeed;

        /// <summary>
        /// 分离权重
        /// </summary>
        [ReadOnly]
        private float m_separationWeight;

        /// <summary>
        /// 对齐权重
        /// </summary>
        [ReadOnly]
        private float m_alignmentWeight;

        /// <summary>
        /// 聚集权重
        /// </summary>
        [ReadOnly]
        private float m_cohesionWeight;

        /// <summary>
        /// 聚集权重
        /// </summary>
        [ReadOnly]
        private float m_goalWeight;

        /// <summary>
        /// 更新间隔时间
        /// </summary>
        [ReadOnly]
        private float m_deltaTime;

        public BoidsRefreshVelocityJob(
            in BigCatNativeArray<float3> positions,
            in BigCatNativeArray<quaternion> rotations,
            in BigCatNativeArray<float3> velocities,
            in BigCatNativeList<BoidsMacroGroupInfo> macroGroupInfos,
            in BigCatNativeArray<int> macroGroupIndices,
            in BigCatNativeArray<float3> goalPositions,
            float rotateSpeed,
            float separationWeight,
            float alignmentWeight,
            float cohesionWeight,
            float goalWeight,
            float deltaTime)
        {
            m_positions = positions;
            m_rotations = rotations;
            m_velocities = velocities;
            m_macroGroupInfos = macroGroupInfos.AsParallelReader();
            m_macroGroupIndices = macroGroupIndices;

            m_goalPositions = goalPositions;

            m_rotateSpeed = rotateSpeed;

            m_separationWeight = separationWeight;
            m_alignmentWeight = alignmentWeight;
            m_cohesionWeight = cohesionWeight;
            m_goalWeight = goalWeight;

            m_deltaTime = deltaTime;
        }

        //[BurstCompile]
        public void Execute(int index)
        {
            var curPosition = m_positions[index];
            var curVelocity = math.forward(m_rotations[index]);

            var finalVelocity = float3.zero;

            var macroGroupIndex = m_macroGroupIndices[index];
            if (macroGroupIndex < 0)
            {
                finalVelocity = curVelocity;
            }
            else
            {
                unsafe
                {
                    var pMacroGroupInfo = m_macroGroupInfos.ElementAt(macroGroupIndex);
                    var macroInstanceCount = pMacroGroupInfo->instanceCount;

                    // 计算对齐
                    finalVelocity += m_alignmentWeight * (math.normalizesafe(pMacroGroupInfo->alignment / macroInstanceCount) - curVelocity);

                    // 计算分离
                    finalVelocity += m_cohesionWeight * math.normalizesafe(pMacroGroupInfo->cohesion / macroInstanceCount - curPosition);
                }
            }

            // 计算Goal
            var goalPosition = GetNearestPosition(m_goalPositions, curPosition);
            var deltaGoal = goalPosition - curPosition;
            var goalWeight = m_goalWeight * math.clamp(math.length(deltaGoal) / 10f, 0.5f, 2f);
            var goalDirection = math.normalizesafe(deltaGoal) * goalWeight;
            finalVelocity += goalDirection * goalWeight;

            // 计算速度
            finalVelocity = math.normalizesafe(finalVelocity);

            // 限制转向速度
            finalVelocity = curVelocity + (finalVelocity - curVelocity) * m_rotateSpeed * m_deltaTime;
            m_velocities[index] = finalVelocity;
        }

        //[BurstCompile]
        private float3 GetNearestPosition(in BigCatNativeArray<float3> positions, float3 position)
        {
            var index = 0;
            var distance = math.lengthsq(position - positions[0]);
            for (var i = 1; i < positions.length; ++i)
            {
                var delta = position - positions[i];
                var deltaLength = math.lengthsq(delta);
                if (deltaLength < distance)
                {
                    distance = deltaLength;
                    index = i;
                }
            }
            return positions[index];
        }
    }

    /// <summary>
    /// 刷新Boids的Transform
    /// </summary>
    //[BurstCompile]
    public struct BoidsRefreshTransformJob : IJobParallelFor
    {
        private BigCatNativeArray<float3> m_positions;
        private BigCatNativeArray<quaternion> m_rotations;
        private BigCatNativeArray<float3> m_scales;
        private BigCatNativeArray<float3> m_velocities;
        private BigCatNativeArray<PackedMatrix> m_matrices;

        /// <summary>
        /// 最大移动速度
        /// </summary>
        [ReadOnly]
        private float3 m_moveSpeed;

        /// <summary>
        /// 更新间隔时间
        /// </summary>
        [ReadOnly]
        private float m_deltaTime;

        public BoidsRefreshTransformJob(
            in BigCatNativeArray<float3> positions,
            in BigCatNativeArray<quaternion> rotations,
            in BigCatNativeArray<float3> scales,
            in BigCatNativeArray<float3> velocities,
            in BigCatNativeArray<PackedMatrix> matrices,
            float3 moveSpeed,
            float deltaTime)
        {
            m_positions = positions;
            m_rotations = rotations;
            m_scales = scales;
            m_velocities = velocities;
            m_matrices = matrices;

            m_moveSpeed = moveSpeed;
            m_deltaTime = deltaTime;
        }

        //[BurstCompile]
        public void Execute(int index)
        {
            var position = m_positions[index];
            var rotation = m_rotations[index];
            var scale = m_scales[index];
            var velocity = m_velocities[index];
            var velocityLength = math.clamp(math.length(velocity), m_moveSpeed.y, m_moveSpeed.z);
            velocity = math.normalizesafe(velocity);

            // 更新位置
            position += velocity * m_deltaTime * m_moveSpeed.x * velocityLength;
            m_positions[index] = position;

            // 更新旋转
            rotation = quaternion.LookRotationSafe(velocity, math.up());
            m_rotations[index] = rotation;

            // 更新矩阵
            var matrix = float4x4.TRS(position, rotation, scale);
            m_matrices[index] = new PackedMatrix(matrix);
        }
    }
}