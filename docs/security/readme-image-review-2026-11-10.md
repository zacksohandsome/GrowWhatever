# README 展示圖片審查

審查日期：2026-11-10。

- 素材：`docs/images/gameplay.png`，1280 × 720 PNG。
- 來源：本機遊戲驗證截圖 `docs/local/validation/m3-goal-complete.png`，直接複製，未修改像素。
- SHA-256：`61640f0855d21784893e95e05e7e0e3584049cae3eac4995fd2a7d1d5376aec1`。
- 方法：檢視完整圖片，確認為農場遊戲介面、四種作物及遊戲資源數值；比對來源與展示檔案雜湊相同。以 Python 標準函式庫檢查 PNG 區塊，只有 IHDR、IDAT、IEND，沒有附加文字或 EXIF 區塊。
- 結果：畫面未發現憑證、帳號、個資、桌面或編輯器資訊。實際索引與推送範圍另依專案工具掃描。
- 限制：畫面與區塊檢查不代表對隱寫資料或所有可能編碼的全面檢測。素材本身的來源說明見 [素材來源](../design/asset-sources.md)。
