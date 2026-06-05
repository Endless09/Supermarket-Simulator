using TMPro;
using UnityEngine;

/// <summary>
/// Runtime placeholder for the future cardboard baler/trash area.
/// </summary>
public class TrashManager : MonoBehaviour
{
    [Header("Trash/Baler Zone")]
    [SerializeField] private Vector3 trashZoneCenter = new Vector3(-2.6f, 0f, 5.85f);
    [SerializeField] private Vector3 trashZoneSize = new Vector3(1.6f, 0.12f, 1.5f);
    [SerializeField] private Vector3 labelOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private Color trashZoneColor = new Color(0.18f, 0.55f, 0.35f, 0.92f);

    private GameManager gameManager;
    private Transform trashZoneRoot;

    public void Initialize(GameManager owner)
    {
        gameManager = owner;
        EnsureVisual();
    }

    public void EnsureVisual()
    {
        if (trashZoneRoot != null)
        {
            trashZoneRoot.position = trashZoneCenter;
            return;
        }

        GameObject rootObject = new GameObject("TrashBalerZone");
        rootObject.transform.SetParent(transform, false);
        trashZoneRoot = rootObject.transform;

        GameObject padObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        padObject.name = "TrashBalerPad";
        padObject.transform.SetParent(trashZoneRoot, false);
        padObject.transform.localPosition = new Vector3(0f, trashZoneSize.y * 0.5f, 0f);
        padObject.transform.localScale = trashZoneSize;

        MeshRenderer padRenderer = padObject.GetComponent<MeshRenderer>();
        if (padRenderer != null)
        {
            Material padMaterial = gameManager.CreateRuntimeMaterial(trashZoneColor);
            padRenderer.material = padMaterial;
            padRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            padRenderer.receiveShadows = false;
        }

        GameObject labelObject = new GameObject("TrashBalerLabel");
        labelObject.transform.SetParent(trashZoneRoot, false);
        labelObject.transform.localPosition = labelOffset;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = "BALER\nEmpty Boxes";
        label.fontSize = 3f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        label.outlineWidth = 0.18f;
        labelObject.AddComponent<BillboardToCamera>();

        trashZoneRoot.position = trashZoneCenter;
    }

    public bool IsPointInsideZone(Vector3 worldPoint)
    {
        float halfWidth = trashZoneSize.x * 0.5f;
        float halfDepth = trashZoneSize.z * 0.5f;

        return worldPoint.x >= trashZoneCenter.x - halfWidth &&
               worldPoint.x <= trashZoneCenter.x + halfWidth &&
               worldPoint.z >= trashZoneCenter.z - halfDepth &&
               worldPoint.z <= trashZoneCenter.z + halfDepth;
    }
}
