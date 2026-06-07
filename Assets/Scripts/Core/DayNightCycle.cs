using UnityEngine;

/// <summary>
/// Tracks store-day phases, the in-game clock, and simple day/night lighting.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public enum DayPhase
    {
        BeforeOpen = 0,
        Open = 1,
        AfterClose = 2
    }

    [Header("Clock")]
    [SerializeField] private float realSecondsPerDay = 600f;
    [SerializeField] private float openingHour = 8f;
    [SerializeField] private float closingHour = 20f;

    [Header("Lighting")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private float dayLightIntensity = 2f;
    [SerializeField] private float nightLightIntensity = 0.28f;
    [SerializeField] private Color dayLightColor = new Color(1f, 0.97f, 0.88f, 1f);
    [SerializeField] private Color warmLightColor = new Color(1f, 0.68f, 0.42f, 1f);
    [SerializeField] private Color nightLightColor = new Color(0.34f, 0.44f, 0.76f, 1f);
    [SerializeField] private Color dayAmbientColor = new Color(0.7f, 0.78f, 0.86f, 1f);
    [SerializeField] private Color nightAmbientColor = new Color(0.08f, 0.1f, 0.17f, 1f);

    private GameManager gameManager;
    private float currentTimeOfDayHours;
    private DayPhase currentPhase = DayPhase.BeforeOpen;
    private bool isInitialized;

    public int CurrentDay => gameManager != null ? gameManager.CurrentDay : 1;
    public DayPhase CurrentPhase => currentPhase;
    public float CurrentTimeOfDayHours => currentTimeOfDayHours;
    public float CurrentDayProgress => Mathf.Clamp01(Mathf.InverseLerp(openingHour, closingHour, currentTimeOfDayHours));
    public bool IsClockRunning => currentPhase == DayPhase.Open;
    public bool IsStoreOpen => currentPhase == DayPhase.Open;
    public bool IsAtSaveBoundary => currentPhase == DayPhase.BeforeOpen || currentPhase == DayPhase.AfterClose;
    public bool CanStartDay => currentPhase == DayPhase.BeforeOpen;
    public bool CanAdvanceToNextDay => currentPhase == DayPhase.AfterClose;

    public string StoreStateText
    {
        get
        {
            switch (currentPhase)
            {
                case DayPhase.BeforeOpen:
                    return "Preparing";
                case DayPhase.Open:
                    return "Open";
                case DayPhase.AfterClose:
                    return "Closed";
                default:
                    return "Closed";
            }
        }
    }

    public void Initialize(GameManager owner)
    {
        gameManager = owner;

        if (!isInitialized)
        {
            ResetToOpeningTime();
            isInitialized = true;
        }

        EnsureDirectionalLight();
        ApplyLighting();
    }

    private void Update()
    {
        if (gameManager == null || gameManager.IsApplyingSaveData || !IsClockRunning)
        {
            return;
        }

        float activeDayHours = Mathf.Max(0.1f, closingHour - openingHour);
        float secondsPerActiveDay = Mathf.Max(1f, realSecondsPerDay);
        currentTimeOfDayHours += (Time.deltaTime / secondsPerActiveDay) * activeDayHours;

        if (currentTimeOfDayHours >= closingHour)
        {
            currentTimeOfDayHours = closingHour;
            currentPhase = DayPhase.AfterClose;
            ApplyLighting();
            gameManager.HandleDayReachedClosingTime();
            return;
        }

        ApplyLighting();
    }

    public void StartDay()
    {
        if (currentPhase != DayPhase.BeforeOpen)
        {
            return;
        }

        currentTimeOfDayHours = openingHour;
        currentPhase = DayPhase.Open;
        ApplyLighting();
    }

    public void StartNextMorning()
    {
        ResetToOpeningTime();
    }

    public void SkipToClosingTime()
    {
        if (currentPhase == DayPhase.AfterClose)
        {
            return;
        }

        currentTimeOfDayHours = closingHour;
        currentPhase = DayPhase.AfterClose;
        ApplyLighting();
        gameManager?.HandleDayReachedClosingTime();
    }

    public void ResetToOpeningTime()
    {
        currentTimeOfDayHours = openingHour;
        currentPhase = DayPhase.BeforeOpen;
        ApplyLighting();
    }

    public void ApplySavedState(float timeOfDayHours, float dayProgress, int savedPhase, bool hasSavedPhaseState, bool hasSavedClockState)
    {
        if (hasSavedPhaseState)
        {
            currentPhase = (DayPhase)Mathf.Clamp(savedPhase, (int)DayPhase.BeforeOpen, (int)DayPhase.AfterClose);
        }
        else if (hasSavedClockState && timeOfDayHours >= closingHour)
        {
            currentPhase = DayPhase.AfterClose;
        }
        else
        {
            currentPhase = DayPhase.BeforeOpen;
        }

        switch (currentPhase)
        {
            case DayPhase.BeforeOpen:
                currentTimeOfDayHours = openingHour;
                break;
            case DayPhase.AfterClose:
                currentTimeOfDayHours = closingHour;
                break;
            case DayPhase.Open:
                currentTimeOfDayHours = hasSavedClockState
                    ? Mathf.Clamp(timeOfDayHours, openingHour, closingHour)
                    : Mathf.Lerp(openingHour, closingHour, Mathf.Clamp01(dayProgress));
                break;
        }

        ApplyLighting();
    }

    public string GetClockText()
    {
        int totalMinutes = Mathf.FloorToInt(CurrentTimeOfDayHours * 60f + 0.5f) % 1440;
        int hour24 = totalMinutes / 60;
        int minute = totalMinutes % 60;
        string suffix = hour24 >= 12 ? "PM" : "AM";
        int hour12 = hour24 % 12;
        if (hour12 == 0)
        {
            hour12 = 12;
        }

        return hour12 + ":" + minute.ToString("00") + " " + suffix;
    }

    private void EnsureDirectionalLight()
    {
        if (directionalLight != null)
        {
            return;
        }

        if (RenderSettings.sun != null)
        {
            directionalLight = RenderSettings.sun;
            return;
        }

        GameObject lightObject = GameObject.Find("Directional Light");
        if (lightObject != null)
        {
            directionalLight = lightObject.GetComponent<Light>();
        }

        if (directionalLight != null)
        {
            return;
        }

        Light[] lights = FindObjectsByType<Light>();
        foreach (Light light in lights)
        {
            if (light != null && light.type == LightType.Directional)
            {
                directionalLight = light;
                return;
            }
        }
    }

    private void ApplyLighting()
    {
        EnsureDirectionalLight();

        float daylightAmount = Mathf.Clamp01(Mathf.Sin(CurrentDayProgress * Mathf.PI));
        float sunriseAmount = Mathf.Clamp01(1f - Mathf.Abs(currentTimeOfDayHours - openingHour) / 2f);
        float sunsetAmount = Mathf.Clamp01(1f - Mathf.Abs(currentTimeOfDayHours - closingHour) / 2f);
        float warmAmount = Mathf.Max(sunriseAmount, sunsetAmount);

        Color targetColor = Color.Lerp(nightLightColor, dayLightColor, daylightAmount);
        targetColor = Color.Lerp(targetColor, warmLightColor, warmAmount * 0.65f);
        float targetIntensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, daylightAmount);

        if (directionalLight != null)
        {
            directionalLight.color = targetColor;
            directionalLight.intensity = targetIntensity;
            directionalLight.transform.rotation = Quaternion.Euler(Mathf.Lerp(25f, 155f, CurrentDayProgress), -30f, 0f);
        }

        RenderSettings.ambientLight = Color.Lerp(nightAmbientColor, dayAmbientColor, Mathf.Max(daylightAmount, 0.18f));
        RenderSettings.ambientIntensity = Mathf.Lerp(0.35f, 1f, Mathf.Max(daylightAmount, 0.18f));
    }
}
