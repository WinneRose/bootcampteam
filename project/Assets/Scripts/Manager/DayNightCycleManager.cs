using UnityEngine;

public class DayNightCycleManager : MonoBehaviour
{
    [Header("Time")]
    [Range(0, 24)] public float currentTime = 12f;
    private float timeSpeed;

    [Header("References")]
    public Light sun;
    public Material skyboxMaterial;
    public DayNightProfile profile;

    private void Start()
    {
        if (profile != null)
        {
            timeSpeed = 24f / (profile.dayLengthInMinutes * 60f);
        }
    }

    private void Update()
    {
        if (profile == null) return;

        currentTime += Time.deltaTime * timeSpeed;
        if (currentTime >= 24f) currentTime -= 24f;

        UpdateSun();
        UpdateSkybox();
    }

    private void UpdateSun()
    {
        float timePercent = currentTime / 24f;
        float sunAngle = timePercent * 360f - 90f;

        sun.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
        sun.color = LerpByTime(
            profile.morningSunColor,
            profile.noonSunColor,
            profile.eveningSunColor,
            profile.nightSunColor
        );

        sun.intensity = profile.sunIntensity.Evaluate(timePercent);
    }

    private void UpdateSkybox()
    {
        if (skyboxMaterial == null) return;

        float timePercent = currentTime / 24f;

        // 🌅 Sky gradient
        Color top = LerpByTime(
            profile.morningSkyTop,
            profile.noonSkyTop,
            profile.eveningSkyTop,
            profile.nightSkyTop
        );

        Color bottom = LerpByTime(
            profile.morningSkyBottom,
            profile.noonSkyBottom,
            profile.eveningSkyBottom,
            profile.nightSkyBottom
        );

        skyboxMaterial.SetColor("_MainColor", top);
        skyboxMaterial.SetColor("_SecondColor", bottom);

        // 🌠 Stars
        skyboxMaterial.SetFloat("_StarVisibility", profile.starVisibility.Evaluate(timePercent));
        skyboxMaterial.SetFloat("_StarsDensity", profile.starsDensity);

        // 🌄 Skybox positioning
        skyboxMaterial.SetFloat("_Height", profile.skyboxHeight);
        skyboxMaterial.SetVector("_Tiling", profile.skyboxTiling);

        // 💡 Lighting
        RenderSettings.skybox = skyboxMaterial;
        RenderSettings.ambientLight = LerpByTime(
            profile.morningAmbient,
            profile.noonAmbient,
            profile.eveningAmbient,
            profile.nightAmbient
        );
    }

    private Color LerpByTime(Color morning, Color noon, Color evening, Color night)
    {
        if (currentTime >= 5f && currentTime < 9f)
            return Color.Lerp(morning, noon, Mathf.InverseLerp(5f, 9f, currentTime));
        else if (currentTime >= 9f && currentTime < 17f)
            return Color.Lerp(noon, evening, Mathf.InverseLerp(9f, 17f, currentTime));
        else if (currentTime >= 17f && currentTime < 20f)
            return Color.Lerp(evening, night, Mathf.InverseLerp(17f, 20f, currentTime));
        else
        {
            float t = currentTime < 5f
                ? Mathf.InverseLerp(0f, 5f, currentTime)
                : Mathf.InverseLerp(20f, 24f, currentTime);
            return Color.Lerp(night, morning, t);
        }
    }
}
