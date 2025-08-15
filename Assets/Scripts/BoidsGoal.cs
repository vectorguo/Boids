using UnityEngine;

namespace BigCat.Boids
{
    public class BoidsGoal : MonoBehaviour
    {
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Color lastColor = Gizmos.color;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.position, 0.2f);
            Gizmos.color = lastColor;
        }
#endif
    }
}