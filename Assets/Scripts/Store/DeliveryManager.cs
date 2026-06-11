using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Owns receiving visuals, queued deliveries, and dock-crate interactions.
/// </summary>
public class DeliveryManager : MonoBehaviour
{
    [Serializable]
    public class QueuedDeliveryState
    {
        public string productName;
        public int amount;
        public float remainingTime;
    }

    [Serializable]
    public class DockCrateState
    {
        public string productName;
        public int amount;
        public Vector3 position;
    }

    private class PendingDelivery
    {
        public ProductData product;
        public int amount;
        public float remainingTime;
    }

    [Header("Deliveries")]
    [SerializeField] private float deliveryDurationSeconds = 30f;
    [SerializeField] private Vector3 deliveryDropOffPosition = new Vector3(-10f, 0.4f, 8f);
    [SerializeField] private Vector3 deliveryCrateSpacing = new Vector3(1f, 0f, 0f);
    [SerializeField] private Vector3 receivingZoneSize = new Vector3(3.6f, 0.08f, 2.4f);
    [SerializeField] private Vector3 receivingZoneLabelOffset = new Vector3(0f, 1.4f, -1.1f);
    [SerializeField] private Color receivingZoneColor = new Color(0.93f, 0.75f, 0.26f, 0.95f);
    [SerializeField] private Color receivingZoneStripeColor = new Color(0.17f, 0.17f, 0.17f, 1f);

    private readonly List<PendingDelivery> pendingDeliveries = new List<PendingDelivery>();
    private readonly List<DeliveryCrate> activeDeliveryCrates = new List<DeliveryCrate>();

    private GameManager gameManager;
    private Transform receivingZoneRoot;
    private DeliveryCrate carriedDeliveryCrate;

    public bool IsCarryingCrate => carriedDeliveryCrate != null;

    public void Initialize(GameManager owner)
    {
        gameManager = owner;
        EnsureReceivingZoneVisual();
    }

    public void TickPendingDeliveries(bool isApplyingSaveData)
    {
        if (isApplyingSaveData || pendingDeliveries.Count == 0)
        {
            return;
        }

        bool deliveryArrived = false;

        for (int index = pendingDeliveries.Count - 1; index >= 0; index--)
        {
            PendingDelivery delivery = pendingDeliveries[index];
            delivery.remainingTime -= Time.deltaTime;

            if (delivery.remainingTime > 0f)
            {
                continue;
            }

            SpawnDeliveryCrate(delivery.product, delivery.amount);
            Debug.Log(delivery.product.productName + " delivery arrived at the loading area: +" + delivery.amount);
            pendingDeliveries.RemoveAt(index);
            deliveryArrived = true;
        }

        if (deliveryArrived)
        {
            gameManager.NotifyStateChanged();
        }
    }

    public void QueueDelivery(ProductData product, int amount)
    {
        if (product == null || amount <= 0)
        {
            return;
        }

        gameManager.RegisterKnownProduct(product);
        pendingDeliveries.Add(new PendingDelivery
        {
            product = product,
            amount = amount,
            remainingTime = Mathf.Max(1f, deliveryDurationSeconds)
        });

        Debug.Log(product.productName + " delivery ordered. Arrival in " + Mathf.CeilToInt(Mathf.Max(1f, deliveryDurationSeconds)) + " seconds.");
    }

    public int GetIncomingDeliveryAmount(ProductData product)
    {
        if (product == null)
        {
            return 0;
        }

        int total = 0;
        foreach (PendingDelivery delivery in pendingDeliveries)
        {
            if (delivery.product == product)
            {
                total += delivery.amount;
            }
        }

        return total;
    }

    public float GetSoonestDeliveryTime(ProductData product)
    {
        if (product == null)
        {
            return -1f;
        }

        float soonest = float.MaxValue;
        bool found = false;

        foreach (PendingDelivery delivery in pendingDeliveries)
        {
            if (delivery.product != product)
            {
                continue;
            }

            soonest = Mathf.Min(soonest, delivery.remainingTime);
            found = true;
        }

        return found ? soonest : -1f;
    }

    public int GetDockDeliveryAmount(ProductData product)
    {
        if (product == null)
        {
            return 0;
        }

        int total = 0;
        foreach (DeliveryCrate crate in activeDeliveryCrates)
        {
            if (crate != null && crate.Product == product)
            {
                total += crate.Amount;
            }
        }

        return total;
    }

    public bool HasAnyDockCrates()
    {
        activeDeliveryCrates.RemoveAll(crate => crate == null);
        return activeDeliveryCrates.Count > 0;
    }

    public string GetTaskStatus()
    {
        if (carriedDeliveryCrate != null && carriedDeliveryCrate.Product != null)
        {
            return "Carrying: " + carriedDeliveryCrate.Product.productName + " x" + carriedDeliveryCrate.Amount +
                   "\nPlace it on a warehouse shelf";
        }

        if (HasAnyDockCrates())
        {
            return "Dock crates ready\nPick one up, then place it on a warehouse shelf";
        }

        return string.Empty;
    }

    public string GetCarryInteractionPrompt(bool isLookingAtBackroomDropZone)
    {
        if (carriedDeliveryCrate == null)
        {
            return string.Empty;
        }

        return "Look at a compatible warehouse shelf and press [E]";
    }

    public ProductData CarriedCrateProduct => carriedDeliveryCrate != null ? carriedDeliveryCrate.Product : null;

    public string GetPickupPrompt(DeliveryCrate crate)
    {
        return crate != null && crate.Product != null
            ? "[E] Pick up " + crate.Product.productName + " crate"
            : string.Empty;
    }

    public void HandleCrateClicked(DeliveryCrate crate)
    {
        if (crate == null || gameManager == null || gameManager.IsPlacingShelf || gameManager.IsCarryingRestockBox || gameManager.IsCarryingEmptyBox)
        {
            return;
        }

        if (carriedDeliveryCrate != null && carriedDeliveryCrate != crate)
        {
            return;
        }

        carriedDeliveryCrate = crate;
        carriedDeliveryCrate.SetCarriedState(true);
        ShowHudNotice("Picked up " + GetProductName(crate.Product) + " crate x" + crate.Amount + ". Place it on a warehouse rack.", HudNoticeType.Info);
    }

    public void UpdateCarriedCratePosition()
    {
        if (carriedDeliveryCrate == null || gameManager == null)
        {
            return;
        }

        carriedDeliveryCrate.transform.position = gameManager.GetHeldObjectPosition();
    }

    public bool TryUnloadCarriedCrateAtDropZone(bool isInsideDropZone)
    {
        return false;
    }

    public bool TryApplyCarriedCrateToShelf(Shelf shelf)
    {
        return false;
    }

    public bool TryPlaceCarriedCrateOnWarehouseShelf(WarehouseShelf shelf)
    {
        if (carriedDeliveryCrate == null || shelf == null || gameManager == null)
        {
            return false;
        }

        ProductData product = carriedDeliveryCrate.Product;
        int amount = carriedDeliveryCrate.Amount;
        if (!gameManager.TryPlaceBoxOnWarehouseShelf(shelf, product, amount))
        {
            ShowHudNotice("No rack space for " + GetProductName(product) + ".", HudNoticeType.Warning);
            return false;
        }

        activeDeliveryCrates.Remove(carriedDeliveryCrate);
        Destroy(carriedDeliveryCrate.gameObject);
        carriedDeliveryCrate = null;
        ArrangeDeliveryCrates();
        ShowHudNotice("Stored " + GetProductName(product) + " x" + amount + " on warehouse rack.", HudNoticeType.Success);
        gameManager.NotifyStateChanged();
        return true;
    }

    public void ReturnCarriedCrateToDock()
    {
        if (carriedDeliveryCrate == null)
        {
            return;
        }

        carriedDeliveryCrate.SetCarriedState(false);
        carriedDeliveryCrate = null;
        ArrangeDeliveryCrates();
    }

    public void ClearState()
    {
        pendingDeliveries.Clear();
        carriedDeliveryCrate = null;

        foreach (DeliveryCrate crate in activeDeliveryCrates)
        {
            if (crate != null)
            {
                Destroy(crate.gameObject);
            }
        }

        activeDeliveryCrates.Clear();
    }

    public List<QueuedDeliveryState> BuildQueuedDeliveryStates()
    {
        List<QueuedDeliveryState> states = new List<QueuedDeliveryState>();

        foreach (PendingDelivery delivery in pendingDeliveries)
        {
            if (delivery.product == null)
            {
                continue;
            }

            states.Add(new QueuedDeliveryState
            {
                productName = delivery.product.productName,
                amount = delivery.amount,
                remainingTime = delivery.remainingTime
            });
        }

        return states;
    }

    public List<DockCrateState> BuildDockCrateStates()
    {
        List<DockCrateState> states = new List<DockCrateState>();

        foreach (DeliveryCrate crate in activeDeliveryCrates)
        {
            if (crate == null || crate.Product == null)
            {
                continue;
            }

            states.Add(new DockCrateState
            {
                productName = crate.Product.productName,
                amount = crate.Amount,
                position = crate.transform.position
            });
        }

        return states;
    }

    public void LoadQueuedDelivery(ProductData product, int amount, float remainingTime)
    {
        if (product == null || amount <= 0)
        {
            return;
        }

        pendingDeliveries.Add(new PendingDelivery
        {
            product = product,
            amount = amount,
            remainingTime = Mathf.Max(0.1f, remainingTime)
        });
    }

    public void LoadDockCrate(ProductData product, int amount, Vector3 position)
    {
        SpawnDeliveryCrate(product, amount, position);
    }

    private void SpawnDeliveryCrate(ProductData product, int amount)
    {
        SpawnDeliveryCrate(product, amount, GetNextDeliveryCratePosition());
    }

    private void SpawnDeliveryCrate(ProductData product, int amount, Vector3 position)
    {
        if (product == null || amount <= 0)
        {
            return;
        }

        EnsureReceivingZoneVisual();

        GameObject crateObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crateObject.name = product.productName + "_DeliveryCrate";
        crateObject.transform.position = position;

        DeliveryCrate crate = crateObject.AddComponent<DeliveryCrate>();
        crate.Setup(product, amount);
        activeDeliveryCrates.Add(crate);
        ArrangeDeliveryCrates();
    }

    private void UnloadDeliveryCrate(DeliveryCrate crate)
    {
        if (crate == null || gameManager == null)
        {
            return;
        }

        if (carriedDeliveryCrate == crate)
        {
            carriedDeliveryCrate = null;
        }

        gameManager.AddToBackroom(crate.Product, crate.Amount);
        activeDeliveryCrates.Remove(crate);
        Destroy(crate.gameObject);
        ArrangeDeliveryCrates();
        gameManager.NotifyStateChanged();
    }

    private void ArrangeDeliveryCrates()
    {
        activeDeliveryCrates.RemoveAll(crate => crate == null);

        for (int index = 0; index < activeDeliveryCrates.Count; index++)
        {
            DeliveryCrate crate = activeDeliveryCrates[index];
            int row = index / 3;
            int column = index % 3;
            crate.transform.position = deliveryDropOffPosition +
                                       new Vector3(deliveryCrateSpacing.x * column,
                                                   deliveryCrateSpacing.y * row,
                                                   deliveryCrateSpacing.z * row);
        }
    }

    private Vector3 GetNextDeliveryCratePosition()
    {
        int index = activeDeliveryCrates.Count;
        int row = index / 3;
        int column = index % 3;
        return deliveryDropOffPosition +
               new Vector3(deliveryCrateSpacing.x * column,
                           deliveryCrateSpacing.y * row,
                           deliveryCrateSpacing.z * row);
    }

    private void EnsureReceivingZoneVisual()
    {
        if (receivingZoneRoot != null)
        {
            UpdateReceivingZoneTransform();
            return;
        }

        GameObject rootObject = new GameObject("ReceivingZone");
        receivingZoneRoot = rootObject.transform;

        CreateReceivingZonePad();
        CreateReceivingZoneStripes();
        CreateReceivingZoneLabel();
        UpdateReceivingZoneTransform();
    }

    private void CreateReceivingZonePad()
    {
        GameObject padObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        padObject.name = "ReceivingPad";
        padObject.transform.SetParent(receivingZoneRoot, false);
        padObject.transform.localPosition = new Vector3(0f, -deliveryDropOffPosition.y + (receivingZoneSize.y * 0.5f), 0f);
        padObject.transform.localScale = receivingZoneSize;

        Collider padCollider = padObject.GetComponent<Collider>();
        if (padCollider != null)
        {
            Destroy(padCollider);
        }

        MeshRenderer padRenderer = padObject.GetComponent<MeshRenderer>();
        if (padRenderer != null)
        {
            Material padMaterial = gameManager.CreateRuntimeMaterial(receivingZoneColor);
            padRenderer.material = padMaterial;
            padRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            padRenderer.receiveShadows = false;
        }
    }

    private void CreateReceivingZoneStripes()
    {
        float stripeWidth = 0.18f;
        float stripeHeight = 0.01f;
        float stripeLength = receivingZoneSize.z * 0.82f;
        float stripeY = -deliveryDropOffPosition.y + receivingZoneSize.y + stripeHeight;

        for (int index = -1; index <= 1; index++)
        {
            GameObject stripeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripeObject.name = "ReceivingStripe_" + (index + 2);
            stripeObject.transform.SetParent(receivingZoneRoot, false);
            stripeObject.transform.localPosition = new Vector3(index * 0.85f, stripeY, 0f);
            stripeObject.transform.localScale = new Vector3(stripeWidth, stripeHeight, stripeLength);
            stripeObject.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);

            Collider stripeCollider = stripeObject.GetComponent<Collider>();
            if (stripeCollider != null)
            {
                Destroy(stripeCollider);
            }

            MeshRenderer stripeRenderer = stripeObject.GetComponent<MeshRenderer>();
            if (stripeRenderer != null)
            {
                Material stripeMaterial = gameManager.CreateRuntimeMaterial(receivingZoneStripeColor);
                stripeRenderer.material = stripeMaterial;
                stripeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                stripeRenderer.receiveShadows = false;
            }
        }
    }

    private void CreateReceivingZoneLabel()
    {
        GameObject labelObject = new GameObject("ReceivingLabel");
        labelObject.transform.SetParent(receivingZoneRoot, false);
        labelObject.transform.localPosition = receivingZoneLabelOffset;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = "RECEIVING\nDeliveries Arrive Here";
        label.fontSize = 4f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = new Color(0f, 0f, 0f, 0.7f);
        label.outlineWidth = 0.15f;

        labelObject.AddComponent<BillboardToCamera>();
    }

    private void UpdateReceivingZoneTransform()
    {
        if (receivingZoneRoot == null)
        {
            return;
        }

        receivingZoneRoot.position = new Vector3(deliveryDropOffPosition.x, 0f, deliveryDropOffPosition.z);
    }

    private static string GetProductName(ProductData product)
    {
        return product != null && !string.IsNullOrWhiteSpace(product.productName)
            ? product.productName
            : "Stock";
    }

    private void ShowHudNotice(string message, HudNoticeType type, float durationSeconds = 2f)
    {
        if (BasicUIManager.Instance != null)
        {
            BasicUIManager.Instance.ShowHudNotice(message, type, durationSeconds);
        }
    }
}
