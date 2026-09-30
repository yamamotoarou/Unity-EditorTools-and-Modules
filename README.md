# Unity Editor Tools & UI Modules

Unity / C# を使用したゲーム開発の中で実際に生じた課題を解決するために制作した、
**Unity Editor拡張・UI制御・開発ワークフロー改善用スクリプト集**です。

本リポジトリはポートフォリオ用途として、公開可能な代表スクリプトのみ掲載しています。

製品コード全体については、知的財産およびプロジェクト固有情報の保護を目的として非公開としています。

現在、以下の **8種類のカスタムスクリプト**を収録しています。

---

## 収録スクリプト

### 1. `UISfHighlightController.cs`
### UI選択・ハイライト演出制御

ボタンの選択時やマウスポインターのホバー時に、
動的な視覚エフェクトを追加するUIコンポーネントです。

- ボタン周囲を周回するパーティクル演出
- サイン波を利用した透明度のパルスアニメーション
- UI外周の微細な拡大・縮小演出
- `ISelectHandler`
- `IDeselectHandler`
- `IPointerEnterHandler`
- `IPointerExitHandler`

などのUnity EventSystemインターフェースを利用し、
入力方式に応じてハイライトエフェクトを制御します。

---

### 2. `ButtonColorCopier.cs`
### Button ColorBlock コピー／ペースト用Editor拡張

Unity UIのButtonに対するコンテキストメニューを拡張し、
`ColorBlock` の設定値を別のButtonへコピーできるEditorツールです。

- Buttonの `ColorBlock` をコピー
- コピーしたColor設定を別Buttonへ適用
- Color関連設定のみを個別に転送
- `Undo.RecordObject` によるUndo対応
- 大量のUIボタン設定時の反復作業を削減

UIデザイン調整時に発生する、
同一カラー設定の手動入力を効率化することを目的としています。

---

### 3. `PlayModeTransformSaver.cs`
### Play Mode中のTransform・GameObject保存ツール

Play Mode中に調整したオブジェクトの状態を保存し、
Edit Modeへ戻った後も変更内容を維持できるEditor拡張です。

ショートカットキーを実行すると、
選択中のオブジェクトを一時保存したうえでPlay Modeを終了し、
Edit Mode復帰後に自動的に状態を復元します。

#### 既存オブジェクト

Play Mode開始前から存在していたGameObjectについて、

- Position
- Rotation
- Scale

を保存・復元します。

Prefab Instanceの場合は変更内容も記録されます。

#### RectTransform対応

通常の3D Transformだけでなく、
Unity UIの `RectTransform` にも対応しています。

以下の情報を保存・復元します。

- `anchorMin`
- `anchorMax`
- `pivot`
- `anchoredPosition3D`
- `sizeDelta`
- `offsetMin`
- `offsetMax`

#### Play Mode中に生成したGameObjectの持ち帰り

Play Mode中に新規作成・配置されたGameObjectは、
一時Prefabとして退避されます。

Edit Mode復帰後にシーンへ再生成し、
Prefabとのリンクを解除した独立GameObjectとして復元します。

復元されたオブジェクトには `[Restored]` を付与し、
Play Modeから持ち帰ったオブジェクトであることを識別できるようにしています。

#### その他の安全対策

- 親子オブジェクト同時選択時は親のみ保存
- 子オブジェクトの二重生成を防止
- 一時Prefab保存フォルダを処理終了後に自動削除
- Unityの `InstanceID` を利用して既存オブジェクトを判定
- 外部GameObjectへの参照が切れる可能性がある場合はConsoleへ警告

Play Mode上で試行錯誤した配置結果を、
Edit Modeで再入力する作業を削減するためのツールです。

---

### 4. `ButtonColorSyncEditor.cs`
### UI Button色・Text色同期制御

Buttonの状態に応じて、
子要素のText色を自動的に同期させるUI用カスタム制御です。

- Normal
- Highlighted
- Pressed
- Selected
- Disabled

などのButton状態に応じて、
指定された色へTextをクロスフェードさせます。

以下のTextコンポーネントへ対応しています。

- `TextMeshProUGUI`
- Unity標準 `Text`

また、

- `ButtonBlinkHover`
- `ButtonTimeOutLock`

など特定コンポーネントが存在する場合には、
演出処理との競合を避けるため自動的に同期処理をスキップします。

---

### 5. `ImageScaleFilter.cs`
### UI画像自動フィット・個別補正

異なる解像度やアスペクト比を持つSpriteを、
指定したUIフレーム内へ自動的に収める表示補正コンポーネントです。

- Spriteのアスペクト比を維持した自動フィット
- UIフレームサイズに応じた自動スケーリング
- `framePadding` による余白指定
- Spriteごとの `scaleMultiplier`
- Spriteごとの `positionOffset`
- フレーム外へのはみ出し防止
- 個別倍率適用後の自動再補正

外部スクリプトやUnityEventから利用できる、

- `SetSprite()`
- `Refresh()`

も公開しており、
実行中のキャラクター画像変更にも対応しています。

---

### 6. `UIFocusKeeper.cs`
### マウス・ゲームパッドUIフォーカス統合制御

マウスとゲームパッドを併用するUIにおいて発生する、
選択状態の不整合やフォーカス消失を防止するコンポーネントです。

- マウスホバー時に対象Selectableへフォーカスを同期
- `EventSystem.currentSelectedGameObject` を自動更新
- 指定した親GameObject以下のSelectableを自動検索
- `HoverToSelect` コンポーネントを自動付与
- 動的生成されたUIに対する再スキャン
- 最後に選択されていたUIを記憶
- 背景クリック等でフォーカスが消失した場合に自動復元

以下の状態ではフォーカスを復元しません。

- 非アクティブなGameObject
- `interactable == false` のSelectable

画面遷移や操作不可状態を考慮しながら、
マウスとゲームパッド双方で一貫したUI操作を維持します。

---

### 7. `ButtonBlinkLoop.cs`
### UI選択演出・アニメーション制御

マウスホバーやキーボード／ゲームパッドによる選択状態に応じて、
ボタンの色・透明度・スケールを統合的に制御するUI演出コンポーネントです。

- Mouse HoverとEventSystem Selectionの双方に対応
- マウス・キーボード・ゲームパッドで共通の演出を適用
- 選択中のText色変更
- 周期的なAlpha点滅
- Button Imageへの色・Alpha連動
- TextMeshPro `VertexGradient` 対応
- 選択時の拡大アニメーション
- 選択解除時の縮小アニメーション
- `AnimationCurve` によるEasing設定
- Overshoot / Bounce系アニメーション対応
- 選択途中解除時の自然な縮小遷移
- 選択中のPulseアニメーション
- `Time.unscaledDeltaTime` 使用

`Time.timeScale = 0` の状態でもUIアニメーションを継続できます。

また、Buttonが `interactable = false` になった場合には、
Unity標準のDisabled表現を妨げないよう通常状態へ自動復帰します。

実行時に利用できる、

- `ClassChangeButtonColor()`
- `RefreshBaseScale()`

も用意しており、
動的なカラー変更やUIサイズ変更にも対応しています。

---

### 8. `PlayModeOperationKey.cs`
### Play Mode開始／停止ショートカット

Unity Editor上でのテストプレイを高速化するための
開発ワークフロー改善ツールです。

デフォルトでは、

- **F8：Play Mode開始**
- **F9：Play Mode停止**

として動作します。

開始と停止を別キーへ割り当てることで、
停止処理中の連続入力によって誤ってPlay Modeを再開始する事故を防止しています。

#### Game Viewからの停止

Play Mode開始時に、
Hierarchyへ表示されない監視用GameObjectを自動生成します。

このオブジェクトがF9入力を監視するため、
Game Viewを操作中でも即座にPlay Modeを終了できます。

監視オブジェクトには `DontDestroyOnLoad` を適用しているため、
シーン切り替え後も停止キーを利用できます。

#### Editorウィンドウからの操作

Unityの `MenuItem` ショートカットも併用しており、

- Hierarchy
- Inspector
- Scene View

などEditor側へフォーカスがある状態でも、
F8 / F9によるPlay Mode操作が可能です。

#### Legacy Input / New Input System対応

Game Viewでの停止入力について、

- Legacy Input Manager
- New Input System

の双方へ対応しています。

#### Time Scale非依存

入力監視には `Update()` を使用しているため、

```csharp
Time.timeScale = 0;
