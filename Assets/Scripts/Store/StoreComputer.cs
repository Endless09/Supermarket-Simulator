using TMPro;
using UnityEngine;

/// <summary>
/// Prototype in-world computer used to open store ordering and management screens.
/// </summary>
public class StoreComputer : MonoBehaviour
{
    [SerializeField] private Vector3 computerPosition = new Vector3(4f, 0f, -4f);
    [SerializeField] private Vector3 computerEulerAngles = new Vector3(0f, 180f, 0f);

    private TextMeshPro label;

    private void Awake()
    {
        ApplyPrototypeTransform();

        if (transform.childCount == 0)
        {
            BuildPrototypeComputer();
        }
    }

    private void OnMouseDown()
    {
        OpenComputer();
    }

    public void OpenComputer()
    {
        if (BasicUIManager.Instance != null)
        {
            BasicUIManager.Instance.OpenStoreComputer();
        }
    }

    public string GetPrompt()
    {
        return "[E] Use store computer";
    }

    private void BuildPrototypeComputer()
    {
        ApplyPrototypeTransform();
        gameObject.name = "StoreComputer";

        CreatePart("ComputerDesk", new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.12f, 1.1f), new Color(0.72f, 0.72f, 0.68f, 1f));
        CreatePart("ComputerTower", new Vector3(0.62f, 0.82f, 0.1f), new Vector3(0.42f, 0.7f, 0.5f), new Color(0.08f, 0.09f, 0.1f, 1f));
        CreatePart("ComputerMonitor", new Vector3(-0.22f, 1.08f, 0.08f), new Vector3(0.9f, 0.55f, 0.08f), new Color(0.02f, 0.1f, 0.16f, 1f));
        CreatePart("ComputerKeyboard", new Vector3(-0.22f, 0.56f, -0.28f), new Vector3(0.8f, 0.04f, 0.22f), new Color(0.04f, 0.04f, 0.045f, 1f));

        GameObject labelObject = new GameObject("ComputerLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);

        label = labelObject.AddComponent<TextMeshPro>();
        label.text = "STORE COMPUTER\nOrders & Management";
        label.fontSize = 2.6f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = new Color(0f, 0f, 0f, 0.8f);
        label.outlineWidth = 0.18f;
        labelObject.AddComponent<BillboardToCamera>();
    }

    private void ApplyPrototypeTransform()
    {
        transform.position = computerPosition;
        transform.rotation = Quaternion.Euler(computerEulerAngles);
    }

    private void CreatePart(string partName, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject partObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        partObject.name = partName;
        partObject.transform.SetParent(transform, false);
        partObject.transform.localPosition = localPosition;
        partObject.transform.localScale = localScale;

        MeshRenderer renderer = partObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Material material = GameManager.Instance != null
                ? GameManager.Instance.CreateRuntimeMaterial(color)
                : new Material(Shader.Find("Standard"));
            material.color = color;
            renderer.material = material;
        }
    }
}
