using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class ButtonColorCopier : EditorWindow
{
    private static ButtonColorBlockHolder clipboard;

    // 1. コピー元のボタンを右クリックして色を記憶
    [MenuItem("CONTEXT/Button/「Color Tintのみ」をコピーします")]
    private static void CopyButtonColors(MenuCommand command)
    {
        Button btn = (Button)command.context;
        clipboard = new ButtonColorBlockHolder { colors = btn.colors };
        Debug.Log("ButtonのColor Tintをクリップボードに記憶しました！");
    }

    // 2. コピー先のボタンを右クリックして色だけを貼り付け
    [MenuItem("CONTEXT/Button/「Color Tint」をペーストします")]
    private static void PasteButtonColors(MenuCommand command)
    {
        if (clipboard == null) return;

        // ➔ 【修正箇所】ここを正しい型キャストに直しました！
        Button btn = (Button)command.context;

        Undo.RecordObject(btn, "Paste Button Colors"); // Ctrl+Zで戻せるようにする
        btn.colors = clipboard.colors;
    }

    private class ButtonColorBlockHolder { public ColorBlock colors; }
}
