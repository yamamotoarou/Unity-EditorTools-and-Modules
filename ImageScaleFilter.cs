using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImageScaleFilter : MonoBehaviour
{
    // =========================================================
    // キャラクター個別設定
    // =========================================================

    [Serializable]
    public class ImageScaleData
    {
        [Tooltip("対象Spriteの名前")]
        public string spriteName;

        [Tooltip("このキャラクター専用の追加倍率")]
        public float scaleMultiplier = 1.0f;

        [Tooltip("このキャラクター専用の位置補正")]
        public Vector2 positionOffset = Vector2.zero;
    }

    // =========================================================
    // 基本設定
    // =========================================================

    [Header("対象Image")]
    [Tooltip("キャラクター画像が表示されるImage")]
    [SerializeField]
    private Image targetImage;

    [Header("収める枠")]
    [Tooltip(
        "画像をこのRectTransform内に収めます。\n" +
        "未設定の場合はtargetImageの親RectTransformを使用します。"
    )]
    [SerializeField]
    private RectTransform frameRect;

    // =========================================================
    // 自動フィット設定
    // =========================================================

    [Header("自動フィット")]

    [Tooltip("ONにするとSpriteを枠内へ自動的に収めます")]
    [SerializeField]
    private bool autoFitToFrame = true;

    [Tooltip(
        "枠いっぱいではなく少し内側へ余白を確保します。\n" +
        "1.0 = 枠いっぱい\n" +
        "0.9 = 90%サイズ"
    )]
    [SerializeField, Range(0.1f, 1.0f)]
    private float framePadding = 0.95f;

    [Tooltip("Spriteの縦横比を維持します")]
    [SerializeField]
    private bool preserveAspect = true;

    // =========================================================
    // デフォルト設定
    // =========================================================

    [Header("デフォルト設定")]

    [Tooltip("登録されていない画像に使用する倍率")]
    [SerializeField]
    private float defaultScaleMultiplier = 1.0f;

    [Tooltip("登録されていない画像の位置補正")]
    [SerializeField]
    private Vector2 defaultPositionOffset = Vector2.zero;

    // =========================================================
    // キャラクター登録
    // =========================================================

    [Header("キャラクター個別設定")]

    [Tooltip(
        "Sprite名ごとの倍率・位置補正を登録できます。\n" +
        "何人でも追加可能です。"
    )]
    [SerializeField]
    private List<ImageScaleData> imageSettings =
        new List<ImageScaleData>();

    // =========================================================
    // 実行設定
    // =========================================================

    [Header("実行設定")]

    [Tooltip("ゲーム開始時に自動適用")]
    [SerializeField]
    private bool applyOnStart = true;

    // =========================================================
    // 内部データ
    // =========================================================

    private RectTransform _imageRect;

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (targetImage != null)
        {
            _imageRect = targetImage.rectTransform;
        }

        // 枠未設定なら親を自動使用
        if (frameRect == null &&
            targetImage != null &&
            targetImage.transform.parent != null)
        {
            frameRect =
                targetImage.transform.parent as RectTransform;
        }
    }

    private void Start()
    {
        if (applyOnStart)
        {
            Apply();
        }
    }

    // =========================================================
    // メイン処理
    // =========================================================

    /// <summary>
    /// 現在targetImageに設定されているSpriteへ
    /// 自動フィット＋キャラクター個別補正を適用する。
    /// </summary>
    public void Apply()
    {
        if (targetImage == null)
        {
            Debug.LogWarning(
                "【ImageScaleFilter】targetImageが設定されていません。"
            );

            return;
        }

        if (targetImage.sprite == null)
        {
            Debug.LogWarning(
                "【ImageScaleFilter】Spriteが設定されていません。"
            );

            return;
        }

        if (_imageRect == null)
        {
            _imageRect = targetImage.rectTransform;
        }

        // アスペクト比維持
        targetImage.preserveAspect = preserveAspect;

        string currentSpriteName =
            targetImage.sprite.name;

        // 登録データ検索
        ImageScaleData setting =
            FindSetting(currentSpriteName);

        float scaleMultiplier =
            defaultScaleMultiplier;

        Vector2 positionOffset =
            defaultPositionOffset;

        // 個別設定があれば使用
        if (setting != null)
        {
            scaleMultiplier =
                setting.scaleMultiplier;

            positionOffset =
                setting.positionOffset;
        }

        // ---------------------------------------------
        // まず位置を適用
        // ---------------------------------------------

        _imageRect.anchoredPosition =
            positionOffset;

        // ---------------------------------------------
        // 自動フィット
        // ---------------------------------------------

        if (autoFitToFrame && frameRect != null)
        {
            FitImageInsideFrame(scaleMultiplier);
        }
        else
        {
            // 自動フィットを使わない場合
            _imageRect.localScale =
                Vector3.one * scaleMultiplier;
        }

        Debug.Log(
            $"【ImageScaleFilter】{currentSpriteName} 適用完了 " +
            $"Scale倍率:{scaleMultiplier} " +
            $"Offset:{positionOffset}"
        );
    }

    // =========================================================
    // 枠フィット
    // =========================================================

    /// <summary>
    /// SpriteをframeRect内へアスペクト比を維持して収める。
    /// 個別倍率を掛けても枠からはみ出さない。
    /// </summary>
    private void FitImageInsideFrame(
        float requestedMultiplier)
    {
        if (targetImage == null ||
            targetImage.sprite == null ||
            frameRect == null)
        {
            return;
        }

        Sprite sprite =
            targetImage.sprite;

        Rect spriteRect =
            sprite.rect;

        float spriteWidth =
            spriteRect.width;

        float spriteHeight =
            spriteRect.height;

        if (spriteWidth <= 0f ||
            spriteHeight <= 0f)
        {
            return;
        }

        float frameWidth =
            frameRect.rect.width * framePadding;

        float frameHeight =
            frameRect.rect.height * framePadding;

        if (frameWidth <= 0f ||
            frameHeight <= 0f)
        {
            return;
        }

        // ---------------------------------------------
        // Sprite本来の縦横比
        // ---------------------------------------------

        float spriteAspect =
            spriteWidth / spriteHeight;

        float frameAspect =
            frameWidth / frameHeight;

        float fittedWidth;
        float fittedHeight;

        // 横長Sprite
        if (spriteAspect > frameAspect)
        {
            fittedWidth =
                frameWidth;

            fittedHeight =
                frameWidth / spriteAspect;
        }
        // 縦長Sprite
        else
        {
            fittedHeight =
                frameHeight;

            fittedWidth =
                frameHeight * spriteAspect;
        }

        // ---------------------------------------------
        // 個別倍率
        // ---------------------------------------------

        fittedWidth *= requestedMultiplier;
        fittedHeight *= requestedMultiplier;

        // ---------------------------------------------
        // 枠超過チェック
        //
        // requestedMultiplier が巨大でも
        // 絶対にframeRectからはみ出させない
        // ---------------------------------------------

        float overflowScale = 1f;

        if (fittedWidth > frameWidth)
        {
            overflowScale =
                Mathf.Min(
                    overflowScale,
                    frameWidth / fittedWidth
                );
        }

        if (fittedHeight > frameHeight)
        {
            overflowScale =
                Mathf.Min(
                    overflowScale,
                    frameHeight / fittedHeight
                );
        }

        fittedWidth *= overflowScale;
        fittedHeight *= overflowScale;

        // ---------------------------------------------
        // RectTransformへ反映
        // ---------------------------------------------

        _imageRect.localScale =
            Vector3.one;

        _imageRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            fittedWidth
        );

        _imageRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            fittedHeight
        );
    }

    // =========================================================
    // 設定検索
    // =========================================================

    /// <summary>
    /// Sprite名から個別設定を検索。
    /// </summary>
    private ImageScaleData FindSetting(
        string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
        {
            return null;
        }

        for (int i = 0;
             i < imageSettings.Count;
             i++)
        {
            ImageScaleData setting =
                imageSettings[i];

            if (setting == null)
            {
                continue;
            }

            if (setting.spriteName ==
                spriteName)
            {
                return setting;
            }
        }

        return null;
    }

    // =========================================================
    // 外部操作
    // =========================================================

    /// <summary>
    /// 外部からSpriteを変更して
    /// そのまま自動調整する。
    /// </summary>
    public void SetSprite(Sprite newSprite)
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.sprite =
            newSprite;

        Apply();
    }

    /// <summary>
    /// Sprite切替後などに再計算したい場合。
    /// UnityEventからも呼べる。
    /// </summary>
    public void Refresh()
    {
        Apply();
    }




    /// <summary>
    /// 旧ImageScaleFilterとの互換用。
    /// 古いスクリプトからの呼び出しを維持する。
    /// </summary>
    public void ApplyScaleIfNameMatches()
    {
        Apply();
    }














#if UNITY_EDITOR

    // =========================================================
    // Inspectorテスト
    // =========================================================

    [ContextMenu("現在の画像に設定を適用")]
    private void EditorApply()
    {
        if (targetImage == null ||
            targetImage.sprite == null)
        {
            return;
        }

        _imageRect =
            targetImage.rectTransform;

        if (frameRect == null &&
            targetImage.transform.parent != null)
        {
            frameRect =
                targetImage.transform.parent
                    as RectTransform;
        }

        Apply();
    }

#endif
}