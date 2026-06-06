using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores player-controlled sale prices separately from ProductData so future market pricing can vary independently.
/// </summary>
public class ProductPricingManager
{
    private readonly Dictionary<ProductData, float> salePrices = new Dictionary<ProductData, float>();

    public float GetSalePrice(ProductData product)
    {
        if (product == null)
        {
            return 0f;
        }

        if (!salePrices.TryGetValue(product, out float price))
        {
            price = GetDefaultSalePrice(product);
            salePrices[product] = price;
        }

        return price;
    }

    public void SetSalePrice(ProductData product, float price)
    {
        if (product == null)
        {
            return;
        }

        salePrices[product] = Mathf.Max(0.05f, price);
    }

    public void ResetSalePrice(ProductData product)
    {
        if (product == null)
        {
            return;
        }

        salePrices[product] = GetDefaultSalePrice(product);
    }

    public void ResetAll()
    {
        salePrices.Clear();
    }

    public List<ProductPriceSaveEntry> BuildSaveEntries()
    {
        List<ProductPriceSaveEntry> entries = new List<ProductPriceSaveEntry>();
        foreach (KeyValuePair<ProductData, float> entry in salePrices)
        {
            if (entry.Key == null)
            {
                continue;
            }

            entries.Add(new ProductPriceSaveEntry
            {
                productName = entry.Key.productName,
                salePrice = entry.Value
            });
        }

        return entries;
    }

    private float GetDefaultSalePrice(ProductData product)
    {
        return product != null ? Mathf.Max(0.05f, product.price) : 0f;
    }
}
