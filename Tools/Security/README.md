# 專案安全檢查

需求：Python 3.12+、Git，以及此 Unity 專案既有的 Git LFS。使用 Python 標準函式庫，沒有額外套件或下載。掃描與啟用程序均不連線；只有正常 Git 推送階段才可能傳送已檢查的 Git／LFS 內容。

## 新 clone 與本機啟用

先審查腳本，然後在專案根目錄執行：

```powershell
python Tools/Security/install_hooks.py
git config --get core.hooksPath
python -B Tools/Security/verify_guards.py
```

有效 hooksPath 應為 `.githooks`。也可用 PowerShell 7 執行 `pwsh -NoProfile -File Tools/Security/Enable-Hooks.ps1`。不修改全域 Git 設定或 PowerShell 執行政策；Windows PowerShell 5.1 若阻擋腳本，使用 Python 入口。

版本控制包含 `.githooks/`、掃描器與文件，但 repository-local 設定及機器專用串接狀態不會隨 clone 傳遞，因此每份 clone 都須啟用。Hook shell 腳本使用 LF，Git 索引須保留可執行位元。

啟用程序會辨識既有標準 LFS hooks 與此專案先前的安全 guards，保留所有原始 hook 檔案，將受審查的 predecessor 位置記錄於 Git 中繼資料的 `security-hooks/state.json`。未知 hooks 或自訂 hooksPath 會停止啟用，須先審查整合。重複啟用會驗證既有狀態，不重複增加 hooks。

pre-commit 檢查實際索引，commit-msg 檢查訊息；pre-push 讀取一次 stdin，先掃描 outgoing 範圍，再將相同位元組與參數傳給保留的 LFS hook。post-checkout、post-commit、post-merge 也保留 LFS 行為。新 clone 若沒有 predecessor，執行既有 Git LFS 命令作為標準行為。任一掃描、串接狀態、必要工具或 predecessor 失敗都會停止對應操作；post hooks 的失敗不能撤回已完成的 Git 操作。原始錯誤輸出可能包含認證資訊，因此只回報安全的失敗摘要。

## 檢查與測試

```powershell
pwsh -NoProfile -File Tools/Security/Check-Secrets.ps1 -Mode all
pwsh -NoProfile -File Tools/Security/Check-Secrets.ps1 -Mode staged
python Tools/Security/check_secrets.py --mode history
python -B Tools/Security/test_security.py
python -B Tools/Security/test_hooks.py
python -B Tools/Security/verify_guards.py
```

- `all`：工作目錄、完整索引、所有本機 refs 可達的提交及訊息；另外檢查指定 Git 連線設定欄位，不顯示 URL 或授權標頭值。
- `worktree`：包括未追蹤／忽略中的相關檔案；列明產生目錄與格式缺口。
- `staged`：實際索引 blob，連同未變動的已追蹤檔案一併檢查，不以工作檔案代替。
- `history`：每筆提交的檔案樹與訊息，包括目前已刪除檔案，以及本機直接指向 tree 的快照 refs；相同 blob／路徑只掃一次。
- `push`：依標準輸入的實際更新清單掃描；新分支或缺少舊遠端物件時，掃描完整可達歷史。非 commit refs／annotated tags 列為待補審查，不靜默通過。
- `message`：以 `--message-file` 指定擬提交訊息。

輸出只有安全的位置、類型、計數與缺口；機敏路徑遮蔽。退出碼 0 表示本次範圍沒有命中或阻擋性缺口，1 表示命中或格式缺口，2 表示檢查未完成。檢查命令每次執行後必須確認退出碼，不能因後續命令成功就忽略掃描失敗。

## 限制與處理

此工具是啟發式基礎防護，無法辨識所有服務憑證、所有個資、任意編碼或加密值。機敏檔名本身要求審查。Library、Temp、obj、.vs 與 .git 一般內容不屬工作目錄掃描；索引、可達歷史與可取得的本機 LFS payload 另行檢查。

未知格式、二進位／圖片、連結、超過 16 MiB 檔案，以及缺失或雜湊不符的 LFS 內容，會阻擋對應範圍通過。須補足審查，不能增加排除或放行來規避結果。完整工作目錄的 `UserSettings/Search.index` 目前仍是缺口；它在忽略範圍、沒有列入本次提交或推送，不能據此宣稱整個本機資料夾已通過。

提交前掃描實際暫存內容、路徑與訊息，推送前掃描實際歷史範圍。疑似命中僅回報安全位置、類型及數量，停止受影響的提交／推送／上傳。`.gitignore` 不保護已追蹤檔案，hooks 不涵蓋手動上傳與其他同步工具；本機掃描不代表遠端或聊天紀錄安全，也不是防竄改的伺服器強制措施。

## 安裝位置與復原

可追蹤防護位於 `.githooks/` 與 `Tools/Security/`；啟用設定僅在此 repository 的 Git config 與 Git 中繼資料內。原有 `.git/hooks` 檔案保持原樣，包括初次部署時的 `pre-push.before-security` LFS 備份。

若日後明確授權移除本次可攜式啟用設定，可由已記錄的前置狀態還原本機 hooksPath，並檢查原有 hooks 是否仍有效，再移除本次串接狀態。本次遷移前沒有自訂 hooksPath，還原後會回到原本 `.git/hooks`。新 clone 的前置狀態可能不同，必須按其實況還原。復原不等於撤回已傳送的資料，也不得用來繞過機敏資料命中。
