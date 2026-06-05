using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Owns player-view interaction prompts, focused-object detection, and input helpers.
/// </summary>
public class PlayerInteractionManager : MonoBehaviour
{
    private GameManager gameManager;
    private Camera mainCamera;
    private float interactionDistance;

    public void Initialize(GameManager owner, Camera camera, float interactionDistanceValue)
    {
        gameManager = owner;
        mainCamera = camera;
        interactionDistance = interactionDistanceValue;
    }

    public string GetPlayerInteractionPrompt()
    {
        if (FirstPersonPlayerController.Instance == null || !FirstPersonPlayerController.Instance.IsPlayerViewActive)
        {
            return string.Empty;
        }

        DeliveryManager deliveryManager = gameManager.DeliveryManager;
        WarehouseManager warehouseManager = gameManager.WarehouseManager;
        TrashManager trashManager = gameManager.TrashManager;
        bool isLookingAtBackroomDropZone = false;
        bool isLookingAtTrashZone = false;

        if (gameManager.IsMovingShelf)
        {
            return "Left click to place shelf, right click to cancel";
        }

        if (gameManager.IsPlacingShelf)
        {
            return "Left click to place new shelf, right click to cancel";
        }

        if (deliveryManager != null &&
            deliveryManager.IsCarryingCrate &&
            warehouseManager != null &&
            gameManager.TryGetPlacementPositionFromMouse(out Vector3 promptPlacementPosition))
        {
            isLookingAtBackroomDropZone = warehouseManager.IsPointInsideDropZone(promptPlacementPosition);
        }

        if (gameManager.IsCarryingEmptyBox &&
            trashManager != null &&
            gameManager.TryGetPlacementPositionFromMouse(out Vector3 trashPromptPosition))
        {
            isLookingAtTrashZone = trashManager.IsPointInsideZone(trashPromptPosition);
        }

        if (gameManager.IsCarryingEmptyBox)
        {
            return isLookingAtTrashZone
                ? "[E] Throw away empty box"
                : "Take the empty box to the Baler";
        }

        if (!TryGetFocusedInteractable(out Shelf shelf, out DeliveryCrate crate, out WarehouseStockBox warehouseStockBox))
        {
            if (deliveryManager != null && deliveryManager.IsCarryingCrate)
            {
                return deliveryManager.GetCarryInteractionPrompt(isLookingAtBackroomDropZone);
            }

            if (gameManager.IsCarryingRestockBox)
            {
                return "Look at a matching shelf and press [E] to stock it";
            }

            return string.Empty;
        }

        if (crate != null &&
            deliveryManager != null &&
            !deliveryManager.IsCarryingCrate &&
            !gameManager.IsCarryingRestockBox &&
            !gameManager.IsCarryingEmptyBox)
        {
            return deliveryManager.GetPickupPrompt(crate);
        }

        if (warehouseStockBox != null &&
            (deliveryManager == null || !deliveryManager.IsCarryingCrate) &&
            !gameManager.IsCarryingRestockBox &&
            !gameManager.IsCarryingEmptyBox)
        {
            return warehouseManager != null
                ? warehouseManager.GetPrompt(warehouseStockBox, gameManager.GetSelectedOrFocusedShelf(), mainCamera)
                : string.Empty;
        }

        if (shelf != null)
        {
            if (deliveryManager != null &&
                deliveryManager.IsCarryingCrate &&
                shelf.CanAcceptProduct(deliveryManager.CarriedCrateProduct))
            {
                return "[E] Stock shelf from crate";
            }

            if (gameManager.IsCarryingRestockBox && shelf.CanAcceptRestock(gameManager.CarriedRestockProduct))
            {
                return "[E] Stock shelf";
            }

            if (!gameManager.IsCarryingRestockBox && (deliveryManager == null || !deliveryManager.IsCarryingCrate))
            {
                return "[E] Select shelf";
            }
        }

        return string.Empty;
    }

    public void HandlePlayerViewInteraction()
    {
        DeliveryManager deliveryManager = gameManager.DeliveryManager;
        WarehouseManager warehouseManager = gameManager.WarehouseManager;
        TrashManager trashManager = gameManager.TrashManager;

        bool isLookingAtBackroomDropZone = false;
        if (deliveryManager != null &&
            deliveryManager.IsCarryingCrate &&
            warehouseManager != null &&
            gameManager.TryGetPlacementPositionFromMouse(out Vector3 interactionPlacementPosition))
        {
            isLookingAtBackroomDropZone = warehouseManager.IsPointInsideDropZone(interactionPlacementPosition);
        }

        if (gameManager.IsCarryingEmptyBox &&
            trashManager != null &&
            gameManager.TryGetPlacementPositionFromMouse(out Vector3 trashInteractionPosition) &&
            trashManager.IsPointInsideZone(trashInteractionPosition))
        {
            gameManager.TryDisposeCarriedEmptyBoxFromPointer();
            return;
        }

        if (isLookingAtBackroomDropZone && deliveryManager != null && deliveryManager.IsCarryingCrate)
        {
            deliveryManager.TryUnloadCarriedCrateAtDropZone(true);
            return;
        }

        if (TryGetFocusedInteractable(out Shelf shelf, out DeliveryCrate crate, out WarehouseStockBox warehouseStockBox))
        {
            if (crate != null)
            {
                deliveryManager?.HandleCrateClicked(crate);
                return;
            }

            if (warehouseStockBox != null)
            {
                gameManager.HandleWarehouseStockBoxClicked(warehouseStockBox);
                return;
            }

            if (shelf != null)
            {
                gameManager.HandleShelfClicked(shelf);
                return;
            }
        }

        if (gameManager.IsPlacingShelf)
        {
            gameManager.PlacePendingShelfFromCurrentPointer();
        }
    }

    public Shelf GetFocusedShelf()
    {
        return TryGetFocusedInteractable(out Shelf shelf, out DeliveryCrate crate, out WarehouseStockBox warehouseStockBox) &&
               shelf != null &&
               crate == null &&
               warehouseStockBox == null
            ? shelf
            : null;
    }

    public bool TryGetFocusedInteractable(out Shelf shelf, out DeliveryCrate crate, out WarehouseStockBox warehouseStockBox)
    {
        shelf = null;
        crate = null;
        warehouseStockBox = null;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            crate = hit.collider.GetComponentInParent<DeliveryCrate>();
            if (crate != null)
            {
                return true;
            }

            warehouseStockBox = hit.collider.GetComponentInParent<WarehouseStockBox>();
            if (warehouseStockBox != null)
            {
                return true;
            }

            shelf = hit.collider.GetComponentInParent<Shelf>();
            if (shelf != null)
            {
                return true;
            }
        }

        return false;
    }

    public bool GetLeftMouseButtonDown()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    public bool GetRightMouseButtonDown()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    public bool IsLeftMouseButtonHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    public bool IsPlayerViewInteractionPressed()
    {
        if (FirstPersonPlayerController.Instance == null || !FirstPersonPlayerController.Instance.IsPlayerViewActive)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    public Vector3 GetMouseScreenPosition()
    {
        if (FirstPersonPlayerController.Instance != null &&
            FirstPersonPlayerController.Instance.UseCenteredCursorForWorldInteractions)
        {
            return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
#else
        return Input.mousePosition;
#endif

        return Vector3.zero;
    }
}
