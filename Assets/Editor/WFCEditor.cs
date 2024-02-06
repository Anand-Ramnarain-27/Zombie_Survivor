using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(WaveFunctionCollapse))]
public class WFCEditor : Editor
{

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += PlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= PlayModeChanged;
    }

    private void PlayModeChanged(PlayModeStateChange stateChange)
    {
        if (stateChange == PlayModeStateChange.ExitingEditMode)
        {
            WaveFunctionCollapse waveFunctionCollapse = (WaveFunctionCollapse)target;
            waveFunctionCollapse.InitializeWaveFunction();
        }
    }

    public override void OnInspectorGUI()
    {
        WaveFunctionCollapse waveFunctionCollapse = (WaveFunctionCollapse)target;
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate Prototypes"))
        {
            waveFunctionCollapse.InitializeWaveFunction();
        }
        if (GUILayout.Button("Clear"))
        {
            waveFunctionCollapse.ClearAll();
        }
        if (GUILayout.Button("Collapse"))
        {
            waveFunctionCollapse.StartCollapse();
        }
    }
}
