using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Physical warehouse rack that stores boxed stock in discrete slots.
/// The first box locks the rack to that product until the player clears the empty rack.
/// </summary>
public class WarehouseShelf : MonoBehaviour
{
    private class SlotState
    {
        public ProductData product;
        public int amount;
    }

    private const int DefaultSlotCount = 4;
    private static readonly Vector3[] DefaultSlotPositions =
    {
        new Vector3(-0.28f, 0.27f, 0f),
        new Vector3(0.28f, 0.27f, 0f),
        new Vector3(-0.28f, 0.78f, 0f),
        new Vector3(0.28f, 0.78f, 0f)
    };

    private readonly SlotState[] slots = new SlotState[DefaultSlotCount];
    private readonly List<GameObject> slotBoxObjects = new List<GameObject>();

    private GameManager gameManager;
    private ProductData lockedProduct;
    private TextMeshPro label;
    private BoxCollider shelfCollider;
    private Transform visualRoot;

    public ProductData LockedProduct => lockedProduct;
    public int SlotCount => slots.Length;
    public bool HasProductLock => lockedProduct != null;
    public bool IsEmpty => GetOccupiedSlotCount() == 0;
    public int FreeSlotCount => slots.Length - GetOccupiedSlotCount();

    public void Initialize(GameManager owner, GameObject visualPrefab)
    {
        gameManager = owner;

        for (int index = 0; index < slots.Length; index++)
        {
            if (slots[index] == null)
            {
                slots[index] = new SlotState();
            }
        }

        EnsureCollider();
        EnsureVisual(visualPrefab);
        EnsureLabel();
        RefreshVisuals();
    }

    public bool CanAcceptProduct(ProductData product)
    {
        if (product == null || FreeSlotCount <= 0)
        {
            return false;
        }

        return lockedProduct == null || lockedProduct == product;
    }

    public bool TryAddBox(ProductData product, int amount)
    {
        if (product == null || amount <= 0 || !CanAcceptProduct(product))
        {
            return false;
        }

        if (lockedProduct == null)
        {
            lockedProduct = product;
        }

        for (int index = 0; index < slots.Length; index++)
        {
            if (slots[index].product != null)
            {
                continue;
            }

            slots[index].product = product;
            slots[index].amount = amount;
            RefreshVisuals();
            return true;
        }

        return false;
    }

    public bool TryRemoveBox(ProductData preferredProduct, out ProductData product, out int amount)
    {
        product = null;
        amount = 0;

        int slotIndex = FindOccupiedSlot(preferredProduct);
        if (slotIndex < 0)
        {
            return false;
        }

        product = slots[slotIndex].product;
        amount = slots[slotIndex].amount;
        slots[slotIndex].product = null;
        slots[slotIndex].amount = 0;
        RefreshVisuals();
        return true;
    }

    public bool TryClearProductLock()
    {
        if (!IsEmpty || lockedProduct == null)
        {
            return false;
        }

        lockedProduct = null;
        RefreshVisuals();
        return true;
    }

    public int GetStoredAmount(ProductData product)
    {
        if (product == null)
        {
            return 0;
        }

        int total = 0;
        foreach (SlotState slot in slots)
        {
            if (slot != null && slot.product == product)
            {
                total += slot.amount;
            }
        }

        return total;
    }

    public string GetPrompt(ProductData carriedProduct, bool isCarryingWarehouseBox, Shelf targetShelf)
    {
        if (carriedProduct != null)
        {
            if (CanAcceptProduct(carriedProduct))
            {
                return isCarryingWarehouseBox ? "[E] Return box to warehouse shelf" : "[E] Place crate on warehouse shelf";
            }

            if (lockedProduct != null && lockedProduct != carriedProduct)
            {
                return "Warehouse shelf locked to " + lockedProduct.productName;
            }

            return "Warehouse shelf is full";
        }

        if (!IsEmpty)
        {
            ProductData preferredProduct = targetShelf != null ? targetShelf.AssignedProduct : null;
            int slotIndex = FindOccupiedSlot(preferredProduct);
            ProductData product = slotIndex >= 0 ? slots[slotIndex].product : lockedProduct;
            return product != null ? "[E] Grab " + product.productName + " box" : "[E] Grab warehouse box";
        }

        if (lockedProduct != null)
        {
            return "[E] Clear " + lockedProduct.productName + " shelf lock";
        }

        return "Empty warehouse shelf";
    }

    public WarehouseShelfSaveEntry BuildSaveEntry()
    {
        WarehouseShelfSaveEntry entry = new WarehouseShelfSaveEntry
        {
            lockedProductName = lockedProduct != null ? lockedProduct.productName : string.Empty,
            position = transform.position
        };

        for (int index = 0; index < slots.Length; index++)
        {
            SlotState slot = slots[index];
            if (slot == null || slot.product == null || slot.amount <= 0)
            {
                continue;
            }

            entry.slots.Add(new WarehouseShelfSlotSaveEntry
            {
                slotIndex = index,
                productName = slot.product.productName,
                amount = slot.amount
            });
        }

        return entry;
    }

    public void LoadState(ProductData productLock, List<WarehouseShelfSlotRuntimeState> loadedSlots)
    {
        lockedProduct = productLock;

        foreach (SlotState slot in slots)
        {
            slot.product = null;
            slot.amount = 0;
        }

        if (loadedSlots != null)
        {
            foreach (WarehouseShelfSlotRuntimeState loadedSlot in loadedSlots)
            {
                if (loadedSlot == null ||
                    loadedSlot.slotIndex < 0 ||
                    loadedSlot.product == null ||
                    loadedSlot.amount <= 0)
                {
                    continue;
                }

                int targetSlotIndex = loadedSlot.slotIndex < slots.Length && slots[loadedSlot.slotIndex].product == null
                    ? loadedSlot.slotIndex
                    : FindFirstEmptySlot();
                if (targetSlotIndex < 0)
                {
                    continue;
                }

                slots[targetSlotIndex].product = loadedSlot.product;
                slots[targetSlotIndex].amount = loadedSlot.amount;

                if (lockedProduct == null)
                {
                    lockedProduct = loadedSlot.product;
                }
            }
        }

        RefreshVisuals();
    }

    private void OnMouseDown()
    {
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.IsCarryingRestockBox || GameManager.Instance.IsCarryingDeliveryCrate())
            {
                return;
            }

            GameManager.Instance.HandleWarehouseShelfClicked(this);
        }
    }

    private int FindOccupiedSlot(ProductData preferredProduct)
    {
        if (preferredProduct != null)
        {
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index].product == preferredProduct)
                {
                    return index;
                }
            }
        }

        for (int index = 0; index < slots.Length; index++)
        {
            if (slots[index].product != null)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindFirstEmptySlot()
    {
        for (int index = 0; index < slots.Length; index++)
        {
            if (slots[index].product == null)
            {
                return index;
            }
        }

        return -1;
    }

    private int GetOccupiedSlotCount()
    {
        int count = 0;
        foreach (SlotState slot in slots)
        {
            if (slot != null && slot.product != null)
            {
                count++;
            }
        }

        return count;
    }

    private void EnsureCollider()
    {
        if (shelfCollider == null)
        {
            shelfCollider = GetComponent<BoxCollider>();
        }

        if (shelfCollider == null)
        {
            shelfCollider = gameObject.AddComponent<BoxCollider>();
        }

        shelfCollider.center = new Vector3(0f, 1f, 0f);
        shelfCollider.size = new Vector3(1.35f, 2.05f, 0.95f);
    }

    private void EnsureVisual(GameObject visualPrefab)
    {
        if (visualRoot != null)
        {
            return;
        }

        Transform existingVisual = transform.Find("WarehouseShelfVisual");
        if (existingVisual != null)
        {
            visualRoot = existingVisual;
            return;
        }

        GameObject visualObject;
        if (visualPrefab != null)
        {
            visualObject = Instantiate(visualPrefab, transform);
        }
        else
        {
            visualObject = CreateFallbackRackVisual();
        }

        visualObject.name = "WarehouseShelfVisual";
        visualObject.transform.localPosition = Vector3.zero;
        visualObject.transform.localRotation = Quaternion.identity;
        visualObject.transform.localScale = Vector3.one;
        visualRoot = visualObject.transform;
    }

    private GameObject CreateFallbackRackVisual()
    {
        GameObject root = new GameObject("WarehouseShelfVisual");
        root.transform.SetParent(transform, false);

        CreateFallbackPart(root.transform, "Post_LF", new Vector3(-0.55f, 1f, 0.35f), new Vector3(0.08f, 2f, 0.08f));
        CreateFallbackPart(root.transform, "Post_RF", new Vector3(0.55f, 1f, 0.35f), new Vector3(0.08f, 2f, 0.08f));
        CreateFallbackPart(root.transform, "Post_LB", new Vector3(-0.55f, 1f, -0.35f), new Vector3(0.08f, 2f, 0.08f));
        CreateFallbackPart(root.transform, "Post_RB", new Vector3(0.55f, 1f, -0.35f), new Vector3(0.08f, 2f, 0.08f));
        CreateFallbackPart(root.transform, "Shelf_0", new Vector3(0f, 0.25f, 0f), new Vector3(1.25f, 0.06f, 0.8f));
        CreateFallbackPart(root.transform, "Shelf_1", new Vector3(0f, 0.85f, 0f), new Vector3(1.25f, 0.06f, 0.8f));
        CreateFallbackPart(root.transform, "Shelf_2", new Vector3(0f, 1.45f, 0f), new Vector3(1.25f, 0.06f, 0.8f));
        return root;
    }

    private void CreateFallbackPart(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = objectName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private void EnsureLabel()
    {
        if (label != null)
        {
            return;
        }

        GameObject labelObject = new GameObject("WarehouseShelfLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 1.62f, -0.48f);

        label = labelObject.AddComponent<TextMeshPro>();
        label.fontSize = 1.45f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        label.outlineWidth = 0.16f;
        labelObject.AddComponent<BillboardToCamera>();
    }

    private void RefreshVisuals()
    {
        foreach (GameObject boxObject in slotBoxObjects)
        {
            if (boxObject != null)
            {
                Destroy(boxObject);
            }
        }

        slotBoxObjects.Clear();

        for (int index = 0; index < slots.Length; index++)
        {
            SlotState slot = slots[index];
            if (slot == null || slot.product == null)
            {
                continue;
            }

            GameObject boxObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boxObject.name = slot.product.productName + "_WarehouseSlotBox";
            boxObject.transform.SetParent(transform, false);
            boxObject.transform.localPosition = DefaultSlotPositions[index];
            boxObject.transform.localScale = new Vector3(0.36f, 0.24f, 0.36f);

            Collider collider = boxObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            MeshRenderer renderer = boxObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.color = ProductVisualUtility.GetProductColor(slot.product, new Color(0.8f, 0.64f, 0.38f, 1f));
            }

            slotBoxObjects.Add(boxObject);
        }

        UpdateLabel();
    }

    private void UpdateLabel()
    {
        EnsureLabel();

        if (label == null)
        {
            return;
        }

        string productName = lockedProduct != null ? lockedProduct.productName : "Empty Rack";
        label.text = productName + "\n" + GetOccupiedSlotCount() + "/" + slots.Length + " boxes";
        label.color = lockedProduct != null ? ProductVisualUtility.GetProductColor(lockedProduct, Color.white) : Color.white;
    }
}

public class WarehouseShelfSlotRuntimeState
{
    public int slotIndex;
    public ProductData product;
    public int amount;
}
