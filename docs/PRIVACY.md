# 隱私與資料處理

SaveClipboard 以「預設不落盤、內容不外傳」為設計原則。本文件說明程式會處理哪些資料、存放在哪裡、保存多久，以及如何清除。

## 1. 摘要

- 剪貼簿內容**預設只保存在記憶體**，程式結束即消失。
- 程式**不會**將剪貼簿內容上傳到網路，也沒有帳號、雲端同步或遙測。
- 可選的「保存歷史到本機（加密）」使用 Windows DPAPI 加密，僅目前 Windows 使用者可解密。
- 診斷日誌只記錄事件與長度，**不記錄**剪貼簿文字或圖片。
- 尊重來源應用程式的「不要記錄」標記。

## 2. 處理的資料

| 資料 | 來源 | 用途 | 預設保存 |
| --- | --- | --- | --- |
| 剪貼簿文字 | 系統剪貼簿 | 顯示歷史、重新複製 | 記憶體 |
| 剪貼簿圖片 | 系統剪貼簿 | PNG 保存、縮圖顯示、重新複製 | 記憶體 |
| 設定值 | 使用者操作 | 啟動、顯示、主題、保存上限等 | `%AppData%` |
| 面板位置與大小 | 視窗操作 | 下次開啟還原 | `%AppData%` |
| 診斷日誌 | 程式事件 | 除錯與問題回報 | `%LOCALAPPDATA%` |
| 崩潰記錄 | 未處理例外 | 問題回報 | `%LOCALAPPDATA%` |
| 加密歷史 | 使用者開啟保存 | 跨啟動保留歷史 | `%LOCALAPPDATA%` |

## 3. 儲存位置

### 3.1 設定檔

```text
%AppData%\SaveClipboard\settings.json
```

內容包含：

- 保存上限
- 開機自動啟動
- 顯示模式
- 主題識別碼
- 主視窗關閉方式
- 選取後直接貼上
- 面板置頂
- 是否加密保存歷史
- 是否看過首次導引
- 面板位置、大小與 DPI

設定檔**不含**剪貼簿內容。

### 3.2 損毀設定備份

```text
%AppData%\SaveClipboard\settings.corrupt-yyyyMMddHHmmssfff.json
```

設定檔無法解析時，原檔會保留為此備份，程式以預設值啟動。備份不會自動刪除。

### 3.3 加密歷史

```text
%LOCALAPPDATA%\SaveClipboard\history.dat
```

- 只有在設定頁開啟「保存歷史到本機（加密）」並儲存後才會產生。
- 整份歷史先序列化為 JSON，再以 DPAPI `CurrentUser` 範圍加密。
- 檔案內容為密文；其他 Windows 使用者或離線讀取無法直接解密。
- 關閉保存選項並儲存後，程式會刪除此檔。

### 3.4 日誌

```text
%LOCALAPPDATA%\SaveClipboard\Logs\app.log
%LOCALAPPDATA%\SaveClipboard\Logs\app.log.1
```

- `app.log` 上限 1 MB，超過時輪替為 `app.log.1`。
- 內容包含：剪貼簿更新序號、加入文字長度、加入圖片位元組數、略過原因、隱私標記、面板顯示／隱藏、貼上目標與錯誤。
- **不包含**剪貼簿文字、圖片內容或完整檔案內容。

### 3.5 崩潰記錄

```text
%LOCALAPPDATA%\SaveClipboard\Logs\crash.log
```

- 僅在發生未處理例外時寫入。
- 上限 1 MB。
- 可能包含例外類型、堆疊與模組資訊；送出回報前請自行檢查是否含敏感路徑或資料。

## 4. 隱私標記

程式會檢查剪貼簿格式：

| 格式 | 行為 |
| --- | --- |
| `ExcludeClipboardContentFromMonitorProcessing` | 不記錄 |
| `CanIncludeInClipboardHistory` = 0 | 不記錄 |
| `CanIncludeInClipboardHistory` 非 0 或不存在 | 依一般流程記錄 |

若 WinRT 無法取得格式清單，會改用 Win32 再次檢查上述標記，再決定是否以 `CF_UNICODETEXT` 讀取文字。

## 5. 容量與保留策略

| 項目 | 限制 |
| --- | --- |
| 未釘選文字／圖片項目數 | 10–50 筆（預設 30） |
| 圖片總量 | 100 MB（含縮圖） |
| 單筆文字 | 200 萬字元 |
| 日誌 | 1 MB + 1 MB 輪替 |
| 崩潰記錄 | 1 MB |

- 超過項目上限時，淘汰最舊的未釘選項目。
- 超過圖片容量時，優先淘汰最舊的未釘選圖片。
- 釘選項目不受上述上限淘汰。
- 記憶體歷史在程式結束時消失（除非啟用加密保存）。

## 6. 暫停記錄

- 可暫停 30 分鐘，期間新的剪貼簿內容不進入歷史。
- 暫停狀態只存在記憶體，程式重新啟動後恢復記錄。
- 到期後自動恢復；也可從系統匣選單或面板按鈕手動恢復。

## 7. 資料清除

### 清除歷史

- 面板「清除全部」：清除記憶體中的歷史（含釘選），需二次確認。
- 若啟用加密保存，歷史變更會在 1 秒內寫入 `history.dat`；清除後檔案會更新為空歷史。

### 完全移除資料

1. 從系統匣選單結束 SaveClipboard。
2. 刪除程式資料夾。
3. 刪除：

   ```text
   %AppData%\SaveClipboard
   %LOCALAPPDATA%\SaveClipboard
   ```

4. 若曾開啟「開機自動啟動」，請先在同一設定頁取消並儲存，或手動移除：

   ```text
   HKCU\Software\Microsoft\Windows\CurrentVersion\Run\SaveClipboard
   ```

## 8. 網路與第三方

- 本程式沒有網路功能，不會連線到任何伺服器。
- 不使用分析、追蹤、廣告或當機回報服務。
- 使用 Windows 內建 API：剪貼簿、DPAPI、Shell_NotifyIcon、RegisterHotKey、SendInput。
- 第三方 NuGet 套件（Windows App SDK、CommunityToolkit.Mvvm、Microsoft.Extensions.*）僅在本機執行，不涉及資料傳輸。

## 9. 安全性注意事項

- Windows 剪貼簿是使用者層級共用資源；同一使用者工作階段的其他程式仍可能讀取剪貼簿。
- DPAPI 可防止其他使用者與離線讀取，但無法防禦已控制目前使用者工作階段的惡意程式。
- 自動貼上對高權限視窗會被 UIPI 阻擋；程式只會複製並提示。
- 本程式未提供程式碼簽章；請只從官方 GitHub Release 下載。

## 10. 回報隱私問題

若發現隱私相關問題，請依 [安全性政策](../SECURITY.md) 私下回報，不要在公開 Issue 張貼真實剪貼簿內容或個人資料。
