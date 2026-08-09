using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.UI;
using TMPro;

[CustomEditor(typeof(Button), true)]
[CanEditMultipleObjects]
public class ButtonColorSyncEditor : ButtonEditor
{
    private Button _button;
    private int _lastStateIndex = -1;

    protected override void OnEnable()
    {
        base.OnEnable();
        _button = (Button)target;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (!EditorApplication.isPlaying || _button == null) return;

        // 💡 どちらか片方でもカウントダウン系が貼られていれば、エディタ色同期をスキップ

        if (_button.GetComponent<ButtonBlinkHover>() != null ||
            _button.GetComponent<ButtonTimeOutLock>() != null)
        {
            return;
        }


        var currentStateProperty = typeof(Selectable).GetProperty("currentSelectionState",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (currentStateProperty == null) return;

        int stateIndex = (int)currentStateProperty.GetValue(_button);

        if (stateIndex != _lastStateIndex)
        {
            _lastStateIndex = stateIndex;
            SyncTextColor(stateIndex);
        }
    }

    private void SyncTextColor(int stateIndex)
    {
        var texts = _button.GetComponentsInChildren<TextMeshProUGUI>(true);
        var oldTexts = _button.GetComponentsInChildren<Text>(true);

        if (texts.Length == 0 && oldTexts.Length == 0) return;

        ColorBlock colors = _button.colors;

        Color targetColor = stateIndex switch
        {
            0 => colors.normalColor,
            1 => colors.highlightedColor,
            2 => colors.pressedColor,
            3 => colors.selectedColor,
            4 => colors.disabledColor,
            _ => Color.white
        };

        foreach (var txt in texts)
        {
            if (txt != null) txt.CrossFadeColor(targetColor, colors.fadeDuration, true, true);
        }

        foreach (var oldTxt in oldTexts)
        {
            if (oldTxt != null) oldTxt.CrossFadeColor(targetColor, colors.fadeDuration, true, true);
        }
    }
}
