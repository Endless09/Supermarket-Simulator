using TMPro;
using UnityEngine;

/// <summary>
/// Visible warehouse stock source that represents backroom inventory for one product.
/// </summary>
public class WarehouseStockBox : MonoBehaviour
{
    private ProductData product;
    private int amount;
    private TextMeshPro label;
    private BoxCollider boxCollider;

    public ProductData Product => product;
    public int Amount => amount;

    public void Setup(ProductData stockProduct, int stockAmount)
    {
        product = stockProduct;
        amount = Mathf.Max(0, stockAmount);

        transform.localScale = new Vector3(0.65f, 0.45f, 0.65f);

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.color = ProductVisualUtility.GetProductColor(product, new Color(0.8f, 0.64f, 0.38f, 1f));
        }

        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        if (boxCollider != null)
        {
            boxCollider.size = new Vector3(1.2f, 1.1f, 1.2f);
            boxCollider.center = new Vector3(0f, 0.05f, 0f);
        }

        if (label == null)
        {
            CreateLabel();
        }

        UpdateLabel();
        SetBoxVisible(amount > 0 && product != null);
    }

    private void OnMouseDown()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandleWarehouseStockBoxClicked(this);
        }
    }

    private void CreateLabel()
    {
        GameObject labelObject = new GameObject("WarehouseStockLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);

        label = labelObject.AddComponent<TextMeshPro>();
        label.fontSize = 2.5f;
        label.alignment = TextAlignmentOptions.Center;
        label.outlineColor = new Color(0f, 0f, 0f, 0.8f);
        label.outlineWidth = 0.16f;

        labelObject.AddComponent<BillboardToCamera>();
    }

    private void UpdateLabel()
    {
        if (label == null)
        {
            return;
        }

        string productName = product != null ? product.productName : "Stock";
        label.color = ProductVisualUtility.GetProductColor(product, Color.white);
        label.text = productName + "\nBackroom x" + amount;
    }

    private void SetBoxVisible(bool isVisible)
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = isVisible;
        }

        if (boxCollider != null)
        {
            boxCollider.enabled = isVisible;
        }
    }
}
