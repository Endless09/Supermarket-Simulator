using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles runtime-only scene helpers that support the prototype floor, materials, and navigation rebuilds.
/// </summary>
public class RuntimeSceneManager : MonoBehaviour
{
    [Header("Prototype Layout")]
    [SerializeField] private bool sizePrototypeFloorToGrid = true;
    [SerializeField] private Vector3 prototypeFloorCenter = new Vector3(-1f, 0f, 3f);
    [SerializeField] private Vector2 prototypeFloorSize = new Vector2(30f, 26f);

    private Shader cachedSurfaceShader;
    private NavMeshSurface storeNavMeshSurface;
    private bool navMeshRebuildQueued;

    public void Initialize()
    {
        EnsurePrototypeFloorCoversGrid();
        EnsureStoreNavigation();
    }

    public Material CreateRuntimeMaterial(Color color)
    {
        Shader shader = GetRuntimeSurfaceShader();
        Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
        material.color = color;
        return material;
    }

    public void QueueStoreNavigationRebuild()
    {
        EnsureStoreNavigation();
        if (storeNavMeshSurface == null || navMeshRebuildQueued)
        {
            return;
        }

        navMeshRebuildQueued = true;
        StartCoroutine(RebuildStoreNavigationAtEndOfFrame());
    }

    private Shader GetRuntimeSurfaceShader()
    {
        if (cachedSurfaceShader != null)
        {
            return cachedSurfaceShader;
        }

        string[] shaderNames =
        {
            "Universal Render Pipeline/Lit",
            "Standard"
        };

        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                cachedSurfaceShader = shader;
                return cachedSurfaceShader;
            }
        }

        return null;
    }

    private void EnsurePrototypeFloorCoversGrid()
    {
        if (!sizePrototypeFloorToGrid)
        {
            return;
        }

        GameObject floorObject = GameObject.Find("Floor");
        if (floorObject == null)
        {
            floorObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorObject.name = "Floor";
        }

        floorObject.transform.position = prototypeFloorCenter;

        MeshFilter meshFilter = floorObject.GetComponent<MeshFilter>();
        Vector3 meshSize = meshFilter != null && meshFilter.sharedMesh != null
            ? meshFilter.sharedMesh.bounds.size
            : new Vector3(10f, 0f, 10f);

        float baseWidth = Mathf.Abs(meshSize.x) > 0.01f ? Mathf.Abs(meshSize.x) : 10f;
        float baseDepth = Mathf.Abs(meshSize.z) > 0.01f ? Mathf.Abs(meshSize.z) : 10f;
        floorObject.transform.localScale = new Vector3(
            Mathf.Max(1f, prototypeFloorSize.x / baseWidth),
            1f,
            Mathf.Max(1f, prototypeFloorSize.y / baseDepth));
    }

    private void EnsureStoreNavigation()
    {
        if (storeNavMeshSurface != null)
        {
            return;
        }

        GameObject floorObject = GameObject.Find("Floor");
        if (floorObject == null)
        {
            return;
        }

        storeNavMeshSurface = floorObject.GetComponent<NavMeshSurface>();
        if (storeNavMeshSurface == null)
        {
            storeNavMeshSurface = floorObject.AddComponent<NavMeshSurface>();
        }

        storeNavMeshSurface.collectObjects = CollectObjects.All;
        storeNavMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        storeNavMeshSurface.layerMask = ~0;
        storeNavMeshSurface.agentTypeID = 0;
    }

    private IEnumerator RebuildStoreNavigationAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        navMeshRebuildQueued = false;

        if (storeNavMeshSurface != null)
        {
            storeNavMeshSurface.BuildNavMesh();
        }
    }
}
