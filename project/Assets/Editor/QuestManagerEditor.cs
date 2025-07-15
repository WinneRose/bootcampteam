using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(QuestManager))]
public class QuestManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        QuestManager questManager = (QuestManager)target;

        // Draw default inspector
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quest Manager Info", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            var activeQuests = questManager.GetActiveQuests();
            
            EditorGUILayout.LabelField($"Active Quests: {activeQuests.Count}");
            
            if (activeQuests.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Quest Details:", EditorStyles.boldLabel);
                
                foreach (var quest in activeQuests)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"Name: {quest.GetQuestTitle()}");
                    EditorGUILayout.LabelField($"Progress: {quest.GetProgressText()}");
                    EditorGUILayout.LabelField($"Completed: {quest.IsCompleted()}");
                    EditorGUILayout.LabelField($"Failed: {quest.IsFailed()}");
                    EditorGUILayout.EndVertical();
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Quest information will appear here during runtime.", MessageType.Info);
        }

        // Force repaint during play mode to show live updates
        if (Application.isPlaying)
        {
            Repaint();
        }
    }
}