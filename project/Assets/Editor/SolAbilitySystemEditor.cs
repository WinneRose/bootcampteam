#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SolAbilitySystem))]
public class SolAbilitySystemEditor : Editor
{
    private SolAbilitySystem solSystem;
    private bool showRuntimeInfo = true;
    
    private void OnEnable()
    {
        solSystem = (SolAbilitySystem)target;
    }
    
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();
        
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Runtime information will appear when game is running", MessageType.Info);
            return;
        }
        
        EditorGUILayout.Space(10);
        
        // Runtime Information Section
        showRuntimeInfo = EditorGUILayout.BeginFoldoutHeaderGroup(showRuntimeInfo, "☀️ Sol Solar System Status");
        if (showRuntimeInfo)
        {
            EditorGUILayout.BeginVertical("box");
            
            // Solar Energy Bar
            EditorGUILayout.LabelField("Solar Energy", EditorStyles.boldLabel);
            float currentEnergy = solSystem.GetCurrentSolarEnergy();
            float maxEnergy = solSystem.GetMaxSolarEnergy();
            float energyPercent = maxEnergy > 0 ? currentEnergy / maxEnergy : 0f;
            
            Color barColor = GetEnergyBarColor(energyPercent);
            DrawProgressBar(currentEnergy, maxEnergy, $"{currentEnergy:F1} / {maxEnergy:F0} Solar Energy", barColor);
            
            EditorGUILayout.Space(5);
            
            // Time Information
            EditorGUILayout.LabelField("Time & Daylight", EditorStyles.boldLabel);
            float timeOfDay = solSystem.GetTimeOfDay();
            bool inSunlight = solSystem.GetIsInSunlight();
            
            EditorGUILayout.LabelField("Current Time:", GetTimeString(timeOfDay));
            EditorGUILayout.LabelField("Daylight Status:", GetDaylightStatus(inSunlight));
            
            EditorGUILayout.Space(5);
            
            // Solar Blast Status
            EditorGUILayout.LabelField("Solar Blast", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Can Use Blast:", solSystem.CanUseAbility() ? "✅ Ready" : "❌ Not Enough Energy");
            EditorGUILayout.LabelField("Energy Cost:", $"{solSystem.blastCost:F0} energy per blast");
            
            if (solSystem.CanUseAbility())
            {
                int blastsAvailable = Mathf.FloorToInt(currentEnergy / solSystem.blastCost);
                EditorGUILayout.LabelField("Blasts Available:", $"{blastsAvailable}");
            }
            
            EditorGUILayout.Space(5);
            
            // Charging Status
            EditorGUILayout.LabelField("Energy Generation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Manual Charging:", solSystem.IsCharging() ? "⚡ Active" : "💤 Inactive");
            
            if (inSunlight)
            {
                float totalRate = solSystem.solarGenerationRate;
                if (solSystem.IsCharging())
                {
                    totalRate += solSystem.manualChargeRate;
                }
                EditorGUILayout.LabelField("Generation Rate:", $"⚡ {totalRate:F1}/sec");
            }
            else
            {
                EditorGUILayout.LabelField("Generation Rate:", "❌ No Generation (Nighttime)");
            }
            
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        EditorGUILayout.Space(5);
        
        // Quick Actions Section
        if (Application.isPlaying && solSystem.IsOwner)
        {
            EditorGUILayout.LabelField("🎮 Quick Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("☀️ Set Noon"))
            {
                solSystem.SetTimeToNoon();
            }
            
            if (GUILayout.Button("🌙 Set Night"))
            {
                solSystem.SetTimeToNight();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("⚡ Add Energy"))
            {
                solSystem.DebugAddEnergy();
            }
            
            if (GUILayout.Button("🌞 Fill Energy"))
            {
                solSystem.SetSolarEnergy(solSystem.GetMaxSolarEnergy());
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button(solSystem.IsCharging() ? "⏹️ Stop Charging" : "⚡ Start Charging"))
            {
                if (solSystem.IsCharging())
                    solSystem.StopCharging();
                else
                    solSystem.StartCharging();
            }
            
            if (GUILayout.Button("💥 Solar Blast"))
            {
                solSystem.UseAbility();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        // Force repaint to update in real-time
        if (Application.isPlaying)
        {
            Repaint();
        }
    }
    
    private Color GetEnergyBarColor(float percent)
    {
        if (percent >= 0.8f) return Color.yellow;      // High energy - bright yellow
        if (percent >= 0.5f) return Color.orange;      // Medium energy - orange
        if (percent >= 0.2f) return Color.red;         // Low energy - red
        return Color.gray;                              // Very low energy - gray
    }
    
    private void DrawProgressBar(float current, float max, string label, Color barColor)
    {
        Rect rect = GUILayoutUtility.GetRect(18, 18, "TextField");
        EditorGUI.ProgressBar(rect, max > 0 ? current / max : 0f, label);
        
        // Custom color overlay
        if (max > 0)
        {
            Rect colorRect = new Rect(rect.x, rect.y, rect.width * (current / max), rect.height);
            EditorGUI.DrawRect(colorRect, barColor * 0.3f);
        }
    }
    
    private string GetTimeString(float timeOfDay)
    {
        int hours = Mathf.FloorToInt(timeOfDay);
        int minutes = Mathf.FloorToInt((timeOfDay - hours) * 60f);
        return $"{hours:D2}:{minutes:D2}";
    }
    
    private string GetDaylightStatus(bool inSunlight)
    {
        if (inSunlight)
        {
            return "☀️ Daytime (Generating Energy)";
        }
        else
        {
            return "🌙 Nighttime (No Energy Generation)";
        }
    }
}
#endif