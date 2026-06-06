using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Prototype customer with a simple shopping list.
/// The customer navigates to shelves for desired products,
/// then pays at checkout and leaves the store.
/// </summary>
public class Customer : MonoBehaviour
{
    private enum CustomerState
    {
        Shopping,
        WalkingToCheckout,
        CheckingOut,
        LeavingStore
    }

    [SerializeField] private float moveSpeed = 2.8f;
    [SerializeField] private float stoppingDistance = 0.8f;
    [SerializeField] private float repathInterval = 0.4f;
    [SerializeField] private float shelfBrowseTime = 1.25f;
    [SerializeField] private float checkoutDuration = 2f;
    [SerializeField] private float shelfApproachDistance = 0.9f;
    [SerializeField] private float expensivePriceMultiplier = 1.5f;
    [SerializeField] private float overpricedRefusalMultiplier = 2f;

    private CheckoutRegister checkoutRegister;
    private Transform exitPoint;
    private readonly List<ProductData> shoppingList = new List<ProductData>();
    private readonly List<ProductData> carriedProducts = new List<ProductData>();
    private readonly List<ProductData> missingProducts = new List<ProductData>();
    private readonly List<ProductData> tooExpensiveProducts = new List<ProductData>();
    private Shelf targetShelf;
    private CustomerState currentState;
    private NavMeshAgent navMeshAgent;
    private int currentShoppingIndex;
    private float repathTimer;
    private float shelfBrowseTimer;
    private float checkoutTimer;
    private bool isBrowsingShelf;
    private bool hasReportedMissingProducts;
    private bool hasReportedTripFeedback;
    private bool hasJoinedCheckoutQueue;
    private TextMeshPro statusLabel;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
        }

        navMeshAgent.speed = moveSpeed;
        navMeshAgent.acceleration = moveSpeed * 4f;
        navMeshAgent.angularSpeed = 540f;
        navMeshAgent.stoppingDistance = stoppingDistance;
        navMeshAgent.radius = 0.35f;
        navMeshAgent.height = 1.8f;
        navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        navMeshAgent.avoidancePriority = Random.Range(35, 75);

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider customerCollider in colliders)
        {
            customerCollider.isTrigger = true;
        }

        EnsureStatusLabel();
    }

    public void Setup(List<ProductData> desiredProducts, CheckoutRegister checkout, Transform exitTarget)
    {
        shoppingList.Clear();
        carriedProducts.Clear();
        missingProducts.Clear();
        tooExpensiveProducts.Clear();
        currentShoppingIndex = 0;
        shelfBrowseTimer = 0f;
        isBrowsingShelf = false;
        hasReportedMissingProducts = false;
        hasReportedTripFeedback = false;
        hasJoinedCheckoutQueue = false;

        if (desiredProducts != null)
        {
            shoppingList.AddRange(desiredProducts);
        }

        checkoutRegister = checkout;
        exitPoint = exitTarget;
        UpdateStatusLabel();
        AdvanceToNextTargetShelf();
    }

    private void Update()
    {
        switch (currentState)
        {
            case CustomerState.Shopping:
                WalkToShelf();
                break;

            case CustomerState.WalkingToCheckout:
                WalkToCheckout();
                break;

            case CustomerState.CheckingOut:
                ProcessCheckoutPause();
                break;

            case CustomerState.LeavingStore:
                LeaveStore();
                break;
        }
    }

    private void WalkToShelf()
    {
        if (targetShelf == null)
        {
            AdvanceToNextTargetShelf();
            return;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            SetDestination(GetShelfBrowseDestination(targetShelf));
            repathTimer = repathInterval;
        }

        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= stoppingDistance)
        {
            if (!isBrowsingShelf)
            {
                isBrowsingShelf = true;
                shelfBrowseTimer = shelfBrowseTime;
                if (navMeshAgent != null)
                {
                    navMeshAgent.isStopped = true;
                }
            }

            FaceTarget(targetShelf.transform.position);
            shelfBrowseTimer -= Time.deltaTime;
            if (shelfBrowseTimer > 0f)
            {
                return;
            }

            ProductData desiredProduct = shoppingList[currentShoppingIndex];
            if (ShouldRefuseShelfPrice(targetShelf, desiredProduct))
            {
                AddTooExpensiveProduct(desiredProduct);
            }
            else if (targetShelf.TryTakeOneItem())
            {
                carriedProducts.Add(targetShelf.AssignedProduct);
            }
            else
            {
                AddMissingProduct(shoppingList[currentShoppingIndex]);
            }

            UpdateStatusLabel();
            isBrowsingShelf = false;
            if (navMeshAgent != null)
            {
                navMeshAgent.isStopped = false;
            }

            currentShoppingIndex++;
            AdvanceToNextTargetShelf();
        }
    }

    private void WalkToCheckout()
    {
        if (checkoutRegister == null)
        {
            currentState = CustomerState.LeavingStore;
            hasReportedMissingProducts = true;
            UpdateStatusLabel();
            SetDestination(exitPoint != null ? exitPoint.position : transform.position);
            return;
        }

        if (!hasJoinedCheckoutQueue)
        {
            checkoutRegister.JoinQueue(this);
            hasJoinedCheckoutQueue = true;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            SetDestination(checkoutRegister.GetQueuePosition(this));
            repathTimer = repathInterval;
        }

        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= stoppingDistance)
        {
            FaceTarget(checkoutRegister.GetServiceLookPosition());

            if (!checkoutRegister.IsFirstInQueue(this))
            {
                UpdateStatusLabel();
                return;
            }

            currentState = CustomerState.CheckingOut;
            checkoutTimer = checkoutDuration;
            if (navMeshAgent != null)
            {
                navMeshAgent.isStopped = true;
            }

            UpdateStatusLabel();
        }
    }

    private void ProcessCheckoutPause()
    {
        checkoutTimer -= Time.deltaTime;
        if (checkoutTimer > 0f)
        {
            return;
        }

        FaceTarget(checkoutRegister.GetServiceLookPosition());
        foreach (ProductData product in carriedProducts)
        {
            checkoutRegister.ProcessCustomer(this, product);
        }

        checkoutRegister.LeaveQueue(this);
        hasJoinedCheckoutQueue = false;
        hasReportedMissingProducts = true;
        currentState = CustomerState.LeavingStore;
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = false;
        }

        UpdateStatusLabel();
        if (exitPoint != null)
        {
            SetDestination(exitPoint.position);
        }
    }

    private void LeaveStore()
    {
        if (exitPoint == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= stoppingDistance)
        {
            Destroy(gameObject);
        }
    }

    private void AdvanceToNextTargetShelf()
    {
        if (currentShoppingIndex >= shoppingList.Count)
        {
            FinishShoppingTrip();
            return;
        }

        targetShelf = FindBestShelfForCurrentItem();

        if (targetShelf != null)
        {
            currentState = CustomerState.Shopping;
            isBrowsingShelf = false;
            if (navMeshAgent != null)
            {
                navMeshAgent.isStopped = false;
            }

            SetDestination(GetShelfBrowseDestination(targetShelf));
            repathTimer = repathInterval;
            UpdateStatusLabel();
            return;
        }

        AddCurrentProductAsMissing();
        currentShoppingIndex++;
        while (currentShoppingIndex < shoppingList.Count)
        {
            targetShelf = FindBestShelfForCurrentItem();
            if (targetShelf != null)
            {
                currentState = CustomerState.Shopping;
                isBrowsingShelf = false;
                if (navMeshAgent != null)
                {
                    navMeshAgent.isStopped = false;
                }

                SetDestination(GetShelfBrowseDestination(targetShelf));
                repathTimer = repathInterval;
                UpdateStatusLabel();
                return;
            }

            AddCurrentProductAsMissing();
            currentShoppingIndex++;
        }

        FinishShoppingTrip();
    }

    private void FinishShoppingTrip()
    {
        ReportTripFeedback();

        if ((carriedProducts.Count > 0 || missingProducts.Count > 0 || tooExpensiveProducts.Count > 0) && checkoutRegister != null)
        {
            currentState = CustomerState.WalkingToCheckout;
            checkoutRegister.JoinQueue(this);
            hasJoinedCheckoutQueue = true;
            SetDestination(checkoutRegister.GetQueuePosition(this));
            repathTimer = repathInterval;
        }
        else
        {
            hasReportedMissingProducts = missingProducts.Count > 0 || tooExpensiveProducts.Count > 0;
            currentState = CustomerState.LeavingStore;
            if (exitPoint != null)
            {
                SetDestination(exitPoint.position);
            }
        }

        UpdateStatusLabel();
    }

    private void ReportTripFeedback()
    {
        if (hasReportedTripFeedback || GameManager.Instance == null)
        {
            return;
        }

        hasReportedTripFeedback = true;
        GameManager.Instance.RecordCustomerShoppingFeedback(missingProducts.Count, tooExpensiveProducts.Count);
    }

    private Shelf FindBestShelfForCurrentItem()
    {
        if (currentShoppingIndex < 0 || currentShoppingIndex >= shoppingList.Count)
        {
            return null;
        }

        ProductData desiredProduct = shoppingList[currentShoppingIndex];
        if (desiredProduct == null)
        {
            return null;
        }

        Shelf[] shelves = FindObjectsByType<Shelf>();
        Shelf bestStockedShelf = null;
        Shelf bestEmptyShelf = null;
        float bestStockedDistance = float.MaxValue;
        float bestEmptyDistance = float.MaxValue;

        foreach (Shelf shelf in shelves)
        {
            if (shelf == null || shelf.AssignedProduct != desiredProduct)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, shelf.transform.position);
            if (shelf.HasStock && distance < bestStockedDistance)
            {
                bestStockedDistance = distance;
                bestStockedShelf = shelf;
            }
            else if (!shelf.HasStock && distance < bestEmptyDistance)
            {
                bestEmptyDistance = distance;
                bestEmptyShelf = shelf;
            }
        }

        return bestStockedShelf != null ? bestStockedShelf : bestEmptyShelf;
    }

    private void SetDestination(Vector3 targetPosition)
    {
        if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        navMeshAgent.SetDestination(GetNearestNavMeshPosition(targetPosition));
    }

    private Vector3 GetShelfBrowseDestination(Shelf shelf)
    {
        if (shelf == null)
        {
            return transform.position;
        }

        return shelf.GetCustomerBrowsePosition(shelfApproachDistance);
    }

    private Vector3 GetNearestNavMeshPosition(Vector3 desiredPosition)
    {
        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return desiredPosition;
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 540f * Time.deltaTime);
    }

    private void AddMissingProduct(ProductData product)
    {
        if (product != null)
        {
            missingProducts.Add(product);
        }
    }

    private void AddTooExpensiveProduct(ProductData product)
    {
        if (product != null)
        {
            tooExpensiveProducts.Add(product);
        }
    }

    private bool ShouldRefuseShelfPrice(Shelf shelf, ProductData product)
    {
        if (shelf == null || product == null || GameManager.Instance == null)
        {
            return false;
        }

        float defaultPrice = Mathf.Max(0.05f, product.price);
        float salePrice = GameManager.Instance.GetProductSalePrice(product);
        float priceMultiplier = salePrice / defaultPrice;

        if (priceMultiplier >= overpricedRefusalMultiplier)
        {
            return true;
        }

        if (priceMultiplier >= expensivePriceMultiplier)
        {
            return Random.value < 0.35f;
        }

        return false;
    }

    private void OnDestroy()
    {
        if (checkoutRegister != null && hasJoinedCheckoutQueue)
        {
            checkoutRegister.LeaveQueue(this);
        }
    }

    private void AddCurrentProductAsMissing()
    {
        if (currentShoppingIndex >= 0 && currentShoppingIndex < shoppingList.Count)
        {
            AddMissingProduct(shoppingList[currentShoppingIndex]);
        }
    }

    private void EnsureStatusLabel()
    {
        if (statusLabel != null)
        {
            return;
        }

        GameObject labelObject = new GameObject("CustomerStatusLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 2.15f, 0f);

        statusLabel = labelObject.AddComponent<TextMeshPro>();
        statusLabel.fontSize = 2.1f;
        statusLabel.alignment = TextAlignmentOptions.Center;
        statusLabel.color = Color.white;
        statusLabel.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        statusLabel.outlineWidth = 0.16f;
        labelObject.AddComponent<BillboardToCamera>();
    }

    private void UpdateStatusLabel()
    {
        EnsureStatusLabel();
        if (statusLabel == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("Needs: ");
        AppendProductNames(builder, shoppingList);

        if (carriedProducts.Count > 0)
        {
            builder.Append("\nGot: ");
            AppendProductNames(builder, carriedProducts);
        }

        if (!hasReportedMissingProducts && tooExpensiveProducts.Count > 0)
        {
            builder.Append("\nToo expensive: ");
            AppendProductNames(builder, tooExpensiveProducts);
        }

        if (hasReportedMissingProducts && missingProducts.Count > 0)
        {
            builder.Append("\nCould not find: ");
            AppendProductNames(builder, missingProducts);
        }

        if (hasReportedMissingProducts && tooExpensiveProducts.Count > 0)
        {
            builder.Append("\nToo expensive: ");
            AppendProductNames(builder, tooExpensiveProducts);
        }

        if (currentState == CustomerState.CheckingOut)
        {
            builder.Append("\nChecking out...");
        }
        else if (currentState == CustomerState.WalkingToCheckout && checkoutRegister != null && hasJoinedCheckoutQueue)
        {
            builder.Append(checkoutRegister.IsFirstInQueue(this)
                ? "\nNext at checkout"
                : "\nWaiting in line");
        }

        statusLabel.text = builder.ToString();
    }

    private void AppendProductNames(StringBuilder builder, List<ProductData> products)
    {
        if (products == null || products.Count == 0)
        {
            builder.Append("None");
            return;
        }

        for (int index = 0; index < products.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(products[index] != null ? products[index].productName : "Unknown");
        }
    }
}
