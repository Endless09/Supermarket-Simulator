using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serialized snapshot of the current game state.
/// </summary>
[Serializable]
public class GameSaveData
{
    public int currentDay;
    public float currentMoney;
    public string selectedProductName;
    public bool hasDayNightCycleState;
    public bool hasDayPhaseState;
    public int dayPhase;
    public float currentTimeOfDayHours;
    public float currentDayProgress;
    public bool isStoreOpen;
    public List<InventorySaveEntry> backroomInventory = new List<InventorySaveEntry>();
    public List<ShelfSaveEntry> shelves = new List<ShelfSaveEntry>();
    public List<DeliveryManager.QueuedDeliveryState> deliveries = new List<DeliveryManager.QueuedDeliveryState>();
    public List<DeliveryManager.DockCrateState> deliveryCrates = new List<DeliveryManager.DockCrateState>();
    public List<RestockBoxSaveEntry> restockBoxes = new List<RestockBoxSaveEntry>();
    public List<WarehouseShelfSaveEntry> warehouseShelves = new List<WarehouseShelfSaveEntry>();
}

[Serializable]
public class InventorySaveEntry
{
    public string productName;
    public int amount;
}

[Serializable]
public class ShelfSaveEntry
{
    public string productName;
    public int stock;
    public Vector3 position;
}

[Serializable]
public class RestockBoxSaveEntry
{
    public string productName;
    public int amount;
    public Vector3 position;
    public bool isCarried;
}

[Serializable]
public class WarehouseShelfSaveEntry
{
    public string lockedProductName;
    public Vector3 position;
    public List<WarehouseShelfSlotSaveEntry> slots = new List<WarehouseShelfSlotSaveEntry>();
}

[Serializable]
public class WarehouseShelfSlotSaveEntry
{
    public int slotIndex;
    public string productName;
    public int amount;
}
