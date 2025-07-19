using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PlantGrowthManager))]
public class PlantGrowthManagerEditor : Editor
{
    private PlantGrowthManager plant;

    private void OnEnable()
    {
        plant = (PlantGrowthManager)target;
    }

    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        // Status Panel
        DrawStatusPanel();
        
        EditorGUILayout.Space(10);

        // Debug Controls
        if (Application.isPlaying)
        {
            DrawDebugControls();
        }
        else
        {
            EditorGUILayout.HelpBox("Debug controls available during play mode", MessageType.Info);
        }

        EditorGUILayout.Space(10);

        // Manual Hit Testing
        if (Application.isPlaying)
        {
            DrawManualHitTesting();
        }

        EditorGUILayout.Space(10);

        // Growth Requirements Info
        DrawGrowthRequirements();

        // Force repaint to update values in real-time
        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private void DrawStatusPanel()
    {
        EditorGUILayout.LabelField("Plant Status", EditorStyles.boldLabel);
        
        // Create a box for the status
        EditorGUILayout.BeginVertical("box");
        
        EditorGUI.BeginDisabledGroup(true);

        // Growth info
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Growth Phase:", GUILayout.Width(90));
        EditorGUILayout.LabelField($"{plant.CurrentPhase}", EditorStyles.boldLabel, GUILayout.Width(30));
        EditorGUILayout.LabelField($"Water Count:", GUILayout.Width(80));
        EditorGUILayout.LabelField($"{plant.WaterCount}", EditorStyles.boldLabel, GUILayout.Width(30));
        EditorGUILayout.LabelField($"Solar Count:", GUILayout.Width(80));
        EditorGUILayout.LabelField($"{plant.SolarCount}", EditorStyles.boldLabel, GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();

        // Status checkboxes
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.Toggle("Has Water", plant.HasWater);
        EditorGUILayout.Toggle("Has Solar", plant.HasSolar);
        EditorGUILayout.EndHorizontal();

        // Network status
        if (Application.isPlaying)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Network Role:", GUILayout.Width(90));
            string role = plant.IsServer ? "SERVER" : "CLIENT";
            EditorGUILayout.LabelField(role, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndVertical();
    }

    private void DrawDebugControls()
    {
        EditorGUILayout.LabelField("Debug Controls", EditorStyles.boldLabel);
        
        if (!plant.IsServer)
        {
            EditorGUILayout.HelpBox("⚠️ Only server can use debug controls", MessageType.Warning);
            EditorGUI.BeginDisabledGroup(true);
        }

        EditorGUILayout.BeginVertical("box");

        // Growth/Wither testing
        EditorGUILayout.LabelField("Growth Testing", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("🌱 Test Growth", GUILayout.Height(30)))
        {
            plant.TestGrowth();
        }
        
        if (GUILayout.Button("💀 Test Wither", GUILayout.Height(30)))
        {
            plant.TestWither();
        }
        
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // State manipulation
        EditorGUILayout.LabelField("State Control", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("🔄 Reset Plant", GUILayout.Height(25)))
        {
            plant.ResetPlant();
        }
        
        if (GUILayout.Button("🌳 Max Growth", GUILayout.Height(25)))
        {
            plant.MaxGrowth();
        }
        
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        if (!plant.IsServer)
        {
            EditorGUI.EndDisabledGroup();
        }
    }

    private void DrawManualHitTesting()
    {
        EditorGUILayout.LabelField("Manual Hit Testing", EditorStyles.boldLabel);
        
        if (!plant.IsServer)
        {
            EditorGUILayout.HelpBox("⚠️ Only server can add hits manually", MessageType.Warning);
            EditorGUI.BeginDisabledGroup(true);
        }

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Add Individual Hits", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        
        // Water hit button with emoji and color
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("💧 Add Water Hit", GUILayout.Height(25)))
        {
            plant.AddWaterHit();
        }
        GUI.backgroundColor = originalColor;
        
        // Solar hit button with emoji and color
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("☀️ Add Solar Hit", GUILayout.Height(25)))
        {
            plant.AddSolarHit();
        }
        GUI.backgroundColor = originalColor;
        
        EditorGUILayout.EndHorizontal();

        // Show what would happen next
        EditorGUILayout.Space(3);
        string prediction = GetGrowthPrediction();
        if (!string.IsNullOrEmpty(prediction))
        {
            EditorGUILayout.HelpBox($"Next action: {prediction}", MessageType.Info);
        }

        EditorGUILayout.EndVertical();

        if (!plant.IsServer)
        {
            EditorGUI.EndDisabledGroup();
        }
    }

    private void DrawGrowthRequirements()
    {
        EditorGUILayout.LabelField("Growth Requirements", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.LabelField("📈 Growth Conditions:", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("• Exactly 1 Water + 1 Solar = Growth (any order)", EditorStyles.helpBox);
        EditorGUILayout.LabelField("• Example: Water→Solar OR Solar→Water", EditorStyles.helpBox);
        
        EditorGUILayout.Space(3);
        
        EditorGUILayout.LabelField("📉 Wither Conditions:", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("• More than 1 Water OR more than 1 Solar = Wither", EditorStyles.helpBox);
        
        EditorGUILayout.Space(3);
        
        EditorGUILayout.LabelField("⏰ Timeout:", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("• Incomplete combinations reset after 10 seconds", EditorStyles.helpBox);
        
        EditorGUILayout.EndVertical();
    }

    private string GetGrowthPrediction()
    {
        if (!Application.isPlaying) return "";
        
        int water = plant.WaterCount;
        int solar = plant.SolarCount;
        
        if (water == 0 && solar == 0)
            return "Need any projectile to start";
        
        if (water == 1 && solar == 0)
            return "Need 1 Solar for Growth";
        
        if (water == 0 && solar == 1)
            return "Need 1 Water for Growth";
        
        if (water == 1 && solar == 1)
            return "Ready for Growth!";
        
        if (water > 1 || solar > 1)
            return "Will Wither on next check";
        
        return "Unknown state";
    }
}