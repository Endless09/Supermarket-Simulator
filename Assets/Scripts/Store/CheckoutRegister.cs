using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles the moment when a customer pays for a product.
/// In this prototype, the register simply adds the product's sale price to the player's money.
/// </summary>
public class CheckoutRegister : MonoBehaviour
{
    [Header("Queue")]
    [SerializeField] private int visibleQueueSpots = 4;
    [SerializeField] private Vector3 queueStartOffset = new Vector3(0f, 0f, -1.2f);
    [SerializeField] private Vector3 queueSpacing = new Vector3(0f, 0f, -1.05f);
    [SerializeField] private Color queueSpotColor = new Color(0.95f, 0.82f, 0.28f, 0.65f);

    private readonly List<Customer> queuedCustomers = new List<Customer>();
    private MoneyManager moneyManager;
    private Transform queueVisualRoot;

    private void Awake()
    {
        moneyManager = FindAnyObjectByType<MoneyManager>();
        EnsureQueueVisuals();
    }

    public void JoinQueue(Customer customer)
    {
        if (customer == null || queuedCustomers.Contains(customer))
        {
            return;
        }

        queuedCustomers.Add(customer);
    }

    public void LeaveQueue(Customer customer)
    {
        if (customer == null)
        {
            return;
        }

        queuedCustomers.Remove(customer);
    }

    public bool IsFirstInQueue(Customer customer)
    {
        CleanupQueue();
        return queuedCustomers.Count > 0 && queuedCustomers[0] == customer;
    }

    public Vector3 GetQueuePosition(Customer customer)
    {
        CleanupQueue();

        int queueIndex = queuedCustomers.IndexOf(customer);
        if (queueIndex < 0)
        {
            queueIndex = queuedCustomers.Count;
        }

        Vector3 localPosition = queueStartOffset + (queueSpacing * queueIndex);
        Vector3 desiredPosition = transform.TransformPoint(localPosition);
        return NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)
            ? hit.position
            : desiredPosition;
    }

    public Vector3 GetServiceLookPosition()
    {
        return transform.position;
    }

    public void ProcessCustomer(Customer customer, ProductData product)
    {
        if (customer == null)
        {
            return;
        }

        if (product != null && moneyManager != null)
        {
            float salePrice = GameManager.Instance != null
                ? GameManager.Instance.GetProductSalePrice(product)
                : product.price;
            moneyManager.AddMoney(salePrice);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RecordCustomerPurchase(product);
                GameManager.Instance.NotifyStateChanged();
            }
        }
    }

    private void CleanupQueue()
    {
        queuedCustomers.RemoveAll(customer => customer == null);
    }

    private void EnsureQueueVisuals()
    {
        if (queueVisualRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("CheckoutQueueSpots");
        rootObject.transform.SetParent(transform, false);
        queueVisualRoot = rootObject.transform;

        for (int index = 0; index < visibleQueueSpots; index++)
        {
            CreateQueueSpot(index);
        }
    }

    private void CreateQueueSpot(int index)
    {
        GameObject spotObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spotObject.name = "QueueSpot_" + (index + 1);
        spotObject.transform.SetParent(queueVisualRoot, false);
        spotObject.transform.localPosition = queueStartOffset + (queueSpacing * index) + new Vector3(0f, 0.03f, 0f);
        spotObject.transform.localScale = new Vector3(0.75f, 0.06f, 0.75f);

        Collider spotCollider = spotObject.GetComponent<Collider>();
        if (spotCollider != null)
        {
            Destroy(spotCollider);
        }

        MeshRenderer spotRenderer = spotObject.GetComponent<MeshRenderer>();
        if (spotRenderer != null)
        {
            Material material = GameManager.Instance != null
                ? GameManager.Instance.CreateRuntimeMaterial(queueSpotColor)
                : new Material(Shader.Find("Standard"));
            material.color = queueSpotColor;
            spotRenderer.material = material;
            spotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spotRenderer.receiveShadows = false;
        }

        GameObject labelObject = new GameObject("QueueSpotLabel");
        labelObject.transform.SetParent(spotObject.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.25f, 0f);

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = "Q" + (index + 1);
        label.fontSize = 1.6f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        label.outlineWidth = 0.15f;
        labelObject.AddComponent<BillboardToCamera>();
    }
}
