using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonBlinkLoop : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler
{
    // ---------------------------------------------------------
    // Components
    // ---------------------------------------------------------

    private TextMeshProUGUI _textPro;
    private Text _oldText;
    private Image _buttonImage;
    private Button _button;

    // ---------------------------------------------------------
    // State
    // ---------------------------------------------------------

    private bool _isHovered = false;
    private bool _isSelected = false;

    // 前フレームまでButtonが操作可能だったか
    // interactable切り替え時の色残り防止用
    private bool _wasInteractable = true;

    /// <summary>
    /// マウスホバー、またはキーボード・ゲームパッドで
    /// 選択されている場合に演出を有効化する。
    /// </summary>
    private bool IsActiveEffect => _isHovered || _isSelected;

    // ---------------------------------------------------------
    // Effect Settings
    // ---------------------------------------------------------

    [Header("--- 演出切り替えスイッチ ---")]
    [Tooltip(
        "ON: ボタンの色や薄さ、テキストの不透明度もすべて連動させる豪華演出\n" +
        "OFF: テキスト色を変化させるだけの通常演出"
    )]
    [SerializeField]
    private bool useRichEffects = false;

    [Header("点滅（警告）カラー")]
    [SerializeField]
    private float blinkSpeed = 5.0f;

    [SerializeField, Range(0f, 1f)]
    private float minAlpha = 0.3f;

    [SerializeField]
    private Color warningColor = Color.red;

    [Header("通常（待機）カラー")]
    [SerializeField]
    private Color normalColor = Color.white;

    [SerializeField]
    private Color normalButtonColor = Color.white;

    [Header("通常時のグラデーション設定")]
    [SerializeField]
    private bool useGradientInNormal = false;

    [SerializeField]
    private Color gradTopLeft = new Color(1f, 0.85f, 0.2f);

    [SerializeField]
    private Color gradTopRight = new Color(1f, 0.45f, 0.2f);

    [SerializeField]
    private Color gradBotLeft = new Color(0.95f, 0.2f, 0.2f);

    [SerializeField]
    private Color gradBotRight = new Color(1f, 0.65f, 0.2f);

    private float _blinkTimer = 0f;

    // ---------------------------------------------------------
    // 【新規】Scale Settings（ボタンそのものの拡大縮小）
    //
    // ホバー/選択された瞬間に「通常サイズ → 拡大サイズ」へ、
    // 外れた瞬間に「今のサイズ → 通常サイズ」へ、
    // それぞれ別のAnimationCurveでイージングしながら変化する。
    //
    // カーブの縦軸は「進み具合」(0 = 変化前、1 = 変化後)。
    // 1を超える山を作ればオーバーシュート（行き過ぎて戻る）、
    // 波を複数作ればバウンドやエラスティックも表現できる。
    // ---------------------------------------------------------

    [Header("--- 拡大縮小演出 ---")]
    [Tooltip("ONでホバー/選択時にボタンそのものを拡大縮小する")]
    [SerializeField]
    private bool useScaleEffect = true;

    [Tooltip("ホバー/選択時の大きさ（通常サイズに対する倍率）")]
    [SerializeField, Range(0.5f, 2f)]
    private float hoverScale = 1.08f;

    [Header("拡大（ホバー開始）")]
    [Tooltip("拡大にかける時間（秒・実時間）")]
    [SerializeField, Min(0f)]
    private float scaleInDuration = 0.25f;

    [Tooltip(
        "拡大のイージング。横軸 = 経過(0～1)、縦軸 = 進み具合(0～1)\n" +
        "初期値は「少し大きくなりすぎてから戻る」バック系のカーブ"
    )]
    [SerializeField]
    private AnimationCurve scaleInCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 4f),
        new Keyframe(0.55f, 1.15f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f)
    );

    [Header("縮小（ホバー終了）")]
    [Tooltip("元の大きさに戻るまでの時間（秒・実時間）")]
    [SerializeField, Min(0f)]
    private float scaleOutDuration = 0.15f;

    [Tooltip("縮小のイージング。初期値はなめらかな加速→減速")]
    [SerializeField]
    private AnimationCurve scaleOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("ホバー中の脈動（呼吸するような伸縮）")]
    [Tooltip("ONで、拡大し終わった後もゆっくり伸び縮みし続ける")]
    [SerializeField]
    private bool usePulse = true;

    [Tooltip("脈動の強さ（0.03なら拡大サイズからさらに最大+3%）")]
    [SerializeField, Range(0f, 0.3f)]
    private float pulseAmount = 0.03f;

    [Tooltip("脈動1回にかかる時間（秒・実時間）")]
    [SerializeField, Min(0.05f)]
    private float pulsePeriod = 0.8f;

    [Tooltip(
        "脈動の形。横軸 = 1周期の中の位置(0～1)、縦軸 = 膨らみ具合(0～1)\n" +
        "0と1の値を同じにしておくと、繰り返しの継ぎ目がなめらかになる"
    )]
    [SerializeField]
    private AnimationCurve pulseCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 0f)
    );

    // 拡大縮小の内部状態
    private Vector3 _baseScale = Vector3.one;     // 元の大きさ（Awake時点）
    private Vector3 _currentScale = Vector3.one;  // 今フレームで実際に適用している大きさ
    private Vector3 _scaleFrom = Vector3.one;     // 変化の開始時の大きさ
    private Vector3 _scaleTo = Vector3.one;       // 変化の目標の大きさ
    private bool _scaleActive = false;            // 拡大側へ向かっているか
    private bool _scaleTransitioning = false;     // 変化の途中か
    private float _scaleTimer = 0f;
    private float _pulseTimer = 0f;

    // ---------------------------------------------------------
    // Unity Lifecycle
    // ---------------------------------------------------------

    private void Awake()
    {
        // 毎フレームGetComponentしないように最初に取得
        _textPro = GetComponentInChildren<TextMeshProUGUI>(true);
        _oldText = GetComponentInChildren<Text>(true);
        _buttonImage = GetComponent<Image>();
        _button = GetComponent<Button>();

        if (_button != null)
        {
            _wasInteractable = _button.interactable;
        }

        // 【新規】元の大きさを記録（ここを基準に拡大縮小する）
        _baseScale = transform.localScale;
        _currentScale = _baseScale;
    }

    private void OnEnable()
    {
        _blinkTimer = 0f;

        ApplyNormalColor();

        // 【新規】表示された時は必ず通常サイズから
        ResetScaleImmediate();
    }

    private void Update()
    {
        // -----------------------------------------------------
        // 【新規】拡大縮小
        // 操作不能(interactable=false)の時は「非アクティブ」扱いにして
        // 通常サイズへ戻す。色の処理より先に行うので、
        // 下のreturnに影響されない。
        // -----------------------------------------------------

        bool interactable = (_button == null) || _button.interactable;
        UpdateScale(IsActiveEffect && interactable);

        // -----------------------------------------------------
        // Buttonが操作不能の場合
        // -----------------------------------------------------

        if (_button != null && !_button.interactable)
        {
            // interactable=true → false になった瞬間だけ
            // 点滅中だったテキスト色を通常状態へ戻す。
            //
            // Button本体のImageはここでは触らない。
            // Unity Button側のDisabled表現を邪魔しないため。
            if (_wasInteractable)
            {
                ApplyNormalTextColor();
            }

            _wasInteractable = false;

            return;
        }

        // -----------------------------------------------------
        // interactableが復帰した場合
        // -----------------------------------------------------

        if (!_wasInteractable)
        {
            _wasInteractable = true;
            _blinkTimer = 0f;

            // ホバーも選択もされていないなら
            // 通常状態へ完全復帰
            if (!IsActiveEffect)
            {
                ApplyNormalColor();
            }
        }

        // -----------------------------------------------------
        // 点滅演出
        // -----------------------------------------------------

        if (IsActiveEffect)
        {
            _blinkTimer += Time.unscaledDeltaTime;

            float t = Mathf.PingPong(
                _blinkTimer * blinkSpeed,
                1f
            );

            float alpha = Mathf.Lerp(
                minAlpha,
                1f,
                t
            );

            // -------------------------------------------------
            // Text
            // -------------------------------------------------

            Color textColor = warningColor;

            if (useRichEffects)
            {
                textColor.a = alpha;
            }

            if (_textPro != null)
            {
                // 点滅中は通常時グラデーションを解除
                _textPro.enableVertexGradient = false;
                _textPro.color = textColor;
            }

            if (_oldText != null)
            {
                _oldText.color = textColor;
            }

            // -------------------------------------------------
            // Button Image
            // -------------------------------------------------

            if (useRichEffects && _buttonImage != null)
            {
                Color buttonColor = warningColor;
                buttonColor.a = alpha;

                _buttonImage.color = buttonColor;
            }
        }
    }

    private void OnDisable()
    {
        _isHovered = false;
        _isSelected = false;
        _blinkTimer = 0f;

        ApplyNormalColor();

        // 【新規】非表示になった時も通常サイズに戻す
        // (拡大したまま閉じて、次に開いた時に大きいままになるのを防ぐ)
        ResetScaleImmediate();
    }

    // ---------------------------------------------------------
    // 【新規】Scale
    // ---------------------------------------------------------

    /// <summary>
    /// 毎フレームの拡大縮小処理。
    /// wantActive = 「拡大した状態にしたいか」
    /// </summary>
    private void UpdateScale(bool wantActive)
    {
        if (!useScaleEffect)
        {
            // 途中でOFFにされた場合も、元の大きさに戻しておく
            if (_currentScale != _baseScale)
            {
                ResetScaleImmediate();
            }
            return;
        }

        // 状態が切り替わった瞬間に、新しい変化を開始する
        if (wantActive != _scaleActive)
        {
            StartScaleTransition(wantActive);
        }

        // メニューがポーズ中(timeScale = 0)でも動くよう実時間を使う
        float dt = Time.unscaledDeltaTime;

        Vector3 scale;

        if (_scaleTransitioning)
        {
            _scaleTimer += dt;

            float duration = _scaleActive ? scaleInDuration : scaleOutDuration;
            AnimationCurve curve = _scaleActive ? scaleInCurve : scaleOutCurve;

            float rate = (duration <= 0f) ? 1f : Mathf.Clamp01(_scaleTimer / duration);
            float eased = EvaluateCurve(curve, rate);

            // LerpUnclampedを使うことで、カーブが1を超えた分(オーバーシュート)や
            // 0を下回った分(引き込み)も、そのまま大きさに反映される
            scale = Vector3.LerpUnclamped(_scaleFrom, _scaleTo, eased);

            if (rate >= 1f)
            {
                _scaleTransitioning = false;
                scale = _scaleTo;
                _pulseTimer = 0f;
            }
        }
        else
        {
            scale = _scaleTo;
        }

        // 拡大し終わった後だけ、脈動を重ねる
        if (_scaleActive && !_scaleTransitioning && usePulse && pulseAmount > 0f)
        {
            _pulseTimer += dt;

            float phase = Mathf.Repeat(_pulseTimer / pulsePeriod, 1f);
            float pulse = EvaluateCurve(pulseCurve, phase);

            scale *= 1f + pulseAmount * pulse;
        }

        ApplyScale(scale);
    }

    /// <summary>
    /// 拡大/縮小の変化を開始する。
    /// 「今の大きさ」から始めるので、拡大の途中でマウスが外れても
    /// その場からなめらかに戻り、カクッと跳ねない。
    /// </summary>
    private void StartScaleTransition(bool toActive)
    {
        _scaleActive = toActive;
        _scaleTransitioning = true;
        _scaleTimer = 0f;

        _scaleFrom = _currentScale;
        _scaleTo = toActive ? _baseScale * hoverScale : _baseScale;
    }

    /// <summary>アニメーションなしで、即座に通常サイズへ戻す。</summary>
    private void ResetScaleImmediate()
    {
        _scaleActive = false;
        _scaleTransitioning = false;
        _scaleTimer = 0f;
        _pulseTimer = 0f;

        _scaleFrom = _baseScale;
        _scaleTo = _baseScale;

        ApplyScale(_baseScale);
    }

    private void ApplyScale(Vector3 scale)
    {
        _currentScale = scale;
        transform.localScale = scale;
    }

    /// <summary>
    /// カーブが未設定(キーが0個)の場合でも壊れないように評価する。
    /// </summary>
    private static float EvaluateCurve(AnimationCurve curve, float t)
    {
        if (curve == null || curve.length == 0)
        {
            return t; // カーブが無ければ等速(リニア)
        }
        return curve.Evaluate(t);
    }

    // ---------------------------------------------------------
    // Mouse
    // ---------------------------------------------------------

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        _blinkTimer = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;

        // キーボード / パッド側でも選択されていなければ戻す
        if (!IsActiveEffect)
        {
            ApplyNormalColor();
        }
    }

    // ---------------------------------------------------------
    // Keyboard / GamePad
    // ---------------------------------------------------------

    public void OnSelect(BaseEventData eventData)
    {
        _isSelected = true;
        _blinkTimer = 0f;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;

        // マウスホバーもされていなければ通常状態へ
        if (!IsActiveEffect)
        {
            ApplyNormalColor();
        }
    }

    // ---------------------------------------------------------
    // Normal State
    // ---------------------------------------------------------

    /// <summary>
    /// テキストとボタンImageを通常状態へ戻す。
    /// </summary>
    private void ApplyNormalColor()
    {
        ApplyNormalTextColor();

        if (_buttonImage != null)
        {
            _buttonImage.color = normalButtonColor;
        }
    }

    /// <summary>
    /// テキストのみ通常状態へ戻す。
    /// Button.interactable=false時に、
    /// Unity標準のDisabled表現を邪魔しないため分離。
    /// </summary>
    private void ApplyNormalTextColor()
    {
        if (_textPro != null)
        {
            if (useGradientInNormal)
            {
                _textPro.color = Color.white;
                _textPro.enableVertexGradient = true;

                _textPro.colorGradient = new VertexGradient(
                    gradTopLeft,
                    gradTopRight,
                    gradBotLeft,
                    gradBotRight
                );
            }
            else
            {
                _textPro.enableVertexGradient = false;
                _textPro.color = normalColor;
            }
        }

        if (_oldText != null)
        {
            _oldText.color =
                useGradientInNormal
                    ? gradBotLeft
                    : normalColor;
        }
    }

    // ---------------------------------------------------------
    // External Control
    // ---------------------------------------------------------

    /// <summary>
    /// クラスチェンジ等で警告色をシアン系へ変更。
    /// Color32を使うことで0～255表記を正しく扱う。
    /// </summary>
    public void ClassChangeButtonColor()
    {
        warningColor = new Color32(
            0,
            255,
            206,
            255
        );

        _blinkTimer = 0f;
    }

    /// <summary>
    /// 【新規】実行中にボタンの「元の大きさ」を変えた場合に呼ぶ。
    /// 今の大きさを新しい基準として記録し直す。
    /// </summary>
    public void RefreshBaseScale()
    {
        // 先に「今の大きさ」を新しい基準として読み取ってから、状態をリセットする
        _baseScale = transform.localScale;
        ResetScaleImmediate();
    }
}
