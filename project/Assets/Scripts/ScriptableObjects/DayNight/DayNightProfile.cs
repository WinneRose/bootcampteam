using UnityEngine;

[CreateAssetMenu(fileName = "NewDayNightProfile", menuName = "Environment/DayNight Profile")]
public class DayNightProfile : ScriptableObject
{
    [Header("Sun Colors")]
    public Color morningSunColor;
    public Color noonSunColor;
    public Color eveningSunColor;
    public Color nightSunColor;
    public AnimationCurve sunIntensity;

    [Header("Sky Gradient Top (MainColor)")]
    public Color morningSkyTop;
    public Color noonSkyTop;
    public Color eveningSkyTop;
    public Color nightSkyTop;

    [Header("Sky Gradient Bottom (SecondColor)")]
    public Color morningSkyBottom;
    public Color noonSkyBottom;
    public Color eveningSkyBottom;
    public Color nightSkyBottom;

    [Header("Ambient Light")]
    public Color morningAmbient;
    public Color noonAmbient;
    public Color eveningAmbient;
    public Color nightAmbient;

    [Header("Time Settings")]
    public float dayLengthInMinutes = 2f;

    [Header("Star Settings")]
    public AnimationCurve starVisibility;
    public float starsDensity = 10f;
    
    [Header("Shader Settings")]
    public float skyboxHeight = 10f;
    public Vector2 skyboxTiling = new Vector2(8, 4);
    
}