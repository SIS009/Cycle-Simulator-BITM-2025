using UnityEngine;

[ExecuteAlways]
public class TerrainDetailDistance : MonoBehaviour
{
    [Min(0)]
    public float detailDistance = 500f;

    private void OnValidate()
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
            terrain.detailObjectDistance = detailDistance;
    }
}