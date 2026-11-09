# M0／M1 素材提交審查

日期：2026-11-09。範圍：本次 M0／M1 實作的實際暫存版本。新增音效不包含於這次提交。

## 掃描流程調整

使用者明確要求與 Codex 生存者專案一致，採「自動搜尋已知機敏字串＋人工素材審查」。二進位格式本身不再造成自動失敗；仍搜尋實際索引及本機 LFS payload，並輸出 `manual_reviews`。不是依個別檔案設置放行清單，hooks、完整索引、歷史、訊息、LFS 完整性及其他阻擋條件保留。

人工審查不由 hooks 強制驗證。此紀錄只適用下列實際指紋，不代表未來修改版本或整個本機資料夾已通過。

## 二進位素材

| 檔案 | 方法與結果 |
| --- | --- |
| `Assets/Art/Growables/growables-atlas.png` | 內建 imagegen 產生；代理已檢視原圖及遊戲內顯示，使用者另明確確認圖片沒有問題。核對與前次視覺審查版本相同，索引 LFS 指標吻合，沒有另換圖片。 |
| `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` | 與本機已安裝 `com.unity.ugui` 2.0.0 隨附的 TMP Essential Resources 原始 asset 逐位元組比對，相同；索引 LFS 指標吻合。隨附 OFL 授權文件保留。 |
| `Assets/TextMesh Pro/Sprites/EmojiOne.png` | 與同一 TMP Essential Resources 原始 asset 逐位元組比對，相同；另視覺確認為表情圖集，無帳號或機敏文字；索引 LFS 指標吻合。隨附 attribution 保留。 |

上述來源比對直接讀取套件的 unitypackage 封存內容，沒有執行或解壓安裝腳本、沒有網路下載。

內容 SHA-256：

```text
growables-atlas.png  494e85b1c3c52989fb35b013be3138eed6ed20f2e6ab331db0cec0e65671a2a3
LiberationSans.ttf   e5b0af421ea2bfbc1ac8d251d647268087ae82786234c57f757d1f0b90fa8b49
EmojiOne.png         a87b965a701de3dc31d7401e542b1fc946587f912c51d7a26e46ade9f098bd1a
```

## Shader Graph 與自動驗證

四個 TMP Shader Graph 已直接套用文字掃描，並逐位元組比對套件原始 asset，相同；不再因副檔名本身阻擋。

- `TMP_SDF-HDRP LIT.shadergraph`
- `TMP_SDF-HDRP UNLIT.shadergraph`
- `TMP_SDF-URP Lit.shadergraph`
- `TMP_SDF-URP Unlit.shadergraph`

修改後初次實際索引掃描：203 個索引檔案、3 個 LFS payload，0 命中、0 阻擋缺口，3 個二進位人工審查項目如上。提交前會再次依實際索引與訊息執行檢查。

掃描器 17 個合成測試及 hooks 5 個測試通過，涵蓋二進位內機敏字串、未知文字副檔名、不跨無效位元組拼接字串、LFS 缺失／完整性及歷史中的刪除版本。測試不使用真實認證資料。

## 限制

沒有對圖片像素進行 OCR，也沒有解析壓縮 metadata 或字型內部所有表格。來源相同比對證明本機素材與既有套件一致，不是重新驗證套件發布鏈或所有內容的安全性。二進位可讀文字搜尋不能檢查加密、壓縮或所有編碼的資料。

既有日誌命中與本機其他素材不在這次提交範圍；本紀錄不撤銷那些發現，不代表遠端、聊天紀錄或整個工作目錄沒有機敏資料。未執行推送。
