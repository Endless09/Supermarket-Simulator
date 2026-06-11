using TMPro;
using UnityEngine;

/// <summary>
/// Physical stock box pulled from the backroom and carried to a shelf for restocking.
/// </summary>
public class RestockBox : MonoBehaviour
{
    private ProductData product;
    private int amount;
    private TextMeshPro label;
    private Collider boxCollider;
    private Rigidbody boxRigidbody;
    private bool isBeingCarried;

    public ProductData Product => product;
    public int Amount => amount;

    public void Setup(ProductData boxProduct, int boxAmount)
    {
        product = boxProduct;
        amount = Mathf.Max(1, boxAmount);

        transform.localScale = new Vector3(0.7f, 0.45f, 0.7f);

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.color = ProductVisualUtility.GetProductColor(product, new Color(0.38f, 0.72f, 0.95f, 1f));
        }

        boxCollider = GetComponent<Collider>();
        EnsureRigidbody();

        if (label == null)
        {
            CreateLabel();
        }

        UpdateLabel();
    }

    public void SetCarriedState(bool carried)
    {
        isBeingCarried = carried;

        if (boxCollider != null)
        {
            boxCollider.enabled = !carried;
        }

        if (boxRigidbody != null)
        {
            boxRigidbody.linearVelocity = Vector3.zero;
            boxRigidbody.angularVelocity = Vector3.zero;
            boxRigidbody.useGravity = !carried;
            boxRigidbody.isKinematic = carried;

            if (!carried)
            {
                boxRigidbody.WakeUp();
            }
        }

        UpdateLabel();
    }

    public void SetAmount(int newAmount)
    {
        amount = Mathf.Max(1, newAmount);
        UpdateLabel();
    }

    private void OnMouseDown()
    {
        if (!isBeingCarried && GameManager.Instance != null)
        {
            GameManager.Instance.HandleRestockBoxClicked(this);
        }
    }

    private void CreateLabel()
    {
        GameObject labelObject = new GameObject("RestockLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.85f, 0f);

        label = labelObject.AddComponent<TextMeshPro>();
        label.fontSize = 3.5f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = new Color(0f, 0f, 0f, 0.8f);
        label.outlineWidth = 0.18f;

        labelObject.AddComponent<BillboardToCamera>();
    }

    private void EnsureRigidbody()
    {
        if (boxRigidbody == null)
        {
            boxRigidbody = GetComponent<Rigidbody>();
        }

        if (boxRigidbody == null)
        {
            boxRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        boxRigidbody.mass = 1.2f;
        boxRigidbody.linearDamping = 0.15f;
        boxRigidbody.angularDamping = 0.3f;
        boxRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        boxRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void UpdateLabel()
    {
        if (label == null)
        {
            return;
        }

        string productName = product != null ? product.productName : "Stock";
        label.fontSize = isBeingCarried ? 3.6f : 3.5f;
        label.color = ProductVisualUtility.GetProductColor(product, Color.white);
        label.text = isBeingCarried
            ? productName + " x" + amount
            : productName + "\nRestock x" + amount;
    }
}
