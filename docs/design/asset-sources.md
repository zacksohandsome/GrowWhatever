# 素材來源與使用範圍

記錄日期：2026-11-10。素材用途為 GrowWhatever 遊戲呈現與操作回饋；素材各自的授權不因加入專案而被改成另一種授權。

## 圖片

`Assets/Art/Growables/growables-atlas.png` 由內建 imagegen 生成，無第三方參考圖片。包含四種植物各一張成長／成熟圖及空田，共九個 Sprite。畫面與固定互動區分離。圖片已在實際遊戲尺寸檢查，原始圖集與 `.meta` 保留供編輯。

## 音效

使用者提供於 `Assets/Audio/SFX/`：

| 檔案 | 用途 | 來源依據 |
| --- | --- | --- |
| `click.wav` | 選種、面板、一般按鈕 | 使用者先前專案的 AI 生成音效 |
| `sowing.mp3` | 播種成功 | 使用者提供，MP3 來源指向効果音ラボ |
| `harvest.wav` | 收成成功 | 使用者先前專案的 AI 生成音效 |
| `buyOrSell.wav` | 購買／販售成功 | 使用者先前專案的 AI 生成音效 |
| `UnableOperate.wav` | 無法操作回饋 | 使用者先前專案的 AI 生成音效 |
| `ResearchSuccess.mp3` | 加工完成（依使用者指定） | 使用者提供，効果音ラボ |
| `UnlockOrLevelUp.wav` | 研究／升級完成 | 使用者先前專案的 AI 生成音效 |
| `MissionClear.mp3` | 首版目標第一次完成 | 使用者提供，効果音ラボ |

MP3 的使用者指定來源為[効果音ラボ按鈕／系統音頁](https://soundeffect-lab.info/sound/button/)。已查閱[官方使用條款](https://soundeffect-lab.info/agreement/)：允許免費及商業內容使用，列舉應用程式操作音為可用案例；禁止將音效獨立作為素材再配布及其他條款所列用途。本專案將音效用於遊戲回饋，不另作可下載音效素材包。

目前依據使用者提供的來源說明，未逐一辨識重新命名 MP3 在來源頁的原始音效編號；WAV 的生成模型與原始提示詞未提供。這些是來源紀錄的限制，不將素材描述成 CC0 或聲稱已完成全面法律審查。公開前應依實際待發布內容與當時條款再核對。

## 字型與 TMP 資源

`Assets/TextMesh Pro/` 來自已安裝 `com.unity.ugui` 2.0.0 隨附 TMP Essential Resources。LiberationSans 的 OFL 文件、EmojiOne attribution 與其他隨附文件保留於原目錄。二進位來源比對與圖片審查見 [M0／M1 素材審查](../security/material-review-2026-11-09.md)。

目前介面使用 LiberationSans 與英文文字，未加入 CJK 字型或多語系套件。
