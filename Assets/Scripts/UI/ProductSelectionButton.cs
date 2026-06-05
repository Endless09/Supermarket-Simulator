using TMPro;
using UnityEngine;

/// <summary>
/// Put this on a UI button to connect one product asset to the BasicUIManager.
/// It can also update the button label automatically.
/// </summary>
public class ProductSelectionButton : MonoBehaviour
{
    [SerializeField] private BasicUIManager uiManager;
    [SerializeField] private ProductData product;
    [SerializeField] private TMP_Text buttonLabel;

    private void Start()
    {
        if (uiManager == null)
        {
            uiManager = FindAnyObjectByType<BasicUIManager>();
        }

        if (buttonLabel == null)
        {
            buttonLabel = GetComponentInChildren<TMP_Text>();
        }

        if (buttonLabel != null && product != null)
        {
            buttonLabel.text = product.productName;
        }
    }

    public void SelectAssignedProduct()
    {
        if (uiManager != null && product != null)
        {
            uiManager.SelectProduct(product);
        }
    }
}
