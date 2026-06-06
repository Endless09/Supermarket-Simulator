using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns product lookup plus legacy backroom inventory that has not yet been migrated to physical warehouse racks.
/// </summary>
public class ProductInventoryManager
{
    private readonly Dictionary<ProductData, int> backroomInventory = new Dictionary<ProductData, int>();
    private readonly Dictionary<string, ProductData> knownProductsByName = new Dictionary<string, ProductData>(StringComparer.OrdinalIgnoreCase);

    public void BuildStartingInventory(IEnumerable<GameManager.ProductInventoryEntry> startingInventory)
    {
        backroomInventory.Clear();

        if (startingInventory == null)
        {
            return;
        }

        foreach (GameManager.ProductInventoryEntry entry in startingInventory)
        {
            if (entry == null || entry.product == null || entry.amount <= 0)
            {
                continue;
            }

            AddToBackroom(entry.product, entry.amount);
        }
    }

    public int GetLegacyBackroomStock(ProductData product)
    {
        if (product == null)
        {
            return 0;
        }

        return backroomInventory.TryGetValue(product, out int amount) ? amount : 0;
    }

    public void AddToBackroom(ProductData product, int amount)
    {
        if (product == null || amount <= 0)
        {
            return;
        }

        RegisterKnownProduct(product);

        if (!backroomInventory.ContainsKey(product))
        {
            backroomInventory.Add(product, 0);
        }

        backroomInventory[product] += amount;
    }

    public void ClearBackroomInventory()
    {
        backroomInventory.Clear();
    }

    public IEnumerable<KeyValuePair<ProductData, int>> GetBackroomEntries()
    {
        return backroomInventory;
    }

    public void RegisterKnownProducts(IEnumerable<ProductData> products)
    {
        if (products == null)
        {
            return;
        }

        foreach (ProductData product in products)
        {
            RegisterKnownProduct(product);
        }
    }

    public void RegisterKnownProduct(ProductData product)
    {
        if (product == null || string.IsNullOrWhiteSpace(product.productName))
        {
            return;
        }

        knownProductsByName[product.productName] = product;
    }

    public IEnumerable<ProductData> GetKnownProducts()
    {
        return knownProductsByName.Values;
    }

    public void MigrateLegacyBackroomInventory(WarehouseManager warehouseManager)
    {
        if (warehouseManager == null || backroomInventory.Count == 0)
        {
            return;
        }

        warehouseManager.MigrateLegacyBackroomInventory(backroomInventory);
    }

    public ProductData ResolveProductByName(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return null;
        }

        if (knownProductsByName.TryGetValue(productName, out ProductData knownProduct))
        {
            return knownProduct;
        }

        foreach (KeyValuePair<ProductData, int> entry in backroomInventory)
        {
            if (entry.Key != null && string.Equals(entry.Key.productName, productName, StringComparison.OrdinalIgnoreCase))
            {
                RegisterKnownProduct(entry.Key);
                return entry.Key;
            }
        }

        Shelf[] shelves = UnityEngine.Object.FindObjectsByType<Shelf>();
        foreach (Shelf shelf in shelves)
        {
            if (shelf != null && shelf.AssignedProduct != null &&
                string.Equals(shelf.AssignedProduct.productName, productName, StringComparison.OrdinalIgnoreCase))
            {
                RegisterKnownProduct(shelf.AssignedProduct);
                return shelf.AssignedProduct;
            }
        }

        return null;
    }
}
