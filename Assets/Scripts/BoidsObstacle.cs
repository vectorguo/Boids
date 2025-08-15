using UnityEngine;

namespace BigCat.Boids
{
    public class BoidsObstacle : MonoBehaviour
    {
        [Header("Obstacle Settings")]
        [Tooltip("该障碍物的权重乘数，会与全局 obstacleAvoidWeight 相乘")]
        public float weightMultiplier = 1.0f;

        void OnDrawGizmos()
        {
            Color lastColor = Gizmos.color;
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.5f, 0.5f, 0.5f));
            Gizmos.color = lastColor;
        }
    }
}