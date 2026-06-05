using TMPro;
using UnityEngine;

/// <summary>
/// Owns shelf placement state, preview visuals, and shelf spawning.
/// </summary>
public class ShelfPlacementManager : MonoBehaviour
{
    private GameManager gameManager;
    private Camera mainCamera;
    private Shelf shelfPrefab;
    private LayerMask floorLayer;
    private float shelfBuyCost;
    private float placementHeightOffset;
    private bool autoPlaceShelves;
    private Vector3 firstShelfPosition;
    private Vector3 shelfSpacing;
    private Color shelfPreviewColor;
    private Color invalidShelfPreviewColor = new Color(1f, 0.2f, 0.16f, 0.75f);
    private bool hasUnplacedShelfPurchase;
    private int placedShelfCount;
    private bool waitingForPlacementClickRelease;
    private Shelf shelfPreviewInstance;
    private MeshRenderer[] shelfPreviewRenderers;
    private Shelf runtimeShelfTemplate;
    private Shelf movingShelf;
    private Vector3 movingShelfOriginalPosition;
    private bool currentPreviewPlacementValid;
    private float cachedTemplateBottomLift = -1f;

    public bool IsPlacingShelf { get; private set; }
    public bool IsMovingShelf => movingShelf != null;

    public void Initialize(
        GameManager owner,
        Camera camera,
        Shelf shelfPrefabValue,
        LayerMask floorLayerValue,
        float shelfBuyCostValue,
        float placementHeightOffsetValue,
        bool autoPlaceShelvesValue,
        Vector3 firstShelfPositionValue,
        Vector3 shelfSpacingValue,
        Color shelfPreviewColorValue)
    {
        gameManager = owner;
        mainCamera = camera;
        shelfPrefab = shelfPrefabValue;
        floorLayer = floorLayerValue;
        shelfBuyCost = shelfBuyCostValue;
        placementHeightOffset = placementHeightOffsetValue;
        autoPlaceShelves = autoPlaceShelvesValue;
        firstShelfPosition = firstShelfPositionValue;
        shelfSpacing = shelfSpacingValue;
        shelfPreviewColor = shelfPreviewColorValue;
        CacheRuntimeShelfTemplate();
    }

    public void HandleUpdate()
    {
        if (!IsPlacingShelf)
        {
            return;
        }

        if (waitingForPlacementClickRelease)
        {
            UpdateShelfPreviewPosition();

            if (!gameManager.IsLeftMouseButtonHeld())
            {
                waitingForPlacementClickRelease = false;
            }

            return;
        }

        UpdateShelfPreviewPosition();

        if (gameManager.GetLeftMouseButtonDown())
        {
            TryPlaceShelfFromMouse();
        }

        if (gameManager.GetRightMouseButtonDown())
        {
            CancelShelfPlacement();
        }
    }

    public void StartShelfPlacement(MoneyManager moneyManager)
    {
        if (IsPlacingShelf)
        {
            return;
        }

        if (GetShelfTemplate() == null)
        {
            Debug.LogWarning("No shelf prefab has been assigned on the GameManager.");
            return;
        }

        if (moneyManager == null)
        {
            Debug.LogWarning("MoneyManager not found in the scene.");
            return;
        }

        if (!moneyManager.SpendMoney(shelfBuyCost))
        {
            Debug.Log("Not enough money to buy a shelf.");
            return;
        }

        if (autoPlaceShelves)
        {
            Vector3 autoPlacePosition = firstShelfPosition + (shelfSpacing * placedShelfCount);
            PlaceShelfAtPosition(autoPlacePosition, false);
            return;
        }

        IsPlacingShelf = true;
        hasUnplacedShelfPurchase = true;
        waitingForPlacementClickRelease = gameManager.IsLeftMouseButtonHeld();
        CreateShelfPreview();
        Debug.Log("Shelf purchased. Left click on the floor to place it. Right click to cancel.");
    }

    public bool StartMovingShelf(Shelf shelf)
    {
        if (IsPlacingShelf || shelf == null)
        {
            return false;
        }

        movingShelf = shelf;
        movingShelfOriginalPosition = shelf.transform.position;
        IsPlacingShelf = true;
        hasUnplacedShelfPurchase = false;
        waitingForPlacementClickRelease = gameManager.IsLeftMouseButtonHeld();
        movingShelf.gameObject.SetActive(false);
        CreateShelfPreview();
        Debug.Log("Moving shelf. Left click on the floor to place it. Right click to cancel.");
        return true;
    }

    public void CancelShelfPlacement()
    {
        MoneyManager moneyManager = gameManager != null ? gameManager.MoneyManager : null;
        if (IsPlacingShelf && hasUnplacedShelfPurchase && moneyManager != null)
        {
            moneyManager.AddMoney(shelfBuyCost);
        }

        if (movingShelf != null)
        {
            movingShelf.transform.position = movingShelfOriginalPosition;
            movingShelf.gameObject.SetActive(true);
            movingShelf = null;
        }

        IsPlacingShelf = false;
        hasUnplacedShelfPurchase = false;
        waitingForPlacementClickRelease = false;
        DestroyShelfPreview();
    }

    public Shelf SpawnLoadedShelf(Vector3 worldPosition, ProductData shelfProduct, int stock)
    {
        Shelf loadedShelf = Instantiate(GetShelfTemplate(), worldPosition, Quaternion.identity);
        if (loadedShelf != null)
        {
            loadedShelf.gameObject.SetActive(true);
            loadedShelf.LoadState(shelfProduct, stock);
            placedShelfCount++;
        }

        return loadedShelf;
    }

    public void ResetPlacementCount()
    {
        placedShelfCount = 0;
    }

    public bool TryGetPlacementPosition(out Vector3 placementPosition)
    {
        placementPosition = Vector3.zero;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            Debug.LogWarning("Main camera not found.");
            return false;
        }

        Vector3 mousePosition = gameManager.GetMouseScreenPosition();
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);

        bool hitFloor = Physics.Raycast(ray, out RaycastHit hit, 100f, floorLayer);
        if (!hitFloor)
        {
            hitFloor = Physics.Raycast(ray, out hit, 100f);
        }

        if (hitFloor)
        {
            placementPosition = hit.point;
            return true;
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            placementPosition = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    public void TryPlaceShelfFromCurrentPointer()
    {
        TryPlaceShelfFromMouse();
    }

    private void TryPlaceShelfFromMouse()
    {
        if (TryGetPlacementPosition(out Vector3 placementPosition))
        {
            PlaceShelfAtPosition(placementPosition, true);
        }
    }

    private void PlaceShelfAtPosition(Vector3 worldPosition, bool addHeightOffset)
    {
        Vector3 spawnPosition = worldPosition;
        float baseLift = GetBasePlacementLift(addHeightOffset);
        spawnPosition.y += baseLift;

        if (!IsPlacementPositionValid(spawnPosition))
        {
            Debug.Log("Cannot place shelf here. Move it away from shelves, walls, customers, checkout, or warehouse racks.");
            return;
        }

        if (movingShelf != null)
        {
            movingShelf.transform.position = spawnPosition;
            movingShelf.gameObject.SetActive(true);
            movingShelf = null;
        }
        else
        {
            Shelf newShelf = Instantiate(GetShelfTemplate(), spawnPosition, Quaternion.identity);
            if (newShelf != null)
            {
                newShelf.gameObject.SetActive(true);
                newShelf.InitializePlacedShelf(null);
            }

            placedShelfCount++;
        }

        IsPlacingShelf = false;
        hasUnplacedShelfPurchase = false;
        waitingForPlacementClickRelease = false;
        DestroyShelfPreview();
        gameManager.QueueStoreNavigationRebuild();
        gameManager.NotifyStateChanged();
    }

    private void UpdateShelfPreviewPosition()
    {
        if (!IsPlacingShelf || shelfPreviewInstance == null)
        {
            return;
        }

        if (TryGetPlacementPosition(out Vector3 placementPosition))
        {
            Vector3 previewPosition = placementPosition;
            previewPosition.y += GetBasePlacementLift(true);
            shelfPreviewInstance.transform.position = previewPosition;
            currentPreviewPlacementValid = IsPlacementPositionValid(previewPosition);
            ApplyShelfPreviewColor(currentPreviewPlacementValid);

            if (!shelfPreviewInstance.gameObject.activeSelf)
            {
                shelfPreviewInstance.gameObject.SetActive(true);
            }
        }
        else if (shelfPreviewInstance.gameObject.activeSelf)
        {
            shelfPreviewInstance.gameObject.SetActive(false);
            currentPreviewPlacementValid = false;
        }
    }

    private void CreateShelfPreview()
    {
        if (GetShelfTemplate() == null || shelfPreviewInstance != null)
        {
            return;
        }

        shelfPreviewInstance = Instantiate(GetShelfTemplate(), Vector3.zero, Quaternion.identity);
        shelfPreviewInstance.gameObject.name = GetShelfTemplate().name + "_Preview";

        Shelf previewShelf = shelfPreviewInstance.GetComponent<Shelf>();
        if (previewShelf != null)
        {
            previewShelf.enabled = false;
        }

        Collider[] previewColliders = shelfPreviewInstance.GetComponentsInChildren<Collider>();
        foreach (Collider previewCollider in previewColliders)
        {
            previewCollider.enabled = false;
        }

        shelfPreviewRenderers = shelfPreviewInstance.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer previewRenderer in shelfPreviewRenderers)
        {
            previewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            previewRenderer.receiveShadows = false;

            Material previewMaterial = new Material(previewRenderer.material);
            previewMaterial.color = shelfPreviewColor;
            previewRenderer.material = previewMaterial;
        }

        shelfPreviewInstance.gameObject.SetActive(false);
    }

    private bool IsPlacementPositionValid(Vector3 shelfWorldPosition)
    {
        Bounds placementBounds = GetShelfPlacementBounds(shelfWorldPosition);
        if (placementBounds.size == Vector3.zero)
        {
            return true;
        }

        Vector3 overlapCenter = placementBounds.center;
        Vector3 overlapHalfExtents = placementBounds.extents;
        overlapHalfExtents.x = Mathf.Max(0.05f, overlapHalfExtents.x - 0.05f);
        overlapHalfExtents.y = Mathf.Max(0.05f, overlapHalfExtents.y - 0.04f);
        overlapHalfExtents.z = Mathf.Max(0.05f, overlapHalfExtents.z - 0.05f);

        Collider[] overlaps = Physics.OverlapBox(
            overlapCenter,
            overlapHalfExtents,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide);

        foreach (Collider overlap in overlaps)
        {
            if (IsIgnoredPlacementOverlap(overlap, placementBounds))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private Bounds GetShelfPlacementBounds(Vector3 shelfWorldPosition)
    {
        if (shelfPreviewInstance != null)
        {
            Bounds previewBounds = CalculateObjectBounds(shelfPreviewInstance.gameObject);
            if (previewBounds.size != Vector3.zero)
            {
                return previewBounds;
            }
        }

        Shelf template = GetShelfTemplate();
        if (template == null)
        {
            return new Bounds(shelfWorldPosition, Vector3.zero);
        }

        Bounds templateBounds = CalculateObjectBounds(template.gameObject);
        if (templateBounds.size == Vector3.zero)
        {
            return new Bounds(shelfWorldPosition, Vector3.zero);
        }

        Vector3 offsetFromTemplateRoot = templateBounds.center - template.transform.position;
        return new Bounds(shelfWorldPosition + offsetFromTemplateRoot, templateBounds.size);
    }

    private Bounds CalculateObjectBounds(GameObject sourceObject)
    {
        Bounds bounds = new Bounds();
        bool hasBounds = false;

        Collider[] colliders = sourceObject.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (hasBounds)
        {
            return bounds;
        }

        Renderer[] renderers = sourceObject.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.GetComponentInParent<TextMeshPro>() != null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? bounds : new Bounds(sourceObject.transform.position, Vector3.zero);
    }

    private bool IsIgnoredPlacementOverlap(Collider overlap, Bounds placementBounds)
    {
        if (overlap == null)
        {
            return true;
        }

        if (shelfPreviewInstance != null && overlap.transform.IsChildOf(shelfPreviewInstance.transform))
        {
            return true;
        }

        if (movingShelf != null && overlap.transform.IsChildOf(movingShelf.transform))
        {
            return true;
        }

        if (runtimeShelfTemplate != null && overlap.transform.IsChildOf(runtimeShelfTemplate.transform))
        {
            return true;
        }

        if (shelfPrefab != null &&
            !shelfPrefab.gameObject.scene.IsValid() &&
            overlap.transform.IsChildOf(shelfPrefab.transform))
        {
            return true;
        }

        return IsPlacementSurface(overlap, placementBounds);
    }

    private bool IsPlacementSurface(Collider overlap, Bounds placementBounds)
    {
        Bounds overlapBounds = overlap.bounds;
        bool isBelowShelf = overlapBounds.max.y <= placementBounds.min.y + 0.12f;
        bool isThinSurface = overlapBounds.size.y <= 0.2f;
        string overlapName = overlap.gameObject.name;
        bool looksLikeGround = overlapName.Contains("Floor") ||
                               overlapName.Contains("Ground") ||
                               overlapName.Contains("Pad") ||
                               overlapName.Contains("Zone");

        return isBelowShelf && (isThinSurface || looksLikeGround);
    }

    private void ApplyShelfPreviewColor(bool isValidPlacement)
    {
        if (shelfPreviewRenderers == null)
        {
            return;
        }

        Color targetColor = isValidPlacement ? shelfPreviewColor : invalidShelfPreviewColor;
        foreach (MeshRenderer previewRenderer in shelfPreviewRenderers)
        {
            if (previewRenderer != null && previewRenderer.material != null)
            {
                previewRenderer.material.color = targetColor;
            }
        }
    }

    private void CacheRuntimeShelfTemplate()
    {
        if (shelfPrefab == null)
        {
            return;
        }

        if (!shelfPrefab.gameObject.scene.IsValid())
        {
            runtimeShelfTemplate = shelfPrefab;
            return;
        }

        runtimeShelfTemplate = Instantiate(shelfPrefab, Vector3.zero, Quaternion.identity);
        runtimeShelfTemplate.gameObject.name = shelfPrefab.name + "_Template";
        runtimeShelfTemplate.gameObject.SetActive(false);
        runtimeShelfTemplate.gameObject.hideFlags = HideFlags.HideInHierarchy;
        cachedTemplateBottomLift = -1f;
    }

    private Shelf GetShelfTemplate()
    {
        return runtimeShelfTemplate != null ? runtimeShelfTemplate : shelfPrefab;
    }

    private void DestroyShelfPreview()
    {
        if (shelfPreviewInstance == null)
        {
            return;
        }

        Destroy(shelfPreviewInstance.gameObject);
        shelfPreviewInstance = null;
    }

    private float GetTemplateBottomLift()
    {
        if (cachedTemplateBottomLift >= 0f)
        {
            return cachedTemplateBottomLift;
        }

        Shelf template = GetShelfTemplate();
        if (template == null)
        {
            cachedTemplateBottomLift = 0f;
            return cachedTemplateBottomLift;
        }

        Collider[] colliders = template.GetComponentsInChildren<Collider>(true);
        float highestLift = 0f;

        foreach (Collider collider in colliders)
        {
            if (collider is BoxCollider boxCollider)
            {
                float scaledHalfHeight = (boxCollider.size.y * 0.5f) * Mathf.Abs(boxCollider.transform.lossyScale.y);
                float scaledCenterY = boxCollider.center.y * Mathf.Abs(boxCollider.transform.lossyScale.y);
                highestLift = Mathf.Max(highestLift, scaledHalfHeight - scaledCenterY);
            }
            else if (collider is SphereCollider sphereCollider)
            {
                float scaledRadius = sphereCollider.radius * Mathf.Abs(sphereCollider.transform.lossyScale.y);
                float scaledCenterY = sphereCollider.center.y * Mathf.Abs(sphereCollider.transform.lossyScale.y);
                highestLift = Mathf.Max(highestLift, scaledRadius - scaledCenterY);
            }
            else if (collider is CapsuleCollider capsuleCollider)
            {
                float scaledHalfHeight = (capsuleCollider.height * 0.5f) * Mathf.Abs(capsuleCollider.transform.lossyScale.y);
                float scaledCenterY = capsuleCollider.center.y * Mathf.Abs(capsuleCollider.transform.lossyScale.y);
                highestLift = Mathf.Max(highestLift, scaledHalfHeight - scaledCenterY);
            }
        }

        if (highestLift <= 0f)
        {
            Renderer[] renderers = template.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                highestLift = Mathf.Max(highestLift, renderer.localBounds.extents.y);
            }
        }

        cachedTemplateBottomLift = Mathf.Max(0f, highestLift);
        return cachedTemplateBottomLift;
    }

    private float GetBasePlacementLift(bool includeConfiguredOffset)
    {
        float derivedLift = GetTemplateBottomLift();
        if (!includeConfiguredOffset)
        {
            return derivedLift;
        }

        return Mathf.Max(derivedLift, placementHeightOffset);
    }
}
