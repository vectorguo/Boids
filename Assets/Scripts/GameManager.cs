using BigCat;
using BigCat.Boids;
using BigCat.NativeCollections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        BigCatMemoryManager.Initialize();
        BigCatGraphics.Initialize();

        gameObject.AddComponent<BoidsManager>();
    }

    private void OnDestroy()
    {
        BigCatGraphics.Destroy();
        BigCatMemoryManager.Destroy();
    }
}
