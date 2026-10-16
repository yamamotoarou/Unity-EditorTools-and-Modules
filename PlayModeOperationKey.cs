#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
#if UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

/// <summary>
/// F8: 開始 / F9: 一時停止のみ / Shift+F9: 終了。
/// 再開はUnityのツールバーから行う。
/// 遷移・コンパイル中の入力は予約せず破棄する。
/// 終了後は2秒待ち、F8/F9/Shiftを離してから押し直す。
/// 既存の同名スクリプトと置き換える。推奨配置: Assets/Editor。
/// </summary>
[InitializeOnLoad]
public static class PlayModeOperationKey
{
#if UNITY_EDITOR_WIN
    const string MenuPlay = "Tools/Playモード/開始 (F8)";
    const string MenuPause = "Tools/Playモード/一時停止 (F9)";
    const string MenuExit = "Tools/Playモード/終了 (Shift+F9)";
#else
    const string MenuPlay = "Tools/Playモード/開始 _F8";
    const string MenuPause = "Tools/Playモード/一時停止 _F9";
    const string MenuExit = "Tools/Playモード/終了 #F9";
#endif

    const double OperationCooldown = 0.75;
    const double TransitionCooldown = 2.0;
    const double ReleaseQuietSeconds = 0.25;
    const double RequestLifetime = 0.5;

    enum Operation { None, Play, Pause, Exit }
    static Operation pending;
    static double pendingAt;
    static double blockedUntil;
    static bool lifecycleTransition;
    static bool executing;
    static bool shuttingDown;
    static bool expectedPlaying;
    static bool expectedPaused;

#if UNITY_EDITOR_WIN
    [DllImport("user32.dll", ExactSpelling = true)]
    static extern short GetAsyncKeyState(int vKey);
    const int VK_SHIFT = 0x10;
    const int VK_PLAY = 0x77;
    const int VK_PAUSE = 0x78;
    static bool keysArmed;
    static double releasedSince = -1;
#endif

    static PlayModeOperationKey()
    {
        // Domain Reload有効時はここから待ち直す。無効時はイベントで待ち直す。
        BlockFor(TransitionCooldown);
        lifecycleTransition = FlagsChanging();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.pauseStateChanged -= OnPauseChanged;
        EditorApplication.pauseStateChanged += OnPauseChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.quitting -= OnQuitting;
        EditorApplication.quitting += OnQuitting;
    }

    static bool FlagsChanging()
    {
        return EditorApplication.isPlaying != EditorApplication.isPlayingOrWillChangePlaymode;
    }

    static bool IsBusy()
    {
        return shuttingDown || executing || lifecycleTransition || FlagsChanging() ||
            EditorApplication.isCompiling || EditorApplication.isUpdating;
    }

    static bool CanPerform(Operation operation)
    {
        if (IsBusy()) return false;
        switch (operation)
        {
            case Operation.Play:
                return !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode;
            case Operation.Pause:
                return EditorApplication.isPlaying && !EditorApplication.isPaused;
            case Operation.Exit:
                return EditorApplication.isPlaying;
            default:
                return false;
        }
    }

    static void Tick()
    {
        if (!InternalEditorUtility.isApplicationActive || IsBusy())
        {
            CancelPending();
            DisarmKeys();
            // ビジー解除／フォーカス復帰直後も入力を受け付けない。
            BlockFor(OperationCooldown);
            return;
        }
#if UNITY_EDITOR_WIN
        PollKeys();
#endif
    }

#if UNITY_EDITOR_WIN
    static bool IsDown(int key)
    {
        // 下位ビット（前回から押された履歴）は信頼できないため使わない。
        return (GetAsyncKeyState(key) & 0x8000) != 0;
    }

    static void PollKeys()
    {
        bool playDown = IsDown(VK_PLAY);
        bool pauseDown = IsDown(VK_PAUSE);
        bool shiftDown = IsDown(VK_SHIFT);
        double now = EditorApplication.timeSinceStartup;

        if (now < blockedUntil || pending != Operation.None)
        {
            DisarmKeys();
            return;
        }

        // 待機時間終了後、全キーを連続して離したことを確認してから有効化。
        if (!keysArmed)
        {
            if (playDown || pauseDown || shiftDown)
                releasedSince = -1;
            else if (releasedSince < 0)
                releasedSince = now;
            else if (now - releasedSince >= ReleaseQuietSeconds)
                keysArmed = true;
            return;
        }

        if (!playDown && !pauseDown) return;
        DisarmKeys();
        // 同時押しは終了・停止を優先し、開始へ振り替えない。
        if (pauseDown) Request(shiftDown ? Operation.Exit : Operation.Pause);
        else if (playDown && !shiftDown) Request(Operation.Play);
    }
#endif

    static void DisarmKeys()
    {
#if UNITY_EDITOR_WIN
        keysArmed = false;
        releasedSince = -1;
#endif
    }

    static void BlockFor(double seconds)
    {
        blockedUntil = System.Math.Max(blockedUntil, EditorApplication.timeSinceStartup + seconds);
    }

    static void CancelPending()
    {
        EditorApplication.delayCall -= ExecutePending;
        pending = Operation.None;
    }

    static void Request(Operation operation)
    {
        if (!InternalEditorUtility.isApplicationActive ||
            EditorApplication.timeSinceStartup < blockedUntil ||
            pending != Operation.None || !CanPerform(operation)) return;

        expectedPlaying = EditorApplication.isPlaying;
        expectedPaused = EditorApplication.isPaused;
        pendingAt = EditorApplication.timeSinceStartup;
        pending = operation;
        DisarmKeys();
        // 名前付きコールバックなので、遷移・終了・再コンパイル時に解除できる。
        EditorApplication.delayCall -= ExecutePending;
        EditorApplication.delayCall += ExecutePending;
    }

    static void ExecutePending()
    {
        Operation operation = pending;
        CancelPending();
        if (operation == Operation.None) return;
        double now = EditorApplication.timeSinceStartup;
        if (!InternalEditorUtility.isApplicationActive || now < blockedUntil ||
            now - pendingAt > RequestLifetime || !CanPerform(operation) ||
            EditorApplication.isPlaying != expectedPlaying ||
            EditorApplication.isPaused != expectedPaused) return;

        // プロパティ設定より先にロック。同期的にイベントが来ても再入しない。
        executing = true;
        BlockFor(operation == Operation.Pause ? OperationCooldown : TransitionCooldown);
        DisarmKeys();
        try
        {
            switch (operation)
            {
                case Operation.Play:
                    EditorApplication.isPlaying = true;
                    break;
                case Operation.Pause:
                    EditorApplication.isPaused = true;
                    break;
                case Operation.Exit:
                    EditorApplication.isPlaying = false;
                    break;
            }
        }
        finally
        {
            executing = false;
        }
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        CancelPending();
        DisarmKeys();
        lifecycleTransition = state == PlayModeStateChange.ExitingEditMode ||
                              state == PlayModeStateChange.ExitingPlayMode;
        // EnteredEditModeでも更新。終了要求時ではなく終了完了から2秒確保する。
        BlockFor(TransitionCooldown);
    }

    static void OnPauseChanged(PauseState state)
    {
        // ツールバーからの一時停止・再開でも古い予約を破棄する。
        CancelPending();
        DisarmKeys();
        BlockFor(OperationCooldown);
    }

    static void BeforeReload()
    {
        shuttingDown = true;
        Unregister();
    }

    static void OnQuitting()
    {
        shuttingDown = true;
        Unregister();
    }

    static void Unregister()
    {
        CancelPending();
        DisarmKeys();
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.pauseStateChanged -= OnPauseChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        EditorApplication.quitting -= OnQuitting;
    }

    static bool MenuReady(Operation operation)
    {
        return pending == Operation.None && EditorApplication.timeSinceStartup >= blockedUntil &&
            CanPerform(operation);
    }

    [MenuItem(MenuPlay)] static void PlayFromMenu() => Request(Operation.Play);
    [MenuItem(MenuPause)] static void PauseFromMenu() => Request(Operation.Pause);
    [MenuItem(MenuExit)] static void ExitFromMenu() => Request(Operation.Exit);
    [MenuItem(MenuPlay, true)] static bool PlayFromMenuValidate() => MenuReady(Operation.Play);
    [MenuItem(MenuPause, true)] static bool PauseFromMenuValidate() => MenuReady(Operation.Pause);
    [MenuItem(MenuExit, true)] static bool ExitFromMenuValidate() => MenuReady(Operation.Exit);
}
#endif
