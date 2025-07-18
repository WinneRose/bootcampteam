#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DewAbilitySystem))]
public class DewAbilitySystemEditor : Editor
{
    private DewAbilitySystem dewSystem;
    private bool showRuntimeInfo = true;
    
    private void OnEnable()
    {
        dewSystem = (DewAbilitySystem)target;
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
        showRuntimeInfo = EditorGUILayout.BeginFoldoutHeaderGroup(showRuntimeInfo, "💧 Dew Water System Status");
        if (showRuntimeInfo)
        {
            EditorGUILayout.BeginVertical("box");
            
            // Water Capacity Bar
            EditorGUILayout.LabelField("Water Capacity", EditorStyles.boldLabel);
            float currentWater = dewSystem.GetCurrentWaterCapacity();
            float maxWater = dewSystem.GetMaxWaterCapacity();
            float waterPercent = maxWater > 0 ? currentWater / maxWater : 0f;
            
            Color waterBarColor = GetWaterBarColor(waterPercent);
            DrawProgressBar(currentWater, maxWater, $"{currentWater:F1} / {maxWater:F0} Water", waterBarColor);
            
            EditorGUILayout.Space(5);
            
            // Water Zone & Charging Status
            EditorGUILayout.LabelField("Water Zone & Charging", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("In Water Zone:", dewSystem.IsInWaterZone() ? "✅ Yes" : "❌ No");
            EditorGUILayout.LabelField("Charging Water:", dewSystem.IsCharging() ? "⚡ Active" : "💤 Inactive");
            
            // Collection Rate Info
            if (dewSystem.IsCharging() && dewSystem.IsInWaterZone())
            {
                EditorGUILayout.LabelField("Collection Rate:", $"{dewSystem.waterCollectRate:F1} water/sec");
            }
            
            EditorGUILayout.Space(5);
            
            // Water Sources Information
            EditorGUILayout.LabelField("Water Sources", EditorStyles.boldLabel);
            GameObject[] waterSources = GameObject.FindGameObjectsWithTag(dewSystem.waterTag);
            EditorGUILayout.LabelField("Water Objects Found:", $"{waterSources.Length}");
            
            float nearestDistance = dewSystem.GetNearestWaterDistance();
            GameObject nearestSource = dewSystem.GetNearestWaterSource();
            
            if (nearestDistance != -1f && nearestSource != null)
            {
                bool inCollectionRange = nearestDistance <= dewSystem.waterCollectionRange;
                string distanceStatus = inCollectionRange ? 
                    $"🟢 {nearestDistance:F1}m (In Collection Range!)" : 
                    $"🔴 {nearestDistance:F1}m (Out of Range - Need ≤{dewSystem.waterCollectionRange}m)";
                EditorGUILayout.LabelField("Nearest Water:", distanceStatus);
                EditorGUILayout.LabelField("Water Source:", nearestSource.name);
                
                // Show detection range info
                bool inDetectionRange = nearestDistance <= dewSystem.maxWaterDetectionRange;
                EditorGUILayout.LabelField("Detection Range:", inDetectionRange ? 
                    $"✅ Within {dewSystem.maxWaterDetectionRange}m" : 
                    $"❌ Beyond {dewSystem.maxWaterDetectionRange}m");
            }
            else
            {
                EditorGUILayout.LabelField("Nearest Water:", $"❌ None within {dewSystem.maxWaterDetectionRange}m detection range");
            }
            
            EditorGUILayout.Space(5);
            
            // Ability Usage Status
            EditorGUILayout.LabelField("Projectile Ability", EditorStyles.boldLabel);
            bool canUse = dewSystem.CanUseAbility();
            EditorGUILayout.LabelField("Can Use Ability:", canUse ? "✅ Ready" : "❌ Not Enough Water");
            EditorGUILayout.LabelField("Water Cost per Shot:", $"{dewSystem.waterCostPerShot:F0} water");
            
            if (canUse)
            {
                int shotsAvailable = Mathf.FloorToInt(currentWater / dewSystem.waterCostPerShot);
                EditorGUILayout.LabelField("Shots Available:", $"{shotsAvailable}");
            }
            else
            {
                float waterNeeded = dewSystem.waterCostPerShot - currentWater;
                EditorGUILayout.LabelField("Water Needed:", $"{waterNeeded:F1} more");
            }
            
            EditorGUILayout.Space(5);
            
            // Projectile Settings
            EditorGUILayout.LabelField("Projectile Settings", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Speed:", $"{dewSystem.projectileSpeed} units/sec");
            EditorGUILayout.LabelField("Lifetime:", $"{dewSystem.projectileLifetime} seconds");
            EditorGUILayout.LabelField("Prefab:", dewSystem.projectilePrefab != null ? dewSystem.projectilePrefab.name : "❌ Missing");
            EditorGUILayout.LabelField("Spawn Point:", dewSystem.projectileSpawnPoint != null ? "✅ Set" : "❌ Missing");
            
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        EditorGUILayout.Space(5);
        
        // Quick Actions Section
        if (Application.isPlaying)
        {
            // Check if we can access IsOwner (might not be available in editor)
            bool canUseActions = true;
            try
            {
                canUseActions = dewSystem.IsOwner;
            }
            catch
            {
                // IsOwner might not be available in editor, allow actions anyway for testing
                canUseActions = true;
            }

            if (canUseActions)
            {
                EditorGUILayout.LabelField("🎮 Quick Actions", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("💧 Add 10 Water"))
                {
                    dewSystem.AddWater(10f);
                }
                
                if (GUILayout.Button("🌊 Fill Tank"))
                {
                    dewSystem.SetWaterCapacity(dewSystem.maxWaterCapacity);
                }
                
                if (GUILayout.Button("🗑️ Empty Tank"))
                {
                    dewSystem.SetWaterCapacity(0f);
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button(dewSystem.IsCharging() ? "⏹️ Stop Charging" : "⚡ Start Charging"))
                {
                    if (dewSystem.IsCharging())
                        dewSystem.StopCharging();
                    else
                        dewSystem.StartCharging();
                }
                
                if (GUILayout.Button("💥 Fire Projectile"))
                {
                    dewSystem.UseAbility();
                }
                
                EditorGUILayout.EndHorizontal();
                
                // Debug info button
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🔍 Debug Water Sources"))
                {
                    WaterSourceInfo[] waterInfos = dewSystem.GetAllWaterSourcesInfo();
                    Debug.Log($"=== Water Sources Debug for {dewSystem.name} ===");
                    Debug.Log($"Player Position: {dewSystem.transform.position}");
                    Debug.Log($"Collection Range: {dewSystem.waterCollectionRange}m");
                    Debug.Log($"Detection Range: {dewSystem.maxWaterDetectionRange}m");
                    Debug.Log($"Found {waterInfos.Length} water sources:");
                    
                    for (int i = 0; i < waterInfos.Length; i++)
                    {
                        var info = waterInfos[i];
                        string status = info.inCollectionRange ? "IN RANGE" : "OUT OF RANGE";
                        Debug.Log($"{i + 1}. {info.name} - {info.distance:F1}m ({status})");
                    }
                }
                
                if (GUILayout.Button("📊 Debug UI State"))
                {
                    if (dewSystem.abilityUI != null)
                    {
                        dewSystem.abilityUI.DebugDisplayUIState();
                    }
                    else
                    {
                        Debug.Log("No AbilityUI attached to DewAbilitySystem");
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        // Water Sources Debug Section
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("🔍 Water Sources Debug", EditorStyles.boldLabel);
            
            WaterSourceInfo[] waterInfos = dewSystem.GetAllWaterSourcesInfo();
            
            if (waterInfos.Length == 0)
            {
                EditorGUILayout.HelpBox($"No objects found with tag '{dewSystem.waterTag}'. Make sure your water objects have the correct tag!", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Collection Range: {dewSystem.waterCollectionRange}m | Detection Range: {dewSystem.maxWaterDetectionRange}m");
                EditorGUILayout.Space(3);
                
                for (int i = 0; i < Mathf.Min(waterInfos.Length, 5); i++)
                {
                    var info = waterInfos[i];
                    string status = info.inCollectionRange ? "🟢" : (info.inDetectionRange ? "🟡" : "🔴");
                    string rangeText = info.inCollectionRange ? "IN RANGE" : "OUT OF RANGE";
                    EditorGUILayout.LabelField($"{status} {info.name}", $"{info.distance:F1}m ({rangeText})");
                }
                
                if (waterInfos.Length > 5)
                {
                    EditorGUILayout.LabelField($"... and {waterInfos.Length - 5} more water sources");
                }
                EditorGUILayout.EndVertical();
            }
        }
        
        // Force repaint to update in real-time
        if (Application.isPlaying)
        {
            Repaint();
        }
    }
    
    private Color GetWaterBarColor(float percent)
    {
        if (percent >= 0.8f) return Color.cyan;      // Full water - cyan
        if (percent >= 0.5f) return Color.blue;      // Good water - blue
        if (percent >= 0.2f) return Color.yellow;    // Low water - yellow
        return Color.red;                             // Very low water - red
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
}
#endif