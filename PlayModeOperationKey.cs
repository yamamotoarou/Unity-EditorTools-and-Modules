#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
#if UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

/// <summary>
/// 【開発用】キー1つで Play モードを操作する。
///
///   F8         … Play 開始（Play 中は何もしない）
///   F9         … 一時停止 ⇔ 再開（Play 中でなければ何もしない）
///   Shift + F9 … Play モードを終了する
///
/// ■ 仕組み（Windows）
///   エディタ側の更新（EditorApplication.update）で、キーボードの状態を直接見ている。
///   ゲーム側の Update を使わないので、次のどの状態でも同じように効く。
///     ・Game ビューでプレイ中 ・一時停止中（ゲームの Update が止まっている）
///     ・Hierarchy や Inspector を触っている時
///   Unity が一番手前の時だけ反応する（Visual Studio で F9 を押しても反応しない）。
///
/// ■ Windows 以外
///   メニュー「Tools/Playモード/～」のショートカットとして動く
///   （Game ビューにフォーカスがある時は効かないことがある）。
///
/// ■ 置き場所
///   どこでもよい（Editor フォルダでも、それ以外でも）。
///   ファイル全体が #if UNITY_EDITOR で囲まれているので、ビルド版には一切含まれない。
///
/// ■ キーを変えたい時
///   Windows … 下の VK_PLAY / VK_PAUSE（仮想キーコード。F1 = 0x70 … F12 = 0x7B）
///   それ以外 … MenuPlay / MenuPause / MenuExit の「_F8」「_F9」「#_F9」
/// </summary>
[InitializeOnLoad]
public static class PlayModeOperationKey
{
    // ---- メニュー ----
#if UNITY_EDITOR_WIN
    // Windows ではキーを直接見るので、メニューにはショートカットを付けない
    // （付けると、1回押しただけで「一時停止 → すぐ再開」と2回動いてしまう）
    const string MenuPlay = "Tools/Playモード/開始 (F8)";
    const string MenuPause = "Tools/Playモード/一時停止・再開 (F9)";
    const string MenuExit = "Tools/Playモード/終了 (Shift+F9)";
#else
    const string MenuPlay = "Tools/Playモード/開始 _F8";
    const string MenuPause = "Tools/Playモード/一時停止・再開 _F9";
    const string MenuExit = "Tools/Playモード/終了 #_F9";
#endif

    static PlayModeOperationKey()
    {
#if UNITY_EDITOR_WIN
        // 二重登録を防ぐため、一度外してから登録する
        EditorApplication.update -= PollKeys;
        EditorApplication.update += PollKeys;
#endif
    }

    // =========================================================
    // Windows：キーボードを直接見る
    // =========================================================
#if UNITY_EDITOR_WIN
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    const int VK_SHIFT = 0x10;
    const int VK_PLAY = 0x77;  // F8
    const int VK_PAUSE = 0x78; // F9

    // 前回見た時に押されていたか（押した瞬間だけ反応するため）
    static bool playWasDown;
    static bool pauseWasDown;

    static void PollKeys()
    {
        bool playDown = IsDown(VK_PLAY);
        bool pausePressed = PressedNow(VK_PAUSE, pauseWasDown, out bool pauseDown);
        bool playPressed = playDown && !playWasDown;

        playWasDown = playDown;
        pauseWasDown = pauseDown;

        // Unity が一番手前でない時（Visual Studio やブラウザを触っている時）は反応しない
        if (!InternalEditorUtility.isApplicationActive) return;

        if (playPressed)
        {
            StartPlay();
        }

        if (pausePressed)
        {
            if (IsDown(VK_SHIFT)) ExitPlay();
            else TogglePause();
        }
    }

    static bool IsDown(int vKey)
    {
        return (GetAsyncKeyState(vKey) & 0x8000) != 0;
    }

    /// <summary>
    /// 押した瞬間か。
    /// 押して離すのが速すぎて「押されている瞬間」を見逃しても、
    /// 「前回から押されたことがある」印（下位ビット）で拾う
    /// </summary>
    static bool PressedNow(int vKey, bool wasDown, out bool isDown)
    {
        short state = GetAsyncKeyState(vKey);
        isDown = (state & 0x8000) != 0;
        bool tappedSinceLast = (state & 0x0001) != 0;
        return !wasDown && (isDown || tappedSinceLast);
    }
#endif

    // =========================================================
    // メニュー（Windows 以外ではショートカットとして動く）
    // =========================================================

    [MenuItem(MenuPlay)]
    static void PlayFromMenu() => StartPlay();

    [MenuItem(MenuPlay, true)]
    static bool PlayFromMenuValidate() => !EditorApplication.isPlaying;

    [MenuItem(MenuPause)]
    static void PauseFromMenu() => TogglePause();

    [MenuItem(MenuPause, true)]
    static bool PauseFromMenuValidate() => EditorApplication.isPlaying;

    [MenuItem(MenuExit)]
    static void ExitFromMenu() => ExitPlay();

    [MenuItem(MenuExit, true)]
    static bool ExitFromMenuValidate() => EditorApplication.isPlaying;

    // =========================================================
    // 操作
    // =========================================================

    static void StartPlay()
    {
        if (EditorApplication.isPlaying) return;

        // スクリプトのコンパイル中に再生すると、古いコードのまま動いてしまうので待ってもらう
        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("[PlayModeOperationKey] スクリプトのコンパイル中です。終わってからもう一度 F8 を押してください。");
            return;
        }

        EditorApplication.isPlaying = true;
    }

    static void TogglePause()
    {
        if (!EditorApplication.isPlaying) return;

        EditorApplication.isPaused = !EditorApplication.isPaused;
        Debug.Log(EditorApplication.isPaused
            ? "[PlayModeOperationKey] 一時停止しました（F9 で再開 / Shift+F9 で終了）"
            : "[PlayModeOperationKey] 再開しました");
    }

    static void ExitPlay()
    {
        if (!EditorApplication.isPlaying) return;

        Debug.Log("[PlayModeOperationKey] Play モードを終了しました");
        EditorApplication.isPlaying = false;
    }
}
#endif
