using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Animator))]
public sealed class AnimatorInspector : Editor
{
    private static readonly Type BASE_EDITOR_TYPE = typeof(Editor)
        .Assembly
        .GetType("UnityEditor.AnimatorInspector");

    public override void OnInspectorGUI()
    {
        var animator = (Animator)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Animator Window"))
            {
                EditorApplication.ExecuteMenuItem("Window/Animation/Animator");
            }
            if (GUILayout.Button("Open Animation Window"))
            {
                EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
            }


        }

        var editor = CreateEditor(animator, BASE_EDITOR_TYPE);

        editor.OnInspectorGUI();
    }
}