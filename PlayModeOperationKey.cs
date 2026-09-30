#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 【開発用】F8 で Play モードを開始、F9 で停止する。
///
/// ■ 置き場所
///   「Editor」という名前のフォルダの“外”に置くこと（例：Assets/Script/Dev/）。
///   Play 中にゲーム側でキーを受け取るため、実行時のスクリプトとして動かす必要がある。
///   ファイル全体が #if UNITY_EDITOR で囲まれているので、ビルド版には一切含まれない。
///
/// ■ キーの役割（開始と停止を別のキーにしている理由）
///   F8：開始だけ（Play 中に押しても何もしない）
///   F9：停止だけ（Play 中でなければ何もしない）
///   1つのキーで開始・停止を切り替える方式だと、止まるのが遅れた時に
///   もう一度押して「また再生が始まる」事故が起きるため、役割を分けている。
///
/// ■ 停止の仕組み（2段構え）
///   ① Game ビューにフォーカスがある時（普通にプレイしている時）
///      → 見えない監視オブジェクトを自動で作り、毎フレーム F9 を監視して停止する。
///   ② Hierarchy・Inspector など、エディタの他の画面にフォーカスがある時
///      → メニュー「Tools/Playモードを停止」のショートカット（F9）で停止する。
///
/// ■ キーを変えたい時
///   開始キー     … MenuPlay の「_F8」
///   停止キー     … MenuStop の「_F9」と、StopKeyCode・StopInputSystemKey の計3か所
///   （メニューのショートカットは Edit → Shortcuts からも変更できるが、
///     ①の Game ビュー用の監視キーはこのファイルの定数なので、そちらも合わせて変えること）
/// </summary>
[AddComponentMenu("")] // コンポーネントの追加メニューには出さない
public class PlayModeOperationKey : MonoBehaviour
{
    // ---- キーの設定 ----
    const string MenuPlay = "Tools/Playモードを開始 _F8";
    const string MenuStop = "Tools/Playモードを停止 _F9";

    // Game ビューで停止を監視するキー（旧 Input / 新 Input System）
    const KeyCode StopKeyCode = KeyCode.F9;
#if ENABLE_INPUT_SYSTEM
    const Key StopInputSystemKey = Key.F9;
#endif

    // 今動いている監視オブジェクト。
    // bool のフラグではなく「実物への参照」で二重生成を防ぐ。
    // （Enter Play Mode Options で Domain Reload を切っていると static の値が次の再生まで残るため、
    //   bool だと「前回作った」ことだけが残って、2回目以降の再生で監視が作られなくなる。
    //   参照なら、前回のオブジェクトが破棄されていれば null 扱いになるので正しく作り直せる）
    static PlayModeOperationKey instance;

    // =========================================================
    // ① Play 中、Game ビューで押された時
    // =========================================================

    /// <summary>Play 開始時に、監視用の見えないオブジェクトを自動で作る</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateWatcher()
    {
        // 【二重生成の防止】すでに動いている監視があれば作らない
        if (instance != null) return;

        GameObject watcher = new GameObject("[PlayModeOperationKey]");

        // Hierarchy に出さないだけにする（HideAndDontSave は使わない）。
        // DontSave 系の指定をすると、Play を止めても破棄されずにエディタ側へ残ってしまうことがあるため。
        // DontDestroyOnLoad のシーンはそもそも保存されないので、保存の心配は無い
        watcher.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(watcher); // シーンを切り替えても残す

        instance = watcher.AddComponent<PlayModeOperationKey>();
    }

    void Update()
    {
        // Time.timeScale = 0（リミットブレイクの演出中・ポーズ中）でも Update は動くので止められる
        if (StopKeyPressed())
        {
            StopPlayMode();
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    static bool StopKeyPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(StopKeyCode)) return true;
#endif
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[StopInputSystemKey].wasPressedThisFrame) return true;
#endif
        return false;
    }

    // =========================================================
    // Play 終了時の後片付け（念のため、監視オブジェクトを確実に消す）
    // =========================================================

    [InitializeOnLoadMethod]
    static void RegisterPlayModeCallback()
    {
        // 二重登録を防ぐため、一度外してから登録する
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode) return;

        if (instance != null)
        {
            Destroy(instance.gameObject);
        }
        instance = null;
    }

    // =========================================================
    // ② メニュー／ショートカット（エディタの画面にフォーカスがある時）
    // =========================================================

    [MenuItem(MenuPlay)]
    static void PlayFromMenu()
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

    // Play 中は開始メニューを灰色にする（F8 を押しても何も起きない）
    [MenuItem(MenuPlay, true)]
    static bool PlayFromMenuValidate()
    {
        return !EditorApplication.isPlaying;
    }

    [MenuItem(MenuStop)]
    static void StopFromMenu()
    {
        StopPlayMode();
    }

    // Play 中でなければ停止メニューを灰色にする（F9 を押しても何も起きない）
    [MenuItem(MenuStop, true)]
    static bool StopFromMenuValidate()
    {
        return EditorApplication.isPlaying;
    }

    // =========================================================
    // 共通
    // =========================================================

    static void StopPlayMode()
    {
        if (!EditorApplication.isPlaying) return;

        Debug.Log("[PlayModeOperationKey] F9 で Play モードを停止しました");
        EditorApplication.isPlaying = false;
    }
}
#endif
