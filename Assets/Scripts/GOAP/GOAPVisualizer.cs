using UnityEngine;
using System.Collections.Generic;
using System.Text;

public class GOAPVisualizer : MonoBehaviour
{
    private VisibilityManager visibilityManager;
    
    // Style settings
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private Texture2D backgroundTexture;

    void Start()
    {
        visibilityManager = FindObjectOfType<VisibilityManager>();
        
        // Background
        backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.7f));
        backgroundTexture.Apply();
    }

    void OnGUI()
    {
        if (visibilityManager == null) return;

        // Get the agent
        GOAPAgent agent = visibilityManager.GetSpectatedEnemyAgent();
        
        // Draw if watching an enemy
        if (agent == null) return;

        // Setup Styles
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 12;
            
            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.normal.textColor = Color.yellow;
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
        }

        // Define Layout Area
        float width = 300;
        float height = 400;
        float padding = 10;
        Rect areaRect = new Rect(Screen.width - width - padding, padding, width, height);

        // Draw Background
        GUI.DrawTexture(areaRect, backgroundTexture);

        // Begin Area
        GUILayout.BeginArea(areaRect);
        GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

        // Header
        GUILayout.Label($"ENEMY BRAIN: {agent.name}", headerStyle);
        GUILayout.Space(5);

        // Vitals
        GUILayout.Label("VITALS", headerStyle);
        GUILayout.Label($"Energy: {agent.currentEnergy:F1} / {agent.maxEnergy}");
        string destStatus = (agent.lastKnownPosition == Vector3.zero) ? "None" : agent.lastKnownPosition.ToString();
        GUILayout.Label($"Memory (Player Pos): {destStatus}");
        GUILayout.Space(5);

        // State Machine
        GUILayout.Label("STATE MACHINE", headerStyle);
        GUILayout.Label($"Current State: {agent.debugStateName}");
        GUILayout.Space(5);

        // Current Goal
        GUILayout.Label("CURRENT GOAL", headerStyle);
        
        if (agent.currentGoal != null && agent.currentGoal.Count > 0)
        {
            foreach (var kvp in agent.currentGoal)
            {
                // Display the goal Key and Value
                GUILayout.Label($">> {kvp.Key}: {kvp.Value}", labelStyle);
            }
        }
        else
        {
            GUILayout.Label(">> (No Goal Set)", labelStyle);
        }
        
        GUILayout.Space(5);

        // Action Plan
        GUILayout.Label("ACTION PLAN", headerStyle);
        if (agent.currentActions != null && agent.currentActions.Count > 0)
        {
            int step = 1;
            foreach (var action in agent.currentActions)
            {
                string actionName = action.GetType().Name;
                if (step == 1) 
                    GUILayout.Label($"-> {step}. {actionName} (ACTIVE)", labelStyle);
                else
                    GUILayout.Label($"   {step}. {actionName}", labelStyle);
                step++;
            }
        }
        else
        {
            GUILayout.Label("(No Active Plan)");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}