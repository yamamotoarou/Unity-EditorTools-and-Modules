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
///   F9         … 一時停止するだけ（一方通行。一時停止中・Play 中でない時は何もしない）
///                  再開はツールバーの ⏸ ボタンをマウスで押す。
///                  （キーで再開すると Unity エディタ本体が落ちることがあったため、再開はキーで行わない）
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
/// ■ 連打対策
///   一時停止の切り替えを短い間隔で繰り返すと、Unity エディタ本体が落ちることがある。
///   そこで次の3つで守っている。
///     ・前回の操作から CooldownSeconds 秒たつまでは、次の操作を受け付けない
///     ・操作はその場で行わず、エディタの処理の区切り（EditorApplication.delayCall）まで遅らせる
///     ・Play の開始・終了の切り替え中や、コンパイル中は何もしない
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
    const string MenuPause = "Tools/Playモード/一時停止 (F9)";
    const string MenuExit = "Tools/Playモード/終了 (Shift+F9)";
#else
    const string MenuPlay = "Tools/Playモード/開始 _F8";
    const string MenuPause = "Tools/Playモード/一時停止 _F9";
    const string MenuExit = "Tools/Playモード/終了 #_F9";
#endif

    // 前回の操作から、この秒数がたつまでは次の操作を受け付けない（連打対策）
    const double CooldownSeconds = 0.35;

    // 最後に操作した時刻（エディタ起動からの秒数）
    static double lastOperationTime = -999;

    // 操作の予約が入っているか（delayCall に二重に積まないため）
    static bool operationPending;

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
            Request(StartPlay);
        }
        else if (pausePressed)
        {
            if (IsDown(VK_SHIFT)) Request(ExitPlay);
            else Request(Pause);
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
    static void PlayFromMenu() => Request(StartPlay);

    [MenuItem(MenuPlay, true)]
    static bool PlayFromMenuValidate() => !EditorApplication.isPlaying;

    [MenuItem(MenuPause)]
    static void PauseFromMenu() => Request(Pause);

    [MenuItem(MenuPause, true)]
    static bool PauseFromMenuValidate() => EditorApplication.isPlaying && !EditorApplication.isPaused;

    [MenuItem(MenuExit)]
    static void ExitFromMenu() => Request(ExitPlay);

    [MenuItem(MenuExit, true)]
    static bool ExitFromMenuValidate() => EditorApplication.isPlaying;

    // =========================================================
    // 連打対策：操作の受付
    // =========================================================

    /// <summary>
    /// 操作を予約する。
    /// ・前回から CooldownSeconds 秒たっていなければ捨てる
    /// ・すでに予約があれば捨てる
    /// ・実行はエディタの処理の区切り（delayCall）まで遅らせる
    /// </summary>
    static void Request(System.Action operation)
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - lastOperationTime < CooldownSeconds) return;
        if (operationPending) return;

        lastOperationTime = now;
        operationPending = true;

        EditorApplication.delayCall += () =>
        {
            operationPending = false;
            if (IsTransitioning()) return;
            operation();
        };
    }

    /// <summary>
    /// Play の開始・終了の切り替え中か、コンパイル中か。
    /// この間に再生状態や一時停止を触ると、エディタが不安定になりやすい
    /// </summary>
    static bool IsTransitioning()
    {
        // isPlaying と isPlayingOrWillChangePlaymode が食い違う ＝ 開始中・終了中
        bool changing = EditorApplication.isPlaying != EditorApplication.isPlayingOrWillChangePlaymode;
        return changing || EditorApplication.isCompiling || EditorApplication.isUpdating;
    }

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

    /// <summary>
    /// 一時停止するだけ（一方通行）。すでに一時停止中なら何もしない。
    /// 再開はツールバーの ⏸ ボタンをマウスで押す
    /// </summary>
    static void Pause()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.isPaused) return;

        EditorApplication.isPaused = true;
        Debug.Log("[PlayModeOperationKey] 一時停止しました（再開はツールバーの ⏸ ボタン / Shift+F9 で終了）");
    }

    static void ExitPlay()
    {
        if (!EditorApplication.isPlaying) return;

        Debug.Log("[PlayModeOperationKey] Play モードを終了しました");
        EditorApplication.isPlaying = false;
    }
}
#endif
