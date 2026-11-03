# QvInkPreview

> **本パッケージは QvPen の非公式拡張です。** 不具合の報告やご質問を、QvPen の作者（ureishi 氏）へ送ることは、おやめください。お問い合わせは、本パッケージの配布元へお願いします。

QvPen の各ペンの後ろに、現在のインクと同じマテリアル・色・太さの短い線を表示する VPM パッケージです。QvInfoX や QvPalette は不要です。

## セットアップ

QvPalette を使う場合は、QvPalette の Setup（`Tools/Yakigura/QvPalette/Setup`）が、あわせて導入します。個別の操作は要りません。QvPalette を使わない場合は、エディタのスクリプトから `QvInkPreview_AutoSetup.AutoSetup()` を呼びます。シーン内の QvPen ごとにプレビューを一つ作り、`QvInkPreview_Manager` に登録します。再実行しても重複しません。旧 QvPalette の `QppPenPreview` が独自の線を持つペンだけはスキップします。新しい QvPalette の表示アダプターとはプレビューを共用します。

全体の表示切替には `QvInkPreview_Manager._SetAllEnabled(bool)` を呼びます。UI Toggle を使う場合は、Manager の `toggle` に Toggle を割り当て、`onValueChanged` から Manager の UdonBehaviour に `_OnToggleChanged` を送ります。導入処理はシーン内の `QvInkPreviewToggle` という名前の UI Toggle を自動配線します。

ペンごとの上書きには `QvInkPreview_Pen._SetOverrideGradient(Gradient)`、`_SetOverrideWidth(float)`、`_ClearOverride()` を使います。上書き前と解除後は QvPen_PenManager の `colorGradient` / `inkWidth` に追従します。パレット側は設定変更時にこれらを呼べます。

`tailAxis`、`customTailAxis`、`tailDistance`、`upOffset`、`positionOffset` は各ペンの配置調整です。`maxVisibleDistance` が正なら、その距離より遠いペンの表示を止めます（0 なら距離制限なし）。Auto Setup の再実行でも保持します。材質は QvPen の管理者から取得し、実トレイルの MaterialPropertyBlock もコピーするため、スタイル固有のアウトライン色なども反映されます。

## 動作要件

| 項目 | 内容 |
|---|---|
| Unity | 2022.3 |
| VRChat SDK | Worlds 3.5 以上 |
| QvPen | 3.3.0 以上（QvPalette と併用する場合は 3.3.15） |

## 導入方法（VCC）

1. VCC の「Settings → Packages → Add Repository」に、配布元の VPM リスト URL を登録します。 **[URL は公開時に記載]**
2. プロジェクトの「Manage Project」で、**QvInkPreview** の「+」を押します。
3. 上の「セットアップ」を行います。

## 利用規約

[LICENSE](LICENSE) を参照してください。ワールドへの組み込み・公開（商用ワールドを含む）と、自分のワールドに合わせた改変は自由です。再配布、改変版の公開、AI による解析・学習への利用は禁止です。

## クレジット

- **QvInkPreview** — Developed by **Negiyaki**（やきたてぐらふてぃ）
- **QvPen** — Developed by **ureishi 氏** / https://65536.booth.pm/items/1555789
