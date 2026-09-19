using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =============================================================================
// PlayModeTransformSaver (新規オブジェクト持ち帰り対応版)
//
// Playモード中に選択したオブジェクトのTransform数値を保存し、Editモード復帰時に復元。
// 【追加機能】Playモード中に新規作成されたオブジェクト（背景・UI）も、
// 一時プレハブを経由してEditモードへ持ち帰ります。
//
// 【事故防止対策（適当ではなくガチ）】
// 1. 階層重複防止: 親子で選択してしまった場合、親だけを保存して二重生成を防止。
// 2. ゴミ掃除の徹底: どんなエラーが起きても try-finally で一時ファイルを完全消去。
// 3. 参照切れ警告: 新規オブジェクト持ち帰り時、コンソールに警告を出し、名前に [Restored] を付与。
// =============================================================================

public class PlayModeTransformSaver
{
    [System.Serializable]
    private struct TransformBackup
    {
        public int instanceID;
        public string objectName;

        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale;

        public bool isRectTransform;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector3 anchoredPosition3D;
        public Vector2 sizeDelta;
        public Vector2 offsetMin;
        public Vector2 offsetMax;

        // 【新規】一時プレハブの保存パス
        public string tempPrefabPath;
    }

    [System.Serializable]
    private class TransformBackupList
    {
        public List<TransformBackup> items = new List<TransformBackup>();
    }

    private const string SessionKey_BackupJson = "PlayModeTransformSaver_BackupJson";
    private const string TempFolderPath = "Assets/TempPlayModeBackups"; // ゴミ箱用フォルダ

    [MenuItem("Tools/Save Transforms and Stop %_S")]
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

        // 【事故防止1】親子階層の重複保存を防ぐ（子が親と一緒に選択されている場合は子を除外）
        List<GameObject> rootTargets = new List<GameObject>();
        foreach (GameObject obj in targetObjects)
        {
            bool hasSelectedParent = false;
            Transform current = obj.transform.parent;
            while (current != null)
            {
                if (System.Array.IndexOf(targetObjects, current.gameObject) >= 0)
                {
                    hasSelectedParent = true;
                    break;
                }
                current = current.parent;
            }
            if (!hasSelectedParent) rootTargets.Add(obj);
        }

        // 一時フォルダの作成
        if (!AssetDatabase.IsValidFolder(TempFolderPath))
        {
            AssetDatabase.CreateFolder("Assets", "TempPlayModeBackups");
        }

        List<TransformBackup> newBackupList = new List<TransformBackup>();

        foreach (GameObject obj in rootTargets)
        {
            if (obj == null) continue;

            Transform t = obj.transform;
            string tempPath = $"{TempFolderPath}/Backup_{obj.GetInstanceID()}.prefab";

            // 既存・新規問わず、保険として全て一時プレハブとして物理保存しておく
            PrefabUtility.SaveAsPrefabAsset(obj, tempPath);

            TransformBackup data = new TransformBackup
            {
                instanceID = obj.GetInstanceID(),
                objectName = obj.name,
                localPosition = t.localPosition,
                localEulerAngles = t.localEulerAngles,
                localScale = t.localScale,
                tempPrefabPath = tempPath
            };

            RectTransform rect = t as RectTransform;
            if (rect != null)
            {
                data.isRectTransform = true;
                data.anchorMin = rect.anchorMin;
                data.anchorMax = rect.anchorMax;
                data.pivot = rect.pivot;
                data.anchoredPosition3D = rect.anchoredPosition3D;
                data.sizeDelta = rect.sizeDelta;
                data.offsetMin = rect.offsetMin;
                data.offsetMax = rect.offsetMax;
            }

            newBackupList.Add(data);
        }

        TransformBackupList wrapper = new TransformBackupList { items = newBackupList };
        SessionState.SetString(SessionKey_BackupJson, JsonUtility.ToJson(wrapper));

        EditorApplication.isPlaying = false;

        Debug.Log($"[{newBackupList.Count}個] のオブジェクトを一時退避し、Playモードを停止します...");
    }

    [InitializeOnLoadMethod]
    private static void RegisterCallback()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        string json = SessionState.GetString(SessionKey_BackupJson, "");
        if (string.IsNullOrEmpty(json))
            return;

        SessionState.EraseString(SessionKey_BackupJson);

        TransformBackupList wrapper = JsonUtility.FromJson<TransformBackupList>(json);
        List<TransformBackup> backupList = wrapper != null ? wrapper.items : null;

        if (backupList == null || backupList.Count == 0)
            return;

        int existingCount = 0;
        int newObjectCount = 0;
        List<GameObject> restoredObjects = new List<GameObject>();

        Undo.SetCurrentGroupName("Restore PlayMode Objects");
        int undoGroup = Undo.GetCurrentGroup();

        // 【事故防止2】try-finallyでどんなエラーが起きても必ず一時ファイル（ゴミ）を消去する
        try
        {
            foreach (TransformBackup backup in backupList)
            {
                GameObject targetObj = EditorUtility.InstanceIDToObject(backup.instanceID) as GameObject;

                if (targetObj != null)
                {
                    // パターンA: 既存オブジェクト（Playモード前からシーンに存在していたもの）
                    Undo.RecordObject(targetObj.transform, "Restore PlayMode Transform");
                    ApplyTransform(targetObj.transform, backup);

                    if (PrefabUtility.IsPartOfPrefabInstance(targetObj))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(targetObj.transform);
                    }
                    EditorUtility.SetDirty(targetObj.transform);
                    restoredObjects.Add(targetObj);
                    existingCount++;
                }
                else
                {
                    // パターンB: 新規オブジェクト（Playモード中に作られたもの）
                    GameObject tempPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(backup.tempPrefabPath);
                    if (tempPrefab != null)
                    {
                        // プレハブからシーンへ実体化
                        GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(tempPrefab);

                        // プレハブとのリンクを完全に切断して独立オブジェクトにする
                        PrefabUtility.UnpackPrefabInstance(newObj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        Undo.RegisterCreatedObjectUndo(newObj, "Restore New PlayMode Object");

                        ApplyTransform(newObj.transform, backup);

                        // 【事故防止3】持ち帰った新規オブジェクトであることが一目でわかるように命名
                        newObj.name = backup.objectName + " [Restored]";

                        restoredObjects.Add(newObj);
                        newObjectCount++;
                    }
                }

                if (restoredObjects.Count > 0 && restoredObjects[restoredObjects.Count - 1].scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(restoredObjects[restoredObjects.Count - 1].scene);
                }
            }
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);

            // 一時フォルダを中身ごと完全に削除
            if (AssetDatabase.IsValidFolder(TempFolderPath))
            {
                AssetDatabase.DeleteAsset(TempFolderPath);
            }
            AssetDatabase.Refresh();
        }

        if (restoredObjects.Count > 0)
        {
            Selection.objects = restoredObjects.ToArray();
        }

        SceneView.RepaintAll();

        Debug.Log($"<color=lime>【復元完了】既存のTransform更新: {existingCount}個 / 新規オブジェクトの持ち帰り: {newObjectCount}個</color>");

        if (newObjectCount > 0)
        {
            Debug.LogWarning("<b>【注意】</b>Playモード中に作成されたオブジェクトを持ち帰りました。\n他のシーンオブジェクト（カメラやプレイヤー等）へのスクリプト参照が切れている可能性があるため、インスペクターを確認してください。");
        }
    }

    private static void ApplyTransform(Transform t, TransformBackup backup)
    {
        RectTransform rect = t as RectTransform;

        if (backup.isRectTransform && rect != null)
        {
            rect.localScale = backup.localScale;
            rect.localEulerAngles = backup.localEulerAngles;
            rect.anchorMin = backup.anchorMin;
            rect.anchorMax = backup.anchorMax;
            rect.pivot = backup.pivot;
            rect.anchoredPosition3D = backup.anchoredPosition3D;
            rect.sizeDelta = backup.sizeDelta;

            if (!Mathf.Approximately(backup.anchorMin.x, backup.anchorMax.x))
            {
                Vector2 min = rect.offsetMin; min.x = backup.offsetMin.x; rect.offsetMin = min;
                Vector2 max = rect.offsetMax; max.x = backup.offsetMax.x; rect.offsetMax = max;
            }
            if (!Mathf.Approximately(backup.anchorMin.y, backup.anchorMax.y))
            {
                Vector2 min = rect.offsetMin; min.y = backup.offsetMin.y; rect.offsetMin = min;
                Vector2 max = rect.offsetMax; max.y = backup.offsetMax.y; rect.offsetMax = max;
            }
            Vector3 finalPos = rect.anchoredPosition3D;
            finalPos.z = backup.anchoredPosition3D.z;
            rect.anchoredPosition3D = finalPos;
        }
        else
        {
            t.localPosition = backup.localPosition;
            t.localEulerAngles = backup.localEulerAngles;
            t.localScale = backup.localScale;
        }
    }
}