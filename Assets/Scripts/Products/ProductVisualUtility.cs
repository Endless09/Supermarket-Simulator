using UnityEngine;

public static class ProductVisualUtility
{
    private static readonly Color[] ProductColors =
    {
        new Color(0.88f, 0.26f, 0.22f, 1f),
        new Color(0.95f, 0.74f, 0.32f, 1f),
        new Color(0.34f, 0.65f, 0.92f, 1f),
        new Color(0.42f, 0.78f, 0.46f, 1f),
        new Color(0.82f, 0.42f, 0.86f, 1f),
        new Color(0.95f, 0.55f, 0.28f, 1f),
        new Color(0.36f, 0.78f, 0.75f, 1f),
        new Color(0.7f, 0.58f, 0.43f, 1f)
    };

    public static Color GetProductColor(ProductData product, Color fallback)
    {
        if (product == null || string.IsNullOrWhiteSpace(product.productName))
        {
            return fallback;
        }

        string normalizedName = product.productName.Trim().ToLowerInvariant();
        switch (normalizedName)
        {
            case "apples":
            case "apple":
                return new Color(0.9f, 0.18f, 0.16f, 1f);
            case "bread":
                return new Color(0.86f, 0.55f, 0.24f, 1f);
            case "milk":
                return new Color(0.42f, 0.7f, 0.96f, 1f);
            case "eggs":
            case "egg":
                return new Color(0.98f, 0.88f, 0.38f, 1f);
            case "cereal":
                return new Color(0.47f, 0.86f, 0.43f, 1f);
            case "soda":
                return new Color(0.74f, 0.34f, 0.9f, 1f);
        }

        unchecked
        {
            int hash = 17;
            foreach (char character in product.productName)
            {
                hash = (hash * 31) + char.ToUpperInvariant(character);
            }

            int index = Mathf.Abs(hash) % ProductColors.Length;
            return ProductColors[index];
        }
    }
}
