using UnityEngine;

namespace BigCat.Boids
{
    public class BoidsGround : MonoBehaviour
    {
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Color lastColor = Gizmos.color;
            Gizmos.color = new Color(0, 0, 1, 0.25f);
            Gizmos.DrawCube(transform.position, new Vector3(25.0f, 0.01f, 25.0f));
            Gizmos.color = lastColor;
        }
#endif
    }
}