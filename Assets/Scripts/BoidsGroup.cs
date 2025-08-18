using BigCat.NativeCollections;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace BigCat.Boids
{
    /// <summary>
    /// BoidsGroup的数据
    /// </summary>
    public class BoidsGroupData
    {
        /// <summary>
        /// 实例位置数组
        /// </summary>
        public BigCatNativeArray<float3> instancePositions;

        /// <summary>
        /// 实例旋转数组
        /// </summary>
        public BigCatNativeArray<quaternion> instanceRotations;

        /// <summary>
        /// 实例缩放数组
        /// </summary>
        public BigCatNativeArray<float3> instanceScales;

        /// <summary>
        /// 实例的速度数组
        /// </summary>
        public BigCatNativeArray<float3> instanceVelocities;

        /// <summary>
        /// 实例矩阵数组
        /// </summary>
        public BigCatNativeArray<PackedMatrix> instanceMatrices;

        /// <summary>
        /// 目标位置数组
        /// </summary>
        public BigCatNativeArray<float3> goalPositions;

        /// <summary>
        /// 障碍物信息
        /// xyz: 障碍物位置 w: 障碍物权重
        /// </summary>
        public BigCatNativeList<float4> obstacles;

        /// <summary>
        /// 大分组
        /// </summary>
        public BigCatNativeList<BoidsMacroGroupInfo> macroGroupInfos;
        public BigCatNativeArray<int> macroGroupIndices;

        /// <summary>
        /// 小分组
        /// </summary>
        public BigCatNativeList<BoidsMicroGroupInfo> microGroupInfos;
        public BigCatNativeArray<int> microGroupIndices;

        /// <summary>
        /// 用于存储真实数量的数组
        /// 0: 大组数量 1: 小组数量
        /// </summary>
        public BigCatNativeArray<int> realGroupCounts;

        /// <summary>
        /// 构造函数
        /// </summary>
        public BoidsGroupData(BoidsGroup boidsGroup)
        {
            var spawnCount = boidsGroup.spawnCount;
            var spawnRange = boidsGroup.spawnRange;
            var spawnMinScale = boidsGroup.spawnMinScale;
            var spawnMaxScale = boidsGroup.spawnMaxScale;
            var groupGoals = boidsGroup.goals;
            var groupObstacles = boidsGroup.obstacles;

            // 初始化goals
            var firstGoalPosition = new float3(boidsGroup.goals[0].transform.position);
            goalPositions = new BigCatNativeArray<float3>(groupGoals.Count);
            for (var i = 0; i < groupGoals.Count; ++i)
            {
                goalPositions[i] = new float3(groupGoals[i].transform.position);
            }

            // 初始化障碍物位置
            obstacles = new BigCatNativeList<float4>(2 + groupObstacles.Count);
            for (var i = 0; i < groupObstacles.Count; ++i)
            {
                var obstacle = groupObstacles[i];
                obstacles.Add(new float4(obstacle.transform.position, obstacle.weightMultiplier));
            }

            // 初始化分组数据
            var macroGroupCount = Mathf.Min(spawnCount / 8, 128);
            macroGroupInfos = new BigCatNativeList<BoidsMacroGroupInfo>(macroGroupCount);
            macroGroupIndices = new BigCatNativeArray<int>(spawnCount);
            var microGroupCount = Mathf.Min(spawnCount / 4, 256);
            microGroupInfos = new BigCatNativeList<BoidsMicroGroupInfo>(microGroupCount);
            microGroupIndices = new BigCatNativeArray<int>(spawnCount);
            realGroupCounts = new BigCatNativeArray<int>(2);

            // 初始化instance数据
            instancePositions = new BigCatNativeArray<float3>(spawnCount);
            instanceRotations = new BigCatNativeArray<quaternion>(spawnCount);
            instanceScales = new BigCatNativeArray<float3>(spawnCount);
            instanceVelocities = new BigCatNativeArray<float3>(spawnCount);
            instanceMatrices = new BigCatNativeArray<PackedMatrix>(spawnCount);
            for (var i = 0; i < spawnCount; ++i)
            {
                // 随机位置
                var position = firstGoalPosition + new float3(
                    UnityEngine.Random.Range(-spawnRange, spawnRange),
                    UnityEngine.Random.Range(-spawnRange, spawnRange),
                    UnityEngine.Random.Range(-spawnRange, spawnRange));
                instancePositions[i] = position;

                // 随机旋转
                var rotation = Quaternion.Euler(
                    UnityEngine.Random.Range(0f, 360f),
                    UnityEngine.Random.Range(0f, 360f),
                    UnityEngine.Random.Range(0f, 360f));
                instanceRotations[i] = rotation;

                // 随机缩放
                var scale = new float3(
                    UnityEngine.Random.Range(spawnMinScale.x, spawnMaxScale.x),
                    UnityEngine.Random.Range(spawnMinScale.y, spawnMaxScale.y),
                    UnityEngine.Random.Range(spawnMinScale.z, spawnMaxScale.z));
                instanceScales[i] = scale;

                // 计算PackedMatrix
                instanceMatrices[i] = new PackedMatrix(Matrix4x4.TRS(position, rotation, scale));
            }
        }

        /// <summary>
        /// 刷新数据
        /// </summary>
        public void Refresh(Dictionary<Transform, float> dynamicObstacles, int dynamicObstabceStartIndex)
        {
            // 重新调整实例数据的大小
            var realMacroGroupCount = realGroupCounts[0];
            var macroGroupCapacity = macroGroupInfos.capacity;
            if (realMacroGroupCount > macroGroupCapacity)
            {
                macroGroupInfos.SetCapacity(Mathf.Max(realMacroGroupCount, macroGroupCapacity + macroGroupCapacity / 2));
            }

            var realMicroGroupCount = realGroupCounts[1];
            var microGroupCapacity = microGroupInfos.capacity;
            if (realMicroGroupCount > microGroupCapacity)
            {
                microGroupInfos.SetCapacity(Mathf.Max(realMicroGroupCount, microGroupCapacity + microGroupCapacity / 2));
            }

            // 更新动态障碍物的位置
            obstacles.Resize(dynamicObstabceStartIndex);
            if (dynamicObstacles != null && dynamicObstacles.Count > 0)
            {
                foreach (var obstacle in dynamicObstacles)
                {
                    var obstacleTransform = obstacle.Key;
                    if (obstacleTransform != null)
                    {
                        // 添加动态障碍物的位置
                        obstacles.Add(new float4(obstacleTransform.position, obstacle.Value));
                    }
                }
            }

            // 清理数据
            macroGroupInfos.Clear();
            microGroupInfos.Clear();
        }

        /// <summary>
        /// Dispose方法
        /// </summary>
        public void Dispose()
        {
            instancePositions.Dispose();
            instanceRotations.Dispose();
            instanceScales.Dispose();
            instanceVelocities.Dispose();
            instanceMatrices.Dispose();
            goalPositions.Dispose();
            obstacles.Dispose();
            macroGroupInfos.Dispose();
            macroGroupIndices.Dispose();
            microGroupInfos.Dispose();
            microGroupIndices.Dispose();
            realGroupCounts.Dispose();
        }
    }

    public class BoidsGroup : MonoBehaviour
    {
        public Material spawnMaterial;
        public Mesh spawnMesh;

        /// <summary>
        /// 生成数量
        /// </summary>
        public int spawnCount = 200;

        /// <summary>
        /// 初始生成范围
        /// </summary>
        public float spawnRange = 2f;

        /// <summary>
        /// 随机缩放-范围
        /// </summary>
        public float3 spawnMinScale = new(0.8f, 0.8f, 0.8f);
        public float3 spawnMaxScale = new(1.2f, 1.2f, 1.2f);

        /// <summary>
        /// 最大移动速度
        /// </summary>
        public float3 moveSpeed = 1.5f;

        /// <summary>
        /// 最大转向速度
        /// </summary>
        public float rotateSpeed = 1.5f;

        /// <summary>
        /// 大组的范围
        /// </summary>
        public float macroGroupRange = 8.0f;

        /// <summary>
        /// 小组的范围
        /// </summary>
        public float microGroupRange = 1.0f;

        /// <summary>
        /// 组内对齐系数
        /// </summary>
        public float alignmentWeight = 2.0f;

        /// <summary>
        /// 组内聚集权重
        /// </summary>
        public float cohesionWeight = 1f;

        /// <summary>
        /// 组内分离权重
        /// </summary>
        public float separationWeight = 3f;

        /// <summary>
        /// 超过分离距离后才会分离
        /// </summary>
        public float separationDistance = 0.5f;

        /// <summary>
        /// 移动目标点
        /// </summary>
        public List<BoidsGoal> goals;

        /// <summary>
        /// 向目标点移动的权重
        /// </summary>
        public float goalWeight = 2f;

        /// <summary>
        /// 障碍物
        /// </summary>
        public List<BoidsObstacle> obstacles;

        /// <summary>
        /// 障碍物规避权重
        /// </summary>
        public float obstacleAvoidWeight = 1.0f;

        /// <summary>
        /// 障碍物规避距离
        /// </summary>
        public float obstacleAvoidDistance = 2.0f;

        /// <summary>
        /// 地面
        /// </summary>
        public BoidsGround ground;

        /// <summary>
        /// 地面规避权重
        /// </summary>
        public float groundAvoidWeight = 2.0f;

        /// <summary>
        /// 地面规避距离
        /// </summary>
        public float groundAvoidDistance = 2.5f;

        /// <summary>
        /// 地面数据
        /// </summary>
        public float3 groundData => new float3(ground.transform.position.y, groundAvoidWeight, groundAvoidDistance);

        /// <summary>
        /// Spawn协程
        /// </summary>
        private Coroutine m_spawnCoroutine;

        /// <summary>
        /// 是否初始化完成
        /// </summary>
        private bool m_isInitialized = false;

        /// <summary>
        /// 数据
        /// </summary>
        private BoidsGroupData m_data;
        public BoidsGroupData data => m_data;

        /// <summary>
        /// 动态障碍物列表
        /// </summary>
        private Dictionary<Transform, float> m_dynamicObstacles;

        /// <summary>
        /// G Buffer
        /// 用于存储实例数据的图形缓冲区
        /// </summary>
        private BigCatGraphicsBuffer m_graphicsBuffer;
        public BigCatGraphicsBuffer graphicsBuffer => m_graphicsBuffer;

        private void OnEnable()
        {
            if (BoidsManager.TryGetInstance(out var _))
            {
                CreateBoidsGroup();
            }
            else
            {
                m_spawnCoroutine = StartCoroutine(DoCreateBoidsGroup());
            }
        }

        private void OnDisable()
        {
            DestroyBoidsGroup();
        }

        private IEnumerator DoCreateBoidsGroup()
        {
            yield return null;
            yield return null;
            CreateBoidsGroup();
        }

        private void CreateBoidsGroup()
        {
            if (spawnMesh == null || spawnMaterial == null)
            {
#if UNITY_EDITOR
                Debug.LogError("SpawnMesh/SpawnMaterial没有设值");
#endif
                return;
            }

            if (spawnCount <= 0)
            {
#if UNITY_EDITOR
                Debug.LogError("SpawnCount必须大于0");
#endif
                return;
            }

            if (goals == null)
            {
#if UNITY_EDITOR
                Debug.LogError("没有设置Goals");
#endif
                return;
            }
            else
            {
                // 删除无效的Goals
                for (var i = goals.Count - 1; i >= 0; --i)
                {
                    if (goals[i] == null)
                    {
                        goals.RemoveAt(i);
                    }
                }
                if (goals.Count == 0)
                {
#if UNITY_EDITOR
                    Debug.LogError("没有设置Goals");
#endif
                    return;
                }
            }

            //删除无效的障碍物
            if (obstacles != null)
            {
                for (var i = obstacles.Count - 1; i >= 0; --i)
                {
                    if (obstacles[i] == null)
                    {
                        obstacles.RemoveAt(i);
                    }
                }
            }

            m_isInitialized = true;

            // 创建数据
            m_data = new BoidsGroupData(this);

            // 创建GBuffer
            m_graphicsBuffer = new BigCatGraphicsBuffer(
                BigCatGraphics.isOpenGLES ? GraphicsBuffer.UsageFlags.None : GraphicsBuffer.UsageFlags.LockBufferForWrite,
                spawnCount * BoidsBatchRenderGroup.sizeOfPerInstance);

            // 注册到BoidsManager
            m_graphicsBuffer.LockBufferForWrite();
            BoidsManager.instance.RegisterBoidsGroup(this);
            m_graphicsBuffer.UnlockBufferAfterWrite();
        }

        private void DestroyBoidsGroup()
        {
            if (m_spawnCoroutine != null)
            {
                StopCoroutine(m_spawnCoroutine);
                m_spawnCoroutine = null;
            }

            if (m_isInitialized)
            {
                m_isInitialized = false;

                if (BoidsManager.TryGetInstance(out var boidsManager))
                {
                    boidsManager.UnregisterBoidsBrg(this);
                }

                m_data.Dispose();
                m_graphicsBuffer.Dispose();
            }
        }

        /// <summary>
        /// 预裁剪前刷新数据
        /// </summary>
        public void RefreshDataBeforePreCulling()
        {
            m_data.Refresh(m_dynamicObstacles, 0);
        }

        /// <summary>
        /// 添加动态障碍物
        /// </summary>
        /// <param name="obstacle">动态障碍物的Transform</param>
        /// <param name="weight">动态障碍物的权重</param>
        public void AddDynamicObstacle(Transform obstacle, float weight)
        {
            if (m_dynamicObstacles == null)
            {
                m_dynamicObstacles = new Dictionary<Transform, float>();
            }
            m_dynamicObstacles.Add(obstacle, weight);
        }
    }
}