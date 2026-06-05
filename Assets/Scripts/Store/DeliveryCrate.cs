using TMPro;
using UnityEngine;

/// <summary>
/// Physical delivery crate spawned when a shipment arrives.
/// Click it to unload the contents into the store backroom.
/// </summary>
public class DeliveryCrate : MonoBehaviour
{
    private ProductData product;
    private int amount;
    private TextMeshPro label;
    private Collider crateCollider;
    private bool isBeingCarried;

    public ProductData Product => product;
    public int Amount => amount;

    public void Setup(ProductData crateProduct, int crateAmount)
    {
        product = crateProduct;
        amount = Mathf.Max(1, crateAmount);

        transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.color = ProductVisualUtility.GetProductColor(product, new Color(0.72f, 0.52f, 0.29f, 1f));
        }

        crateCollider = GetComponent<Collider>();

        if (label == null)
        {
            CreateLabel();
        }

        UpdateLabel();
    }

    private void OnMouseDown()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandleDeliveryCrateClicked(this);
        }
    }

    public void SetCarriedState(bool carried)
    {
        isBeingCarried = carried;

        if (crateCollider != null)
        {
            crateCollider.enabled = !carried;
        }

        UpdateLabel();
    }

    public void SetAmount(int newAmount)
    {
        amount = Mathf.Max(1, newAmount);
        UpdateLabel();
    }

    private void CreateLabel()
    {
        GameObject labelObject = new GameObject("CrateLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 1.05f, 0f);

        label = labelObject.AddComponent<TextMeshPro>();
        label.fontSize = 4f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = new Color(0f, 0f, 0f, 0.8f);
        label.outlineWidth = 0.18f;

        labelObject.AddComponent<BillboardToCamera>();
    }

    private void UpdateLabel()
    {
        if (label == null)
        {
            return;
        }

        string productName = product != null ? product.productName : "Delivery";
        label.fontSize = isBeingCarried ? 3.8f : 4f;
        label.color = ProductVisualUtility.GetProductColor(product, Color.white);
        label.text = isBeingCarried
            ? productName + " x" + amount
            : productName + "\nPick Up x" + amount;
    }
}
