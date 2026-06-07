using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Runtime placeholder visuals for products sitting on a store shelf.
/// Real product meshes can replace this later without changing shelf stock logic.
/// </summary>
public class ShelfProductDisplay : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector3 displayOrigin = new Vector3(0f, 0.72f, -0.28f);
    [SerializeField] private Vector3 itemSize = new Vector3(0.16f, 0.22f, 0.12f);
    [SerializeField] private int itemsPerRow = 5;
    [SerializeField] private int maxVisibleItems = 10;
    [SerializeField] private float horizontalSpacing = 0.19f;
    [SerializeField] private float rowSpacing = 0.16f;
    [SerializeField] private Vector3 priceTagOffset = new Vector3(0f, 0.43f, -0.54f);

    [Header("Materials")]
    [SerializeField] private Color priceTagColor = new Color(1f, 0.92f, 0.35f, 1f);
    [SerializeField] private Color priceTagTextColor = new Color(0.08f, 0.07f, 0.04f, 1f);

    private readonly List<GameObject> productObjects = new List<GameObject>();
    private Transform productRoot;
    private Transform priceTagRoot;
    private TMP_Text priceTagText;
    private MeshRenderer priceTagRenderer;
    private ProductData lastProduct;
    private int lastStock = -1;
    private int lastCapacity = -1;
    private float lastSalePrice = -1f;

    public void Refresh(ProductData product, int stock, int capacity, float salePrice, string productLabel)
    {
        EnsureRoots();

        bool productVisualsChanged = product != lastProduct ||
                                     stock != lastStock ||
                                     capacity != lastCapacity;

        if (productVisualsChanged)
        {
            RebuildProductObjects(product, stock, capacity);
        }

        UpdatePriceTag(product, stock, capacity, salePrice, productLabel);

        lastProduct = product;
        lastStock = stock;
        lastCapacity = capacity;
        lastSalePrice = salePrice;
    }

    private void EnsureRoots()
    {
        if (productRoot == null)
        {
            GameObject rootObject = new GameObject("ShelfProductDisplayRoot");
            rootObject.transform.SetParent(transform, false);
            productRoot = rootObject.transform;
        }

        if (priceTagRoot == null)
        {
            GameObject tagRootObject = new GameObject("ShelfPriceTagRoot");
            tagRootObject.transform.SetParent(transform, false);
            tagRootObject.transform.localPosition = priceTagOffset;
            priceTagRoot = tagRootObject.transform;
        }

        if (priceTagText != null && priceTagRenderer != null)
        {
            return;
        }

        GameObject tagObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tagObject.name = "ShelfPriceTag";
        tagObject.transform.SetParent(priceTagRoot, false);
        tagObject.transform.localPosition = Vector3.zero;
        tagObject.transform.localScale = new Vector3(0.74f, 0.22f, 0.04f);

        Collider tagCollider = tagObject.GetComponent<Collider>();
        if (tagCollider != null)
        {
            Destroy(tagCollider);
        }

        priceTagRenderer = tagObject.GetComponent<MeshRenderer>();
        if (priceTagRenderer != null)
        {
            priceTagRenderer.material.color = priceTagColor;
        }

        GameObject textObject = new GameObject("ShelfPriceTagText");
        textObject.transform.SetParent(priceTagRoot, false);
        textObject.transform.localPosition = new Vector3(0f, 0f, -0.035f);
        textObject.transform.localRotation = Quaternion.identity;
        textObject.transform.localScale = Vector3.one;

        priceTagText = textObject.AddComponent<TextMeshPro>();
        priceTagText.fontSize = 0.22f;
        priceTagText.alignment = TextAlignmentOptions.Center;
        priceTagText.color = priceTagTextColor;
        priceTagText.outlineColor = new Color(1f, 1f, 1f, 0.45f);
        priceTagText.outlineWidth = 0.06f;
    }

    private void RebuildProductObjects(ProductData product, int stock, int capacity)
    {
        foreach (GameObject productObject in productObjects)
        {
            if (productObject != null)
            {
                Destroy(productObject);
            }
        }

        productObjects.Clear();

        if (product == null || stock <= 0 || capacity <= 0)
        {
            return;
        }

        int visibleCount = Mathf.Clamp(
            Mathf.CeilToInt((stock / (float)capacity) * maxVisibleItems),
            1,
            maxVisibleItems);

        Color productColor = ProductVisualUtility.GetProductColor(product, Color.white);
        Color labelColor = Color.Lerp(productColor, Color.white, 0.68f);

        for (int index = 0; index < visibleCount; index++)
        {
            GameObject itemObject = GameObject.CreatePrimitive(GetPrimitiveForProduct(product));
            itemObject.name = product.productName + "_ShelfDisplayItem";
            itemObject.transform.SetParent(productRoot, false);
            itemObject.transform.localPosition = GetItemLocalPosition(index);
            itemObject.transform.localScale = GetItemScale(product);
            itemObject.transform.localRotation = GetItemRotation(product);

            Collider itemCollider = itemObject.GetComponent<Collider>();
            if (itemCollider != null)
            {
                Destroy(itemCollider);
            }

            MeshRenderer renderer = itemObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.color = productColor;
            }

            AddSmallProductLabel(itemObject.transform, product, labelColor);
            productObjects.Add(itemObject);
        }
    }

    private Vector3 GetItemLocalPosition(int index)
    {
        int column = index % itemsPerRow;
        int row = index / itemsPerRow;
        float rowWidth = (Mathf.Min(itemsPerRow, maxVisibleItems) - 1) * horizontalSpacing;
        float x = (column * horizontalSpacing) - (rowWidth * 0.5f);
        float z = displayOrigin.z + (row * rowSpacing);
        return new Vector3(displayOrigin.x + x, displayOrigin.y, z);
    }

    private PrimitiveType GetPrimitiveForProduct(ProductData product)
    {
        string name = product != null && product.productName != null
            ? product.productName.ToLowerInvariant()
            : string.Empty;

        if (name.Contains("milk") || name.Contains("soda"))
        {
            return PrimitiveType.Cylinder;
        }

        if (name.Contains("apple"))
        {
            return PrimitiveType.Sphere;
        }

        return PrimitiveType.Cube;
    }

    private Vector3 GetItemScale(ProductData product)
    {
        string name = product != null && product.productName != null
            ? product.productName.ToLowerInvariant()
            : string.Empty;

        if (name.Contains("milk"))
        {
            return new Vector3(itemSize.x * 0.9f, itemSize.y * 1.35f, itemSize.z * 0.9f);
        }

        if (name.Contains("soda"))
        {
            return new Vector3(itemSize.x * 0.72f, itemSize.y * 1.12f, itemSize.z * 0.72f);
        }

        if (name.Contains("apple"))
        {
            return new Vector3(itemSize.x * 0.78f, itemSize.x * 0.78f, itemSize.x * 0.78f);
        }

        if (name.Contains("bread"))
        {
            return new Vector3(itemSize.x * 1.35f, itemSize.y * 0.7f, itemSize.z * 1.15f);
        }

        if (name.Contains("eggs"))
        {
            return new Vector3(itemSize.x * 1.32f, itemSize.y * 0.45f, itemSize.z * 1.05f);
        }

        return itemSize;
    }

    private Quaternion GetItemRotation(ProductData product)
    {
        string name = product != null && product.productName != null
            ? product.productName.ToLowerInvariant()
            : string.Empty;

        if (name.Contains("milk") || name.Contains("soda"))
        {
            return Quaternion.identity;
        }

        return Quaternion.Euler(0f, 0f, 0f);
    }

    private void AddSmallProductLabel(Transform itemTransform, ProductData product, Color labelColor)
    {
        if (product == null || string.IsNullOrWhiteSpace(product.productName))
        {
            return;
        }

        GameObject labelObject = new GameObject("ShelfDisplayItemLabel");
        labelObject.transform.SetParent(itemTransform, false);
        labelObject.transform.localPosition = new Vector3(0f, -0.52f, -0.53f);
        labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        labelObject.transform.localScale = Vector3.one;

        TMP_Text label = labelObject.AddComponent<TextMeshPro>();
        label.text = product.productName.Substring(0, Mathf.Min(3, product.productName.Length)).ToUpperInvariant();
        label.fontSize = 0.78f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = labelColor;
        label.outlineColor = new Color(0f, 0f, 0f, 0.55f);
        label.outlineWidth = 0.07f;
    }

    private void UpdatePriceTag(ProductData product, int stock, int capacity, float salePrice, string productLabel)
    {
        if (priceTagText == null || priceTagRenderer == null)
        {
            return;
        }

        bool hasProduct = product != null;
        priceTagRenderer.enabled = hasProduct;
        priceTagText.gameObject.SetActive(hasProduct);

        if (!hasProduct)
        {
            return;
        }

        string safeProductLabel = string.IsNullOrWhiteSpace(productLabel) ? product.productName : productLabel;
        priceTagText.text = safeProductLabel + "  $" + salePrice.ToString("0.00") + "\n" + stock + "/" + capacity;

        Color productColor = ProductVisualUtility.GetProductColor(product, priceTagColor);
        priceTagRenderer.material.color = Color.Lerp(priceTagColor, productColor, 0.18f);
    }
}
