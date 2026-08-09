using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UISfHighlightController : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("--- 1. 光の玉（周回）の設定 ---")]
    [SerializeField] private RectTransform particleRect;
    [SerializeField] private float rotateSpeed = 8f;
    [SerializeField] private float padding = 5f;

    [Header("--- 2. 個別外周グラフィック（パルス明滅）の設定 ---")]
    [Tooltip("外周専用に用意したImageコンポーネントをここにドラッグ＆ドロップ")]
    [SerializeField] private Image outlineImage;
    [SerializeField] private float pulseSpeed = 5f;
    [Tooltip("パルス時の最大スケール倍率（1.05〜1.1あたりがおすすめ）")]
    [SerializeField] private float maxScale = 1.08f;
    [SerializeField] private Color outlineColor = new Color(0f, 0.8f, 1f, 1f);

    private RectTransform buttonRect;
    private RectTransform outlineRect;
    private bool isHighlighted = false;
    private float angle = 0f;
    private float pulseTime = 0f;

    void Start()
    {
        buttonRect = GetComponent<RectTransform>();

        // 外周用Imageの初期化とRectTransformの取得
        if (outlineImage != null)
        {
            outlineRect = outlineImage.GetComponent<RectTransform>();
            outlineImage.color = new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0f); // 最初は透明に
            outlineImage.gameObject.SetActive(false); // オブジェクト自体も隠しておく
        }

        // 光の玉の初期化（最初は非表示）
        if (particleRect != null)
        {
            particleRect.gameObject.SetActive(false);
            particleRect.anchorMin = new Vector2(0.5f, 0.5f);
            particleRect.anchorMax = new Vector2(0.5f, 0.5f);
            particleRect.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    void Update()
    {
        if (!isHighlighted) return;

        // --- ① 光の玉を回す計算 ---
        if (particleRect != null && buttonRect != null)
        {
            angle += Time.unscaledDeltaTime * rotateSpeed;
            if (angle > Mathf.PI * 2) angle -= Mathf.PI * 2;

            float radiusX = (buttonRect.rect.width * 0.5f) + padding;
            float radiusY = (buttonRect.rect.height * 0.5f) + padding;

            particleRect.anchoredPosition = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        // --- ② 個別グラフィックをパルス明滅（＋微細なスケールアップ）させる計算 ---
        if (outlineImage != null)
        {
            pulseTime += Time.unscaledDeltaTime * pulseSpeed;

            // 透明度（Alpha）を 0.4 〜 1.0 の間でサイン波で揺らす
            float alpha = 0.7f + Mathf.Sin(pulseTime) * 0.3f;
            Color c = outlineColor;
            c.a = alpha;
            outlineImage.color = c;

            // 標準のOutlineの代わりに、グラフィック自体を少しだけ膨らませる（パルス効果）
            if (outlineRect != null)
            {
                float currentScale = 1.0f + ((Mathf.Sin(pulseTime) + 1f) * 0.5f * (maxScale - 1.0f));
                outlineRect.localScale = new Vector3(currentScale, currentScale, 1f);
            }
        }
    }

    public void OnSelect(BaseEventData eventData) => PlayGlow();
    public void OnDeselect(BaseEventData eventData) => StopGlow();
    public void OnPointerEnter(PointerEventData eventData) => PlayGlow();
    public void OnPointerExit(PointerEventData eventData)
    {
        if (EventSystem.current.currentSelectedGameObject != gameObject) StopGlow();
    }

    private void PlayGlow()
    {
        isHighlighted = true;
        if (particleRect != null) particleRect.gameObject.SetActive(true);
        if (outlineImage != null) outlineImage.gameObject.SetActive(true);
    }

    private void StopGlow()
    {
        isHighlighted = false;
        angle = 0f;
        pulseTime = 0f;

        if (particleRect != null) particleRect.gameObject.SetActive(false);

        if (outlineImage != null)
        {
            outlineImage.color = new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0f);
            if (outlineRect != null) outlineRect.localScale = Vector3.one;
            outlineImage.gameObject.SetActive(false);
        }
    }
}
