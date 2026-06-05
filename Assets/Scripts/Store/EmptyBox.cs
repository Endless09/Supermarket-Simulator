using TMPro;
using UnityEngine;

/// <summary>
/// Lightweight physical cleanup item created after a stock box is emptied onto shelves.
/// </summary>
public class EmptyBox : MonoBehaviour
{
    private ProductData sourceProduct;
    private TextMeshPro label;
    private Collider boxCollider;
    private bool isBeingCarried;

    public ProductData SourceProduct => sourceProduct;

    public void Setup(ProductData emptiedProduct)
    {
        sourceProduct = emptiedProduct;
        transform.localScale = new Vector3(0.7f, 0.16f, 0.55f);

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.55f, 0.39f, 0.22f, 1f);
        }

        boxCollider = GetComponent<Collider>();

        if (label == null)
        {
            CreateLabel();
        }

        UpdateLabel();
    }

    public void SetCarriedState(bool carried)
    {
        isBeingCarried = carried;

        if (boxCollider != null)
        {
            boxCollider.enabled = !carried;
        }

        UpdateLabel();
    }

    private void CreateLabel()
    {
        GameObject labelObject = new GameObject("EmptyBoxLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.55f, 0f);

        label = labelObject.AddComponent<TextMeshPro>();
        label.fontSize = 3.2f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.95f, 0.86f, 0.65f, 1f);
        label.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        label.outlineWidth = 0.18f;

        labelObject.AddComponent<BillboardToCamera>();
    }

    private void UpdateLabel()
    {
        if (label == null)
        {
            return;
        }

        string productName = sourceProduct != null ? sourceProduct.productName : "Stock";
        label.text = isBeingCarried
            ? "EMPTY BOX\n" + productName
            : "Empty Box";
    }
}
