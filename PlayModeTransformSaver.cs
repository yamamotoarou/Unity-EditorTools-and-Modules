using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PlayModeTransformSaver : EditorWindow
{
    private static List<SavedObjectData> savedList = new List<SavedObjectData>();

    [MenuItem("Tools/Save Multiple Transforms and Stop %_S")]
    public static void SaveAndStop()
    {
        GameObject[] targetObjects = Selection.gameObjects;

        if (targetObjects == null || targetObjects.Length == 0)
        {
            Debug.LogWarning("対象のオブジェクトを1つ以上選択してください。");
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Playモード中のみ有効な機能です。");
            return;
        }

        savedList.Clear();

        foreach (var obj in targetObjects)
        {
            // Unity内部の一意なID（InstanceID）を取得
            int instanceID = obj.GetInstanceID();
            string json = "";

            var rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                json = JsonUtility.ToJson(new RectData(rectTransform));
            }
            else
            {
                json = JsonUtility.ToJson(new TransformData(obj.transform));
            }

            savedList.Add(new SavedObjectData(instanceID, json, rectTransform != null));
        }

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        EditorApplication.isPlaying = false;
        Debug.Log($"[{savedList.Count}個] のオブジェクトの位置を記憶してPlayモードを停止します...");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            if (savedList == null || savedList.Count == 0) return;

            int successCount = 0;

            foreach (var savedData in savedList)
            {
                // === 【最強の改善】内部IDから非アクティブ関係なくオブジェクトを直接一発で引っ張り出す ===
                GameObject objInEditor = EditorUtility.InstanceIDToObject(savedData.targetInstanceID) as GameObject;

                if (objInEditor != null)
                {
                    Undo.RecordObject(objInEditor.transform, "Restore Multiple Transforms");

                    if (savedData.isRectTransform)
                    {
                        var rectTrans = objInEditor.GetComponent<RectTransform>();
                        if (rectTrans != null)
                        {
                            Undo.RecordObject(rectTrans, "Restore RectTransform");
                            var data = JsonUtility.FromJson<RectData>(savedData.jsonData);
                            data.ApplyTo(rectTrans);
                        }
                    }
                    else
                    {
                        var data = JsonUtility.FromJson<TransformData>(savedData.jsonData);
                        data.ApplyTo(objInEditor.transform);
                    }

                    EditorUtility.SetDirty(objInEditor);
                    successCount++;
                }
            }

            Debug.Log($"<color=cyan>【成功】{successCount}個のオブジェクト（Text/UI含む）のRectTransform位置を完全に復元しました！</color>");
            savedList.Clear();
        }
    }

    [System.Serializable]
    public struct SavedObjectData
    {
        public int targetInstanceID; // パス文字列の代わりにInstanceIDで強固に管理
        public string jsonData;
        public bool isRectTransform;
        public SavedObjectData(int id, string json, bool isRect) { targetInstanceID = id; jsonData = json; isRectTransform = isRect; }
    }

    [System.Serializable]
    public struct TransformData
    {
        public Vector3 pos; public Quaternion rot; public Vector3 scale;
        public TransformData(Transform t) { pos = t.localPosition; rot = t.localRotation; scale = t.localScale; }
        public void ApplyTo(Transform t) { t.localPosition = pos; t.localRotation = rot; t.localScale = scale; }
    }

    [System.Serializable]
    public struct RectData
    {
        public Vector2 anchoredPosition; public Vector3 localPosition; public Vector2 sizeDelta;
        public Vector2 anchorMin; public Vector2 anchorMax; public Vector2 pivot; public Vector3 localScale;
        public RectData(RectTransform r)
        {
            anchoredPosition = r.anchoredPosition; localPosition = r.localPosition; sizeDelta = r.sizeDelta;
            anchorMin = r.anchorMin; anchorMax = r.anchorMax; pivot = r.pivot; localScale = r.localScale;
        }
        public void ApplyTo(RectTransform r)
        {
            r.anchoredPosition = anchoredPosition; r.localPosition = localPosition; r.sizeDelta = sizeDelta;
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot; r.localScale = localScale;
        }
    }
}
