using TMPro;
using UnityEngine;

/// <summary>
/// A shelf stores one type of product and a limited quantity of stock.
/// Customers can take items from it, and the player can restock it from the backroom inventory.
/// </summary>
public class Shelf : MonoBehaviour
{
    [Header("Shelf Setup")]
    [SerializeField] private ProductData assignedProduct;
    [SerializeField] private int maxCapacity = 10;
    [SerializeField] private int startingStock = 0;

    [Header("Optional Visuals")]
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private MeshRenderer shelfRenderer;
    [SerializeField] private Color emptyColor = new Color(0.86f, 0.86f, 0.86f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.92f, 0.2f, 1f);
    [SerializeField] private Vector3 labelOffset = new Vector3(0f, 0.85f, 0f);

    public ProductData AssignedProduct => assignedProduct;
    public int CurrentStock { get; private set; }
    public int MaxCapacity => maxCapacity;
    public bool HasStock => CurrentStock > 0;
    public int SpaceRemaining => Mathf.Max(0, maxCapacity - CurrentStock);
    private bool hasRuntimeStateApplied;
    private bool isSelected;

    private void Start()
    {
        if (!hasRuntimeStateApplied)
        {
            CurrentStock = Mathf.Clamp(startingStock, 0, maxCapacity);
        }

        EnsureRuntimeLabel();
        ApplySelectionVisual(isSelected);
        UpdateStockText();
    }

    public bool HasProduct(ProductData product)
    {
        return assignedProduct == product;
    }

    public bool TryTakeOneItem()
    {
        if (!HasStock)
        {
            return false;
        }

        CurrentStock--;
        UpdateStockText();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyStateChanged();
        }

        return true;
    }

    public bool RestockFromBackroom()
    {
        if (assignedProduct == null || GameManager.Instance == null)
        {
            return false;
        }

        int spaceRemaining = maxCapacity - CurrentStock;
        if (spaceRemaining <= 0)
        {
            return false;
        }

        int backroomAmount = GameManager.Instance.GetBackroomStock(assignedProduct);
        int amountToMove = Mathf.Min(spaceRemaining, backroomAmount);

        if (amountToMove <= 0)
        {
            return false;
        }

        if (!GameManager.Instance.TakeFromBackroom(assignedProduct, amountToMove))
        {
            return false;
        }

        return AddStock(assignedProduct, amountToMove) > 0;
    }

    public bool CanAcceptRestock(ProductData product)
    {
        return CanAcceptProduct(product);
    }

    public bool CanAcceptProduct(ProductData product)
    {
        if (product == null || SpaceRemaining <= 0)
        {
            return false;
        }

        return assignedProduct == null || assignedProduct == product || CurrentStock == 0;
    }

    public int AddStock(int amount)
    {
        return AddStock(assignedProduct, amount);
    }

    public int AddStock(ProductData product, int amount)
    {
        if (amount <= 0 || !CanAcceptProduct(product))
        {
            return 0;
        }

        if (assignedProduct != product)
        {
            assignedProduct = product;
        }

        int addedAmount = Mathf.Min(amount, SpaceRemaining);
        CurrentStock += addedAmount;
        UpdateStockText();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyStateChanged();
        }

        return addedAmount;
    }

    public void SetProduct(ProductData product)
    {
        assignedProduct = product;
        UpdateStockText();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyStateChanged();
        }
    }

    public void SetSelected(bool isSelected)
    {
        this.isSelected = isSelected;
        ApplySelectionVisual(this.isSelected);
    }

    private void OnMouseDown()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandleShelfClicked(this);
        }
    }

    private void UpdateStockText()
    {
        EnsureRuntimeLabel();

        if (stockText == null)
        {
            return;
        }

        string productLabel = assignedProduct != null ? assignedProduct.productName : "Empty";
        stockText.text = productLabel + "\n" + CurrentStock + "/" + maxCapacity;
        stockText.color = assignedProduct != null
            ? ProductVisualUtility.GetProductColor(assignedProduct, Color.white)
            : Color.white;

        ApplySelectionVisual(isSelected);
    }

    public void LoadState(ProductData product, int stock)
    {
        hasRuntimeStateApplied = true;
        assignedProduct = product;
        CurrentStock = Mathf.Clamp(stock, 0, maxCapacity);
        UpdateStockText();
    }

    public void InitializePlacedShelf(ProductData product)
    {
        hasRuntimeStateApplied = true;
        assignedProduct = product;
        CurrentStock = 0;
        UpdateStockText();
    }

    private void ApplySelectionVisual(bool isSelected)
    {
        if (shelfRenderer == null)
        {
            shelfRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (shelfRenderer == null || shelfRenderer.material == null)
        {
            return;
        }

        Color baseColor = assignedProduct != null
            ? ProductVisualUtility.GetProductColor(assignedProduct, emptyColor)
            : emptyColor;

        shelfRenderer.material.color = isSelected ? Color.Lerp(baseColor, selectedColor, 0.55f) : baseColor;
    }

    private void EnsureRuntimeLabel()
    {
        if (stockText != null)
        {
            return;
        }

        GameObject labelObject = new GameObject("ShelfStockLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = labelOffset;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.fontSize = 3.2f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        label.outlineWidth = 0.18f;
        labelObject.AddComponent<BillboardToCamera>();

        stockText = label;
    }
}
