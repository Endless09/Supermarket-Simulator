using System;
using UnityEngine;

/// <summary>
/// Keeps track of the player's money and provides simple helper methods
/// for spending and earning cash.
/// </summary>
public class MoneyManager : MonoBehaviour
{
    [SerializeField] private float startingMoney = 500f;

    public float StartingMoney => startingMoney;
    public float CurrentMoney { get; private set; }

    public event Action<float> OnMoneyChanged;

    private void Awake()
    {
        CurrentMoney = startingMoney;
        NotifyMoneyChanged();
    }

    public bool CanAfford(float amount)
    {
        return CurrentMoney >= amount;
    }

    public bool SpendMoney(float amount)
    {
        if (amount < 0f)
        {
            Debug.LogWarning("SpendMoney was called with a negative amount.");
            return false;
        }

        if (!CanAfford(amount))
        {
            return false;
        }

        CurrentMoney -= amount;
        NotifyMoneyChanged();
        return true;
    }

    public void AddMoney(float amount)
    {
        if (amount < 0f)
        {
            Debug.LogWarning("AddMoney was called with a negative amount.");
            return;
        }

        CurrentMoney += amount;
        NotifyMoneyChanged();
    }

    public void SetMoney(float amount)
    {
        CurrentMoney = Mathf.Max(0f, amount);
        NotifyMoneyChanged();
    }

    public void ResetToStartingMoney()
    {
        CurrentMoney = startingMoney;
        NotifyMoneyChanged();
    }

    private void NotifyMoneyChanged()
    {
        OnMoneyChanged?.Invoke(CurrentMoney);
    }
}
