using UnityEngine;

/// <summary>
/// Product data for items sold in the supermarket.
/// Create assets from the Unity Create menu so designers can add new products easily.
/// </summary>
[CreateAssetMenu(fileName = "NewProduct", menuName = "Supermarket/Product Data")]
public class ProductData : ScriptableObject
{
    public string productName;
    public float price = 5f;
    public float wholesaleCost = 2f;
    public string category;
    public int expirationDays = 7;
    public bool requiresRefrigeration;
}
