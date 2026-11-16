# SaveClipboard

SaveClipboard 是一款常駐於 Windows 系統匣的剪貼簿歷史工具。它會監聽系統剪貼簿中的文字與圖片，保存可設定數量的歷史項目，讓您透過系統匣圖示、工作列圖示或全域快捷鍵快速開啟面板，點選即可重新複製。

以 **WinUI 3（Windows App SDK 1.8）+ .NET 10** 開發，採未封裝（unpackaged）模式，提供 **x64** 與 **ARM64** 版本。

[下載最新版本](https://github.com/mark216tw/win-saveclipboard-01/releases/latest) ·
[使用指南](docs/USAGE.md) ·
[系統架構](docs/ARCHITECTURE.md) ·
[系統設計](docs/DESIGN.md) ·
[建置與發佈](docs/BUILD.md)

---

## 功能特色

### 剪貼簿歷史

- 監聽純文字與點陣圖圖片；圖片自動轉為 PNG 保存並產生縮圖。
- 重複內容自動移到最上方，不重複佔位。
- 保存上限 10–50 筆（預設 30），只計算未釘選項目；超過時淘汰最舊的未釘選項目。
- 圖片歷史另有 100 MB 總量上限，超過時先淘汰最舊的未釘選圖片。
- 釘選項目固定顯示在最上方，不受保存上限淘汰。
- 可勾選多筆一次釘選、取消釘選或刪除。
- 刪除後 5 秒內可從左下角提示按「復原」救回。
- 「清除全部」需二次確認，會同時清除釘選項目。

### 面板與操作

- 點系統匣圖示、工作列釘選圖示，或按 `Ctrl+Alt+V` 開啟／收起面板。
- 搜尋列即時過濾文字項目；`Esc` 先清除搜尋再關閉。
- 按住標題列空白處可拖曳移動面板，拖曳邊緣可調整大小（最小 320×400 DIP）；位置與大小會記住，下次啟動依螢幕與 DPI 驗證後還原。
- 面板固定置頂；點選項目只複製到剪貼簿，可選擇「選取後不關閉視窗」保留面板。
- 僅手動關閉：點擊其他視窗不會收起面板，按 X、`Esc` 或快捷鍵關閉。
- 「暫停記錄 30 分鐘」可從系統匣選單或面板底部按鈕啟動，到期自動恢復。
- 首次啟動顯示簡短導引，面板底部顯示項目數與圖片用量。

### 個人化

- 顯示模式：跟隨系統、淺色、深色，立即套用。
- 10 款主題顏色：經典藍、森林綠、暮色紫、暖陽橙、玫瑰紅、湖水青、金黃、石墨灰、靛青、珊瑚紅。
- 主題色同時支援淺色與深色模式，並套用至標題列淡色背景。
- 介面文字支援繁體中文與英文資源載入（預設繁體中文）。

### 隱私與資料

- 剪貼簿內容**預設只保存在記憶體**，程式結束即消失。
- 尊重來源應用程式的隱私標記：`ExcludeClipboardContentFromMonitorProcessing` 與 `CanIncludeInClipboardHistory=0` 的內容不會進入歷史。
- 可選「保存歷史到本機（加密）」：整份歷史以 Windows DPAPI（目前使用者）加密後寫入 `%LOCALAPPDATA%\SaveClipboard\history.dat`，預設關閉。
- 診斷日誌不記錄剪貼簿內容，只記錄事件層級與略過原因。
- 單筆文字超過 200 萬字元時不保存。

### 系統整合

- 單一實例：重複啟動時喚起既有面板，不會多開。
- 開機自動啟動（可選），開機啟動時只常駐系統匣，不彈出面板。
- 全域快捷鍵被其他程式占用時，設定頁會提示。
- Explorer 重新啟動後，系統匣圖示會自動恢復。
- 設定檔損毀時自動備份為 `settings.corrupt-*.json` 並以預設值啟動。

## 系統需求

| 項目 | 需求 |
| --- | --- |
| 作業系統 | Windows 10 1809（10.0.17763）以上；Windows 11 建議 |
| 架構 | x64 或 ARM64 |
| .NET | .NET 10 Desktop Runtime（非 Self-Contained 版本需要） |
| Windows App SDK | Windows App SDK 1.8 Runtime（非 Self-Contained 版本需要） |
| 建置工具 | .NET 10 SDK、Windows App SDK 1.8 開發套件 |

> 本專案發佈的 zip 為 **framework-dependent（非 Self-Contained）**，不含 .NET 與 Windows App SDK 執行環境，請先安裝對應的 Runtime。

## 下載與啟動

1. 前往 [Releases](https://github.com/mark216tw/win-saveclipboard-01/releases/latest)。
2. 依裝置架構下載：
   - `SaveClipboard-1.5.1-win-x64.zip`
   - `SaveClipboard-1.5.1-win-arm64.zip`
3. 解壓縮到任一資料夾（例如 `C:\Apps\SaveClipboard`）。
4. 執行 `SaveClipboard.exe`。
5. 程式啟動後會常駐系統匣；按 `Ctrl+Alt+V` 或點系統匣圖示開啟面板。

> 本專案目前未提供程式碼簽章與安裝檔。首次執行可能出現 Windows SmartScreen 提示，請確認檔案來自本專案 Release 頁面後再選擇「仍要執行」。

## 使用方式

| 操作 | 方式 |
| --- | --- |
| 開啟／收起面板 | `Ctrl+Alt+V`，或點系統匣圖示 |
| 只開啟面板 | 點系統匣左鍵、工作列釘選圖示，或重複啟動程式 |
| 搜尋 | 在搜尋列輸入關鍵字；`Esc` 清除 |
| 選取項目 | 點擊項目即可複製到剪貼簿 |
| 多選 | 勾選各項目左側的 CheckBox |
| 釘選 | 勾選後按右上角釘選按鈕 |
| 刪除 | 勾選後按右上角刪除按鈕 |
| 復原刪除 | 刪除後 5 秒內按左下角「復原」 |
| 暫停記錄 | 系統匣右鍵選單「暫停記錄 30 分鐘」或面板底部按鈕 |
| 開啟設定 | 面板右上角齒輪圖示 |
| 結束程式 | 系統匣右鍵選單「結束」 |

## 設定項目

| 設定 | 說明 | 預設 |
| --- | --- | --- |
| 開機自動啟動 | 登入 Windows 後自動常駐系統匣 | 關閉 |
| 選取後不關閉視窗 | 複製項目後保持面板顯示 | 關閉 |
| 顯示模式 | 跟隨系統／淺色／深色 | 跟隨系統 |
| 主題顏色 | 10 款主題色 | 經典藍 |
| 保存歷史到本機（加密） | 使用 DPAPI 加密保存歷史 | 關閉 |
| 保存上限 | 未釘選項目的上限，10–50 筆 | 30 |
| 全域快捷鍵 | `Ctrl+Alt+V`，固定 | — |

面板固定置頂；關閉方式固定為手動，點擊其他視窗不會收起。

設定頁按「儲存」才會寫入設定檔與開機啟動登錄值；按「關閉」會復原本次未儲存的顯示模式與主題變更。

## 資料與隱私

| 路徑 | 內容 | 時機 |
| --- | --- | --- |
| `%AppData%\SaveClipboard\settings.json` | 保存上限、啟動、顯示模式、主題、選取後不關閉、加密保存、面板位置與 DPI | 按「儲存」或結束時 |
| `%AppData%\SaveClipboard\settings.corrupt-*.json` | 損毀設定檔的備份 | 設定檔無法解析時 |
| `%LOCALAPPDATA%\SaveClipboard\history.dat` | DPAPI 加密的完整歷史 | 啟用「保存歷史到本機（加密）」時 |
| `%LOCALAPPDATA%\SaveClipboard\Logs\app.log` | 診斷日誌（不含剪貼簿內容），1 MB 輪替 | 執行期間 |
| `%LOCALAPPDATA%\SaveClipboard\Logs\crash.log` | 未處理例外記錄，1 MB 上限 | 發生崩潰時 |

詳細說明請見 [隱私與資料處理](docs/PRIVACY.md)。

## 建置

```powershell
dotnet build
dotnet test
dotnet format --verify-no-changes
```

產生的偵錯執行檔：

```text
src\SaveClipboard\bin\Debug\net10.0-windows10.0.19041.0\SaveClipboard.exe
```

發佈 framework-dependent 版本：

```powershell
dotnet publish src\SaveClipboard -c Release -r win-x64 -p:SelfContained=false -p:WindowsAppSDKSelfContained=false -p:WindowsPackageType=None -p:PlatformTarget=x64 -p:Platform=x64 -o artifacts\publish\win-x64
dotnet publish src\SaveClipboard -c Release -r win-arm64 -p:SelfContained=false -p:WindowsAppSDKSelfContained=false -p:WindowsPackageType=None -p:PlatformTarget=arm64 -p:Platform=arm64 -o artifacts\publish\win-arm64
```

完整建置、測試與發佈流程請見 [建置與發佈指南](docs/BUILD.md)。

## 專案結構

```text
src/SaveClipboard/           主程式（WinUI 3）
  Views/                     面板與設定頁
  ViewModels/                MVVM ViewModel
  Models/                    設定、歷史項目、主題
  Services/                  剪貼簿、歷史、保存、主題、系統匣等服務
  Helpers/                   P/Invoke、轉換器、資源字串
  Styles/                    樣式與主題資源字典
  Strings/                   多語系資源（zh-Hant、en-US）
tests/SaveClipboard.Tests/   xUnit 單元測試
tools/                       圖示產生與 E2E 檢查腳本
docs/                        使用、架構、設計、建置與隱私文件
```

## 文件

- [文件索引](docs/README.md)
- [使用指南](docs/USAGE.md)
- [系統架構與技術文件](docs/ARCHITECTURE.md)
- [系統設計文件](docs/DESIGN.md)
- [建置與發佈指南](docs/BUILD.md)
- [隱私與資料處理](docs/PRIVACY.md)
- [開發紀錄](docs/DEV_LOG.md)
- [版本歷程](CHANGELOG.md)
- [版本說明](RELEASE_NOTES.md)

## 貢獻

歡迎回報問題與提出改進。請先閱讀 [CONTRIBUTING.md](CONTRIBUTING.md)；安全性問題請依 [SECURITY.md](SECURITY.md) 回報。

## 授權

本專案採用 [MIT License](LICENSE)。

Copyright (c) 2026 mark216tw
