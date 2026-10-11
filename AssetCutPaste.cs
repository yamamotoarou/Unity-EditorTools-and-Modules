#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【新規】Project ウィンドウで、アセットの「切り取り」と「貼り付け」をできるようにするエディタ拡張。
///
/// 置き場所：Assets の下の「Editor」という名前のフォルダ（無ければ作る）。ゲームのビルドには含まれない。
///
/// 使い方：
///   1. 動かしたいアセットやフォルダを選んで、右クリック →「切り取り」（Alt + X）
///      切り取った物は、貼り付けるまで薄く表示される
///   2. 移動先のフォルダを選ぶ（または、移動先のフォルダを開いて何も無い所を右クリックする）
///   3. 右クリック →「貼り付け」（Alt + V）
///   やめる時は、右クリック →「切り取りをやめる」
///
/// 中でやっていること：
///   Unity の正式な移動（AssetDatabase.MoveAsset）を使う。ドラッグで移動するのと同じ処理なので、
///   .meta ファイルも一緒に動き、シーンやプレハブからの参照は切れない。
///   エクスプローラーで切り取って貼り付けるのとは違う（そちらは .meta を置き忘れると参照が切れる）。
///
/// 注意：
///   ・アセットの移動は、Unity の「元に戻す」（Ctrl + Z）では戻せない。戻す時は、もう一度切り取って元のフォルダへ貼る
///   ・Packages の中の物は動かせない（Assets の中だけ）
/// </summary>
[InitializeOnLoad]
public static class AssetCutPaste
{
    // 右クリックメニューでの並び順（「Copy Path」のすぐ下に出す）
    const int MenuPriority = 20;

    const string MenuCut = "Assets/切り取り &x";
    const string MenuPaste = "Assets/貼り付け &v";
    const string MenuCancel = "Assets/切り取りをやめる";

    // スクリプトを書き換えて再コンパイルされても、切り取った物を忘れないように、ここへ保存する
    const string SessionKey = "AssetCutPaste.CutGuids";

    static readonly HashSet<string> cutGuids = new HashSet<string>();

    static AssetCutPaste()
    {
        LoadCutList();
        EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
    }

    // =========================================================
    // 切り取り
    // =========================================================

    [MenuItem(MenuCut, false, MenuPriority)]
    static void Cut()
    {
        cutGuids.Clear();

        // 【修正】フォルダと、そのフォルダの中の物を一緒に選んだ時は、フォルダだけを切り取る。
        // 旧：両方とも切り取りの対象に入れていた。貼り付ける時に、
        //     フォルダを移動 → 中の物の場所を調べ直す（もう移動先のフォルダの中にいる）→ 中の物だけ、さらに外へ移動
        //     となって、中の物がフォルダの外へ出てしまっていた。
        //     HashSet は順番が決まっていないので、起きたり起きなかったりする分かりにくい不具合だった。
        // フォルダを動かせば中身は一緒に付いてくるので、中の物を別に覚える必要は無い
        foreach (string guid in RemoveItemsInsideSelectedFolders(Selection.assetGUIDs))
        {
            cutGuids.Add(guid);
        }

        SaveCutList();
        EditorApplication.RepaintProjectWindow();

        if (cutGuids.Count > 0)
        {
            Debug.Log("[切り取り] " + cutGuids.Count + " 個を切り取りました。移動先のフォルダを選んで「貼り付け」（Alt + V）してください。");
        }
    }

    [MenuItem(MenuCut, true)]
    static bool CutValidate()
    {
        foreach (string guid in Selection.assetGUIDs)
        {
            if (IsMovablePath(AssetDatabase.GUIDToAssetPath(guid))) return true;
        }

        return false;
    }

    // =========================================================
    // 貼り付け
    // =========================================================

    [MenuItem(MenuPaste, false, MenuPriority + 1)]
    static void Paste()
    {
        string targetFolder = GetTargetFolder();

        if (string.IsNullOrEmpty(targetFolder) || !AssetDatabase.IsValidFolder(targetFolder))
        {
            EditorUtility.DisplayDialog("貼り付け", "貼り付け先のフォルダが分かりません。\n移動先のフォルダを選んでから、もう一度「貼り付け」してください。", "OK");
            return;
        }

        if (!IsMovablePath(targetFolder) && targetFolder != "Assets")
        {
            EditorUtility.DisplayDialog("貼り付け", "ここには貼り付けられません（Assets の中だけに貼り付けられます）。\n\n" + targetFolder, "OK");
            return;
        }

        int moved = 0;
        int skipped = 0;
        List<string> errors = new List<string>();
        List<string> movedGuids = new List<string>();

        // 【修正】移動できなかった物は、切り取ったままにしておく（直してから、もう一度貼り付けられるように）
        List<string> failedGuids = new List<string>();

        // 【修正】貼り付ける直前にも、もう一度「フォルダの中の物」を外す。
        // 切り取った後でフォルダの中へ物が移された場合や、古い版で切り取ったままの物が残っていた場合の保険。
        // 順番も決めておく（パスの短い順＝外側のフォルダから）ので、毎回同じ結果になる
        List<string> targets = RemoveItemsInsideSelectedFolders(new List<string>(cutGuids).ToArray());

        foreach (string guid in targets)
        {
            string source = AssetDatabase.GUIDToAssetPath(guid);

            // 切り取った後で、消されたり、外から動かされたりした物
            if (string.IsNullOrEmpty(source))
            {
                skipped++;
                continue;
            }

            // もう同じフォルダにある
            if (GetParentFolder(source) == targetFolder)
            {
                skipped++;
                continue;
            }

            // フォルダを、自分自身や自分の中へは動かせない
            if (IsSameOrInside(targetFolder, source))
            {
                errors.Add(source + " → フォルダを、そのフォルダ自身の中へは移動できません");
                failedGuids.Add(guid);
                continue;
            }

            string destination = BuildDestinationPath(source, targetFolder);

            // 同じ名前の物がもうある時は、名前の後ろに番号を付ける（上書きはしない）
            string validate = AssetDatabase.ValidateMoveAsset(source, destination);
            if (!string.IsNullOrEmpty(validate))
            {
                destination = AssetDatabase.GenerateUniqueAssetPath(destination);
            }

            string error = AssetDatabase.MoveAsset(source, destination);

            if (string.IsNullOrEmpty(error))
            {
                moved++;
                movedGuids.Add(guid);
            }
            else
            {
                errors.Add(source + " → " + error);
                failedGuids.Add(guid);
            }
        }

        // 【修正】旧：ここで全部の切り取りを解除していたので、移動に失敗した物まで解除されていた。
        // 移動できた物・何もする必要が無かった物だけを解除して、失敗した物は切り取ったまま残す
        cutGuids.Clear();

        foreach (string guid in failedGuids)
        {
            cutGuids.Add(guid);
        }

        SaveCutList();
        AssetDatabase.Refresh();
        EditorApplication.RepaintProjectWindow();

        // 動かした物を選んだ状態にする（どこへ行ったか分かるように）
        if (movedGuids.Count > 0)
        {
            List<Object> objects = new List<Object>();

            foreach (string guid in movedGuids)
            {
                Object obj = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (obj != null) objects.Add(obj);
            }

            Selection.objects = objects.ToArray();
        }

        Debug.Log("[貼り付け] " + moved + " 個を「" + targetFolder + "」へ移動しました。" +
                  (skipped > 0 ? "（" + skipped + " 個は、もう同じフォルダにあるか、見つからなかったので何もしていません）" : ""));

        if (errors.Count > 0)
        {
            string message = string.Join("\n", errors.ToArray());
            Debug.LogWarning("[貼り付け] 移動できなかった物があります（切り取ったままにしてあります）：\n" + message);
            EditorUtility.DisplayDialog("貼り付け",
                "移動できなかった物があります（" + errors.Count + " 個）。\n" +
                "それらは切り取ったままにしてあるので、別のフォルダへ貼り付け直せます。\n" +
                "やめる時は「切り取りをやめる」を選んでください。\n\n" +
                "くわしくは Console を見てください。", "OK");
        }
    }

    [MenuItem(MenuPaste, true)]
    static bool PasteValidate()
    {
        return cutGuids.Count > 0;
    }

    // =========================================================
    // 切り取りをやめる
    // =========================================================

    [MenuItem(MenuCancel, false, MenuPriority + 2)]
    static void CancelCut()
    {
        cutGuids.Clear();
        SaveCutList();
        EditorApplication.RepaintProjectWindow();
    }

    [MenuItem(MenuCancel, true)]
    static bool CancelCutValidate()
    {
        return cutGuids.Count > 0;
    }

    // =========================================================
    // 貼り付け先を決める
    // =========================================================

    /// <summary>
    /// 貼り付け先のフォルダ。
    /// フォルダを1つ選んでいる時は、そのフォルダ。アセットを選んでいる時は、それが入っているフォルダ。
    /// 何も選んでいない時は、Project ウィンドウで今開いているフォルダ
    /// </summary>
    static string GetTargetFolder()
    {
        string[] guids = Selection.assetGUIDs;

        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);

            if (!string.IsNullOrEmpty(path))
            {
                // 切り取った物そのものを選んだままの時は、「今開いているフォルダ」のほうを使う
                bool selectionIsCutItem = cutGuids.Contains(guids[0]);

                if (!selectionIsCutItem)
                {
                    if (guids.Length == 1 && AssetDatabase.IsValidFolder(path)) return path;
                    return GetParentFolder(path);
                }
            }
        }

        string active = GetActiveFolderPath();
        if (!string.IsNullOrEmpty(active)) return active;

        return "Assets";
    }

    /// <summary>Project ウィンドウで今開いているフォルダ（Unity の内部の関数を借りる。取れなければ null）</summary>
    static string GetActiveFolderPath()
    {
        try
        {
            MethodInfo method = typeof(ProjectWindowUtil).GetMethod(
                "GetActiveFolderPath",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            if (method != null) return method.Invoke(null, null) as string;
        }
        catch (System.Exception)
        {
            // Unity のバージョンで関数が無くなっていても、止まらないようにする
        }

        return null;
    }

    /// <summary>
    /// 【修正】GUID の一覧から、動かせない物と、「一緒に選ばれているフォルダの中にある物」を外す。
    /// 結果は、パスの短い順（外側のフォルダから）に並べる
    /// </summary>
    static List<string> RemoveItemsInsideSelectedFolders(string[] guids)
    {
        List<string> result = new List<string>();
        if (guids == null) return result;

        // GUID とパスの組を作る
        List<KeyValuePair<string, string>> items = new List<KeyValuePair<string, string>>();
        List<string> folderPaths = new List<string>();

        foreach (string guid in guids)
        {
            if (string.IsNullOrEmpty(guid)) continue;

            string path = AssetDatabase.GUIDToAssetPath(guid);

            // もう存在しない物は、後で「見つからなかった」と数えるために残しておく
            if (string.IsNullOrEmpty(path))
            {
                items.Add(new KeyValuePair<string, string>(guid, ""));
                continue;
            }

            if (!IsMovablePath(path)) continue;

            items.Add(new KeyValuePair<string, string>(guid, path));
            if (AssetDatabase.IsValidFolder(path)) folderPaths.Add(path);
        }

        items.Sort((a, b) =>
        {
            int byLength = a.Value.Length.CompareTo(b.Value.Length);
            return (byLength != 0) ? byLength : string.CompareOrdinal(a.Value, b.Value);
        });

        foreach (KeyValuePair<string, string> item in items)
        {
            if (item.Value.Length > 0 && IsInsideAnyFolder(item.Value, folderPaths)) continue;
            if (!result.Contains(item.Key)) result.Add(item.Key);
        }

        return result;
    }

    // =========================================================
    // パスの計算（Unity に頼らない部分）
    // =========================================================

    /// <summary>【修正】path が、folders のどれかの「中」にあるか（フォルダ自身は含めない）</summary>
    public static bool IsInsideAnyFolder(string path, List<string> folders)
    {
        if (string.IsNullOrEmpty(path) || folders == null) return false;

        foreach (string folder in folders)
        {
            if (path != folder && IsSameOrInside(path, folder)) return true;
        }

        return false;
    }

    /// <summary>動かしてよい場所か（Assets の中だけ。Assets フォルダ自身と Packages は不可）</summary>
    public static bool IsMovablePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return path.StartsWith("Assets/");
    }

    /// <summary>入っているフォルダのパス（区切りは必ず /）</summary>
    public static string GetParentFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        int index = path.LastIndexOf('/');
        return (index < 0) ? "" : path.Substring(0, index);
    }

    /// <summary>移動した後のパス（移動先のフォルダ ＋ 元のファイル名）</summary>
    public static string BuildDestinationPath(string sourcePath, string targetFolder)
    {
        string name = Path.GetFileName(sourcePath);
        return targetFolder.TrimEnd('/') + "/" + name;
    }

    /// <summary>path が、folder そのもの、または folder の中にあるか</summary>
    public static bool IsSameOrInside(string path, string folder)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder)) return false;
        if (path == folder) return true;
        return path.StartsWith(folder.TrimEnd('/') + "/");
    }

    // =========================================================
    // 切り取った物を覚えておく・薄く表示する
    // =========================================================

    static void SaveCutList()
    {
        SessionState.SetString(SessionKey, string.Join(";", new List<string>(cutGuids).ToArray()));
    }

    static void LoadCutList()
    {
        cutGuids.Clear();

        string saved = SessionState.GetString(SessionKey, "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (string guid in saved.Split(';'))
        {
            if (!string.IsNullOrEmpty(guid)) cutGuids.Add(guid);
        }
    }

    /// <summary>切り取った物を、Project ウィンドウで薄く見せる（エクスプローラーの切り取りと同じ見え方）</summary>
    static void OnProjectWindowItemGUI(string guid, Rect rect)
    {
        if (cutGuids.Count == 0 || !cutGuids.Contains(guid)) return;

        // 背景と同じ色を半透明で重ねて、薄く見せる
        Color cover = EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f, 0.6f)
            : new Color(0.78f, 0.78f, 0.78f, 0.6f);

        EditorGUI.DrawRect(rect, cover);
    }
}
#endif
