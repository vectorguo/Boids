using BigCat;
using BigCat.Boids;
using BigCat.NativeCollections;
using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject player;

    void Start()
    {
        BigCatMemoryManager.Initialize();
        BigCatGraphics.Initialize();

        gameObject.AddComponent<BoidsManager>();

        StartCoroutine(Do());
    }

    private void OnDestroy()
    {
        BigCatGraphics.Destroy();
        BigCatMemoryManager.Destroy();
    }

    private IEnumerator Do()
    {
        while (true)
        {
            yield return null;

            if (BoidsManager.TryGetInstance(out var boidsManager))
            {
                if (boidsManager.boidsGroups.Count > 0)
                {
                    foreach (var group in boidsManager.boidsGroups)
                    {
                        group.AddDynamicObstacle(player.transform, 15f);
                    }
                    yield break;
                }
            }
        }
    }
}
