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
            Gizmos.color = new Color(1, 0, 0, 0.25f);
            Gizmos.DrawSphere(transform.position, 0.25f);
            Gizmos.color = lastColor;
        }
    }
}