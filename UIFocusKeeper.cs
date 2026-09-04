using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // Selectableコンポーネントを扱うために追加

// =========================================================
// 【新規追加】マウスホバーした瞬間、そのオブジェクトを本当の意味での
// currentSelectedGameObjectにするための専用コンポーネント。
// EventTriggerとは完全に独立しているので、他のスクリプトが
// trigger.triggers.Clear()を呼んでも影響を受けない。
// これにより「パッドで選択中の項目」と「マウスホバー中の項目」が
// 別々にハイライトされる、という状態を防げる(常にどちらか一方に統一される)。
// =========================================================
[DisallowMultipleComponent]
public class HoverToSelect : MonoBehaviour, IPointerEnterHandler
{
    Selectable m_Selectable;

    void Awake()
    {
        m_Selectable = GetComponent<Selectable>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 操作不可(interactable == false)のボタンはホバーしても選択状態にしない
        if (m_Selectable != null && !m_Selectable.interactable) return;
        if (EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(gameObject);
    }
}


[DisallowMultipleComponent] // 同じオブジェクトへの複数アタッチを防止
public class UIFocusKeeper : MonoBehaviour
{
    [Tooltip("画面が開いた時に最初に選択状態にしたいオブジェクト（未設定でも可）")]
    [SerializeField] private GameObject _firstSelectedObject;

    [Tooltip("この階層以下にあるSelectable(Button等)全てに、マウスホバーで選択状態にする機能を自動で付与する。複数の親を登録可能。未設定なら何もしない。")]
    [SerializeField] private Transform[] _hoverSelectRoots;

    // 直前に選ばれていたゲームオブジェクトを記憶する箱
    private GameObject _lastSelectedObject;

    void Start()
    {
        // 初期選択オブジェクトが設定されており、かつ現在何も選択されていなければフォーカスを当てる
        if (_firstSelectedObject != null && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
        {
            EventSystem.current.SetSelectedGameObject(_firstSelectedObject);
            _lastSelectedObject = _firstSelectedObject;
        }

        AttachHoverToSelectAll();
    }

    // =========================================================
    // 【改修】登録された全ての親(_hoverSelectRoots)に対して、配下のSelectableへ
    // HoverToSelectを一括付与する。publicにしてあるので、スキル一覧のように
    // ボタンが後から動的に生成される画面がある場合、その生成処理の後に
    // 手動でもう一度呼び出すこともできる(既に付与済みのものはスキップされる)。
    // =========================================================
    public void AttachHoverToSelectAll()
    {
        if (_hoverSelectRoots == null) return;

        foreach (Transform root in _hoverSelectRoots)
        {
            if (root != null)
            {
                AttachHoverToSelect(root);
            }
        }
    }

    // =========================================================
    // 指定した1つの親の階層以下にある全Selectableに、
    // HoverToSelectコンポーネントを自動付与する。
    // 既に付いているものには追加しない(多重アタッチ防止・保険)。
    // =========================================================
    void AttachHoverToSelect(Transform root)
    {
        Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);

        foreach (Selectable selectable in selectables)
        {
            if (selectable.GetComponent<HoverToSelect>() == null)
            {
                selectable.gameObject.AddComponent<HoverToSelect>();
            }
        }
    }

    void Update()
    {
        // UnityのUI管理システムが存在しない場合は何もしない
        if (EventSystem.current == null) return;

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        // 🟢 現在、どこかのボタンが正しく選択されている場合
        if (currentSelected != null)
        {
            // そのオブジェクトを「直前の正しい選択肢」として記憶しておく
            _lastSelectedObject = currentSelected;
        }
        // 🔴 マウスで何もないところをクリックして、選択が外れて（nullになって）しまった場合
        else if (_lastSelectedObject != null)
        {
            // 直前まで選ばれていたオブジェクトが「現在も再選択可能な状態か」をチェック
            if (IsSelectableAndActive(_lastSelectedObject))
            {
                // 再選択可能なら、強制的にフォーカスを戻す
                EventSystem.current.SetSelectedGameObject(_lastSelectedObject);
            }
            else
            {
                // オブジェクトが非表示や操作不可になっていた場合は、記憶をリセットする
                _lastSelectedObject = null;
            }
        }
    }

    /// <summary>
    /// オブジェクトがヒエラルキー上でアクティブかつ、操作可能なSelectableを持っているか判定する
    /// </summary>
    private bool IsSelectableAndActive(GameObject obj)
    {
        // 1. オブジェクト自体が非アクティブ（非表示）なら不可
        if (!obj.activeInHierarchy) return false;

        // 2. Selectableコンポーネント（Button等の親クラス）を取得
        Selectable selectable = obj.GetComponent<Selectable>();

        // 3. コンポーネントが存在し、かつ操作不可（interactable == false）に設定されているなら不可
        if (selectable != null && !selectable.interactable) return false;

        return true;
    }
}