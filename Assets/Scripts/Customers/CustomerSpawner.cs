using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine;

/// <summary>
/// Spawns customers at a set interval.
/// Each customer generates a small shopping list from products
/// that are currently available on stocked shelves.
/// </summary>
public class CustomerSpawner : MonoBehaviour
{
    [SerializeField] private Customer customerPrefab;
    [SerializeField] private CheckoutRegister checkoutRegister;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float spawnInterval = 8f;
    [SerializeField] private int maxCustomersInStore = 3;
    [SerializeField] private int minItemsPerCustomer = 1;
    [SerializeField] private int maxItemsPerCustomer = 2;

    private readonly List<Customer> activeCustomers = new List<Customer>();

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            CleanupCustomerList();

            if (activeCustomers.Count >= maxCustomersInStore)
            {
                continue;
            }

            List<ProductData> shoppingList = BuildShoppingList();
            if (shoppingList.Count == 0 || customerPrefab == null || checkoutRegister == null)
            {
                continue;
            }

            Vector3 spawnPosition = transform.position;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
            }

            Customer newCustomer = Instantiate(customerPrefab, spawnPosition, Quaternion.identity);
            newCustomer.Setup(shoppingList, checkoutRegister, exitPoint);
            activeCustomers.Add(newCustomer);
        }
    }

    private List<ProductData> BuildShoppingList()
    {
        List<ProductData> demandProducts = new List<ProductData>();

        if (GameManager.Instance != null)
        {
            foreach (ProductData product in GameManager.Instance.GetKnownProducts())
            {
                if (product != null && !demandProducts.Contains(product))
                {
                    demandProducts.Add(product);
                }
            }
        }

        if (demandProducts.Count == 0)
        {
            Shelf[] shelves = FindObjectsByType<Shelf>();
            foreach (Shelf shelf in shelves)
            {
                if (shelf.AssignedProduct == null || demandProducts.Contains(shelf.AssignedProduct))
                {
                    continue;
                }

                demandProducts.Add(shelf.AssignedProduct);
            }
        }

        if (demandProducts.Count == 0)
        {
            return demandProducts;
        }

        int minItemCount = Mathf.Clamp(minItemsPerCustomer, 1, demandProducts.Count);
        int maxItemCount = Mathf.Clamp(maxItemsPerCustomer, minItemCount, demandProducts.Count);
        int desiredCount = Random.Range(minItemCount, maxItemCount + 1);
        List<ProductData> shoppingList = new List<ProductData>();

        for (int index = 0; index < desiredCount && demandProducts.Count > 0; index++)
        {
            int choiceIndex = Random.Range(0, demandProducts.Count);
            shoppingList.Add(demandProducts[choiceIndex]);
            demandProducts.RemoveAt(choiceIndex);
        }

        return shoppingList;
    }

    private void CleanupCustomerList()
    {
        activeCustomers.RemoveAll(customer => customer == null);
    }
}
