# 安全性政策

## 支援版本

| 版本 | 支援狀態 |
| --- | --- |
| 1.4.x | 接受安全性回報 |
| 1.3.x 及更早 | 不保證修補 |

## 回報安全性問題

請**不要**在公開 Issue 中揭露安全性問題、剪貼簿內容或個人資料。

請透過 GitHub 的私人安全性回報功能：

<https://github.com/mark216tw/win-saveclipboard-01/security/advisories/new>

回報時請提供：

- 受影響版本與 Windows 版本／架構
- 問題類型與影響範圍
- 重現步驟（請去識別化，不要附上真實密碼或機密內容）
- 您認為可能的修正方向（選填）

維護者會在能力範圍內確認問題、評估影響並準備修正。由於本專案為個人維護的開源專案，無法保證固定回應時間。

## 安全設計重點

- 剪貼簿內容預設只存在記憶體，不寫入磁碟。
- 啟用歷史保存時，使用 Windows DPAPI（`CurrentUser` 範圍）加密，僅目前 Windows 使用者可解密。
- 尊重來源應用程式的 `ExcludeClipboardContentFromMonitorProcessing` 與 `CanIncludeInClipboardHistory=0` 標記。
- 診斷日誌不記錄剪貼簿內容。
- 程式不需要系統管理員權限，也不安裝服務或驅動程式。
- 本專案未提供程式碼簽章；請只從官方 GitHub Release 下載執行檔，並自行比對 Release 內容。

## 已知限制

- 本程式無法防止其他具有相同使用者權限的程式讀取剪貼簿；Windows 剪貼簿本身即為使用者層級共用資源。
- DPAPI 加密可防止離線讀取，但無法防禦已取得目前使用者工作階段控制權的惡意程式。

## 敏感資料外洩處理

若您懷疑 `%LOCALAPPDATA%\SaveClipboard\history.dat`、日誌或設定檔含有敏感資料：

1. 在設定頁關閉「保存歷史到本機（加密）」，程式會刪除 `history.dat`。
2. 使用「清除全部」清除記憶體中的歷史。
3. 必要時手動刪除 `%AppData%\SaveClipboard` 與 `%LOCALAPPDATA%\SaveClipboard`。
