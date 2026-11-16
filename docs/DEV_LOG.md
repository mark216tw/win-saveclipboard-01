# 開發紀錄

最新紀錄放最上面。每次開發工作都要更新，不要事後補寫。

---

## 2026-11-16 - v1.5.2

### 目標
將應用程式圖示改為滿版背景，避免圓角外的透明留白造成標題列、工作列與 Alt+Tab 顯示不一致。

### 變更
- `tools/generate-icon.ps1`：繪製前改以森林綠（`#2E6B3A`）填滿整個畫布，移除透明背景；圖示維持白色剪貼簿與綠色閃電圖案
- `src/SaveClipboard/Assets/AppIcon.ico`、`AppIcon-256.png`：重新產生（16/24/32/48/64/128/256，32bpp PNG 內嵌），所有尺寸四角皆為不透明綠色
- 版本 `1.5.2`（`AssemblyVersion` 維持 `1.5.0.0`）

### 問題與解法
- 直接將透明像素填綠會破壞邊緣反鋸齒 → 改為在繪圖前填滿綠色畫布，讓後續圖形正常疊合

### 待辦／已知限制
- 尚未實機確認滿版圖示在 Windows 10 1809 工作列縮小後的辨識度

---

## 2026-11-16 - v1.5.1

### 目標
從歷史項目複製時不改動原項目內容與順序，並讓「已複製文字／圖片」提示套用目前主題色。

### 變更
- `Services/ClipboardChangeGate.cs`：自我更新抑制改為寫入前後序號範圍（`ExpectOwnChange(start, end)`），範圍內可重複消費，範圍外與序號 0 不消費
- `Services/ClipboardWriterService.cs`：`SetContent` 前後各讀一次 `GetClipboardSequenceNumber`，以範圍標記本程式寫入，避免同一筆寫入產生多次更新通知時再次加回、更新時間或移動原項目
- `Views/HistoryPanelWindow.xaml`：`CopyToast` 改為淡主題底（`AccentSubtleBrush`）、主題色邊框與文字（`AccentBrush`）；`UndoToast` 維持中性配色
- `tests/SaveClipboard.Tests/ClipboardChangeGateTests.cs`：改寫為序號範圍語意，新增範圍內重複消費、範圍外放行、序號 0 與 `Clear` 等案例
- 版本 `1.5.1`（`AssemblyVersion` 維持 `1.5.0.0`）

### 問題與解法
- **複製後項目被更新**：同一筆自我寫入可能先後送出多次 `WM_CLIPBOARDUPDATE`，原本只記錄最後一個期待序號，較早的序號會落入擷取流程並經 `ClipboardHistoryService.Add` 重新加入、更新 `CopiedAt` 與排序；改以寫入前後範圍一次涵蓋，外部真正複製的序號仍在範圍外會正常記錄
- **提示配色在深色模式可讀性**：沿用 `ThemeService` 依顯示模式產生的 `AccentBrush`／`AccentSubtleBrush`，不另外在 XAML 寫死顏色

### 待辦／已知限制
- 需實機確認從歷史連續複製文字與圖片後，清單順序與複製時間不變
- 高對比模式沿用系統強調色，仍需人工確認提示文字可讀性

---

## 2026-11-16 - v1.5.0

### 目標
移除面板鍵盤導覽與自動貼上流程，讓點選項目只負責寫入剪貼簿；保留 `Esc`、全域快捷鍵與「選取後不關閉視窗」。

### 變更
- `Services/KeyboardNavigationService.cs`：刪除；`HistoryPanelWindow` 只保留 `Esc` 清除搜尋／關閉設定或面板
- `Views/HistoryPanelWindow.xaml.cs`：移除 Shift／Ctrl 點擊多選、`Delete` 鍵刪除與焦點移動；點擊項目直接呼叫 `CopyCommand`
- `Services/PasteAutomationService.cs`、`Helpers/ForegroundWindowTracker.cs`、`Helpers/PasteTargetRules.cs`、`Helpers/PasteTargetResolver.cs`：刪除
- `Helpers/NativeMethods.cs`：移除 `SendInput`、`SendPasteShortcut`、`SendEnterKey`、`INPUT` 結構與前景事件 hook 等不再使用的互通宣告
- `App.xaml.cs`：移除前景追蹤器與貼上提示回呼
- `ViewModels/HistoryPanelViewModel.cs`：移除 `PasteOnSelect`、`AutoAppendEnter` 與 `ItemCopied`；保留 `KeepPanelOpenAfterSelect`
- `Models/AppSettings.cs`、`ViewModels/SettingsViewModel.cs`：移除兩個貼上相關設定；舊設定檔欄位由 JSON 忽略
- `Views/HistoryPanelWindow.xaml`、`Strings/zh-Hant`、`Strings/en-US`：移除兩個設定項目與貼上失敗字串，更新搜尋 ToolTip 與首次導引
- 測試：更新 `AppSettingsTests`、`SettingsServiceTests`，刪除 `PasteTargetResolverTests`
- 文件與版本：更新 `README.md`、`docs/USAGE.md`、`docs/DESIGN.md`、`docs/ARCHITECTURE.md`、`docs/PRIVACY.md`、`SECURITY.md`、`docs/BUILD.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`；版本提升至 `1.5.0`（`AssemblyVersion`／`FileVersion` 1.5.0.0）

### 問題與解法
- **設定頁保留項目**：「選取後不關閉視窗」與自動貼上原本共用流程；移除貼上後仍保留此設定，`CopyAsync` 成功後只依它決定是否收起面板
- **舊設定相容**：`PasteOnSelect`、`AutoAppendEnter` 不在 `AppSettings` 後，反序列化會忽略未知欄位，不需遷移
- **鍵盤焦點行為**：移除自訂鍵盤處理後，清單不再攔截方向鍵與 `Enter`；`Esc` 改由視窗直接處理，避免依賴已刪除的服務

### 待辦／已知限制
- 需實機確認移除鍵盤導覽後，滑鼠點擊、勾選多選、釘選與刪除流程正常
- 系統匣、工作列與 `Ctrl+Alt+V` 三種開啟方式仍需人工回歸

---

## 2026-11-16 - v1.4.9

### 目標
修正「選取後直接貼上」在面板已開啟時，無法辨識剛開啟的新視窗、改貼到前一個視窗的問題。

### 變更
- `Helpers/PasteTargetRules.cs`：新增 `IsTrackerCandidate`（不檢查 `IsWindowVisible`），可見性留到實際貼上前用 `IsValidTarget` 驗證
- `Helpers/ForegroundWindowTracker.cs`：事件送達時改以 `GetForegroundWindow()` 取最新前景，只有殼層時才退回事件 hwnd；改用寬鬆候選規則；新增 `ForegroundChanged` 事件，值變更才觸發
- `Helpers/PasteTargetResolver.cs`（新）：候選排序與有效性挑選抽成純函式
- `Services/PasteAutomationService.cs`：訂閱 `ForegroundChanged` 即時更新目標；貼上目標改在隱藏面板前決定，隱藏後只作確認並以「預定目標 → 目前前景 → 追蹤值 → 記錄值」依序驗證；候選值寫入 Debug 日誌
- `Views/HistoryPanelWindow.xaml.cs`：刻意隱藏面板期間不執行失焦目標更新，避免系統還原的視窗覆寫已決定的目標
- `tests/SaveClipboard.Tests/PasteTargetResolverTests.cs`（新）：驗證候選排序、跳過無效值與零值、無候選時回傳零
- `docs/DESIGN.md`：D-08 補充追蹤不檢查可見性的原因
- 版本提升至 `1.4.9`（`AssemblyVersion` 維持 `1.4.0.0`、`FileVersion` 1.4.9.0）

### 問題與解法
- **新視窗被漏記**：`app.log` 顯示 `13:12:47` 失焦時抓到新視窗 `0x5770104`，但貼上時退回舊視窗 `0x724430`。根因是新視窗的 `EVENT_SYSTEM_FOREGROUND` 常在標記為可見前送達，追蹤器以 `IsWindowVisible` 判斷而丟棄，值停留在舊視窗
- **隱藏面板才解析目標**：舊流程在隱藏後才解析，且以隱藏前的快照當備援；隱藏造成的失焦／系統還原視窗會蓋掉剛抓到的新視窗。改為隱藏前先決定目標，隱藏只作確認
- **只靠失焦事件**：面板保持顯示時，只有 `Deactivated` 會更新目標，事件比前景切換早到就留下舊值；改由追蹤器事件即時同步

### 待辦／已知限制
- 需實機確認：面板開著 → 開新視窗 → 回面板點項目，會貼到新視窗
- 若特定 App 的新視窗取得前景後立即關閉，仍可能退回舊視窗；可用 `app.log` 的「解析貼上目標」候選值判斷
- 快捷鍵／系統匣／工作列三種叫出方式仍需人工回歸

---

## 2026-11-16 - v1.4.8

### 目標
移除面板置頂與主視窗關閉設定，面板固定置頂並僅能手動關閉；新增「自動附加 Enter 鍵」與「選取後不關閉視窗」，並整理啟動區選項順序。

### 變更
- `Models/AppSettings.cs`：移除 `CloseBehavior`、`AlwaysOnTop`，新增 `AutoAppendEnter`、`KeepPanelOpenAfterSelect`（皆預設 `false`）；同步 `Clone`／`CopyFrom`／`Normalize`
- `Models/WindowCloseBehavior.cs`：刪除
- `ViewModels/SettingsViewModel.cs`：移除關閉方式與置頂草稿，新增兩個選項草稿與儲存／重設流程
- `Views/HistoryPanelWindow.xaml`：啟動區順序改為開機自動啟動、選取後直接貼上、自動附加 Enter 鍵、選取後不關閉視窗；移除置頂與主視窗關閉區塊
- `Views/HistoryPanelWindow.xaml.cs`：`OverlappedPresenter.IsAlwaysOnTop` 固定為 `true`；移除失焦自動隱藏計時器與相關狀態；貼上流程改依 `KeepPanelOpenAfterSelect`
- `Services/KeyboardNavigationService.cs`：`Esc` 在搜尋空白時關閉設定頁或面板，不再依關閉模式判斷
- `Services/PasteAutomationService.cs`、`Helpers/NativeMethods.cs`：自動貼上成功後可選擇以 `SendInput` 追加 `Enter`；失敗時記錄並提示
- `ViewModels/HistoryPanelViewModel.cs`：複製成功後依「選取後不關閉視窗」決定是否收起
- `Helpers/PanelVisibilityRules.cs`、`tests/SaveClipboard.Tests/PanelVisibilityRulesTests.cs`：刪除，判斷已簡化為單一設定
- `Strings/zh-Hant`、`Strings/en-US`：新增兩個選項與 Enter 失敗提示，移除置頂／關閉方式字串
- 測試：更新 `AppSettingsTests`、`SettingsServiceTests`，驗證新欄位預設值、複製與設定檔往返
- 文件與版本：更新 `README.md`、`docs/USAGE.md`、`docs/DESIGN.md`、`docs/ARCHITECTURE.md`、`docs/PRIVACY.md`、`docs/BUILD.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`；版本提升至 `1.4.8`（`AssemblyVersion` 維持 `1.4.0.0`、`FileVersion` 1.4.8.0）

### 問題與解法
- **固定置頂後貼上目標切換**：面板仍在最上層，但不再依置頂狀態決定是否隱藏；改由「選取後不關閉視窗」控制，未勾選時先隱藏面板再貼回前一個視窗
- **Esc 與僅手動關閉的衝突**：保留 `Esc` 作為手動關閉方式；有搜尋內容時先清除搜尋，再按一次才關閉
- **舊設定相容**：舊版 `settings.json` 的 `CloseBehavior`／`AlwaysOnTop` 由 JSON 反序列化忽略，不需遷移

### 待辦／已知限制
- 需實機確認固定置頂下，自動貼上與追加 Enter 在不同權限視窗的行為
- 設定頁新選項的鍵盤與 UIA 操作需人工驗證

---

## 2026-11-16 - v1.4.7

### 目標
面板是無邊框視窗，原本只能調整大小、無法移動。讓使用者可按住標題列拖曳移動，並確認位置與大小會被記住。

### 變更
- `Views/HistoryPanelWindow.xaml`：`HeaderBar` 加入 `PointerPressed`／`PointerMoved`／`PointerReleased`／`PointerCaptureLost`
- `Views/HistoryPanelWindow.xaml.cs`：以指標螢幕座標計算位移並呼叫 `AppWindow.Move`；按在按鈕等互動控制項時不啟動拖曳；拖曳結束立即 `PersistBounds`
- `Directory.Build.props`：版本提升至 `1.4.7`（`AssemblyVersion` 維持 `1.4.0.0`、`FileVersion` 1.4.7.0）
- `CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/USAGE.md`：補充移動面板操作說明
- `README.md`、`docs/BUILD.md`：下載檔名與發佈範例版本更新為 1.4.7

### 問題與解法
- 無邊框視窗沒有系統標題列可拖曳，因此自行捕捉指標並換算螢幕像素位移；以 `RasterizationScale` 換算可跨 DPI 螢幕移動
- 拖曳區含標題列內的按鈕時會誤觸移動，改以來源元素往上找 `ButtonBase` 等控制項排除
- 原本只在隱藏面板或結束程式時保存位置，拖曳後直接結束程式可能遺失新位置，因此拖曳結束也保存一次（值未變時不寫檔）

### 待辦
- 實機確認多螢幕不同 DPI 的拖曳與還原行為
- 觸控拖曳尚未實機驗證

---

## 2026-11-16 - v1.4.6

### 目標
將 App 圖示改為透明底「剪貼簿＋置中閃電」，提升剪貼簿與快速叫回的辨識度，並兼顧淺色／深色工作列。

### 變更
- `tools/generate-icon.ps1`：改為透明背景、森林綠剪貼簿外框、白色紙面與夾子、置中綠色閃電；16/24 px 使用較寬的簡化閃電
- `src/SaveClipboard/Assets/AppIcon.ico`、`AppIcon-256.png`：重新產生 16/24/32/48/64/128/256 px
- `Directory.Build.props`：版本提升至 `1.4.6`（`AssemblyVersion` 維持 `1.4.0.0`、`FileVersion` 1.4.6.0）
- `CHANGELOG.md`、`RELEASE_NOTES.md`：新增 1.4.6 紀錄

### 問題與解法
- **16px 閃電過細**：直接縮放後只剩 1–2px，中央取樣幾乎看不到；16/24 px 改用較寬的多邊形並略微放大，維持可辨識的鋸齒形狀
- **透明底在淺色背景消失**：改採森林綠外框＋白色紙面的雙色結構，淺色靠綠框、深色靠白紙辨識

### 驗證結果
- `AppIcon-256.png`：四角透明；外框 `#2E6B3A`、紙面與夾子 `#FFFFFF`、閃電 `#2E6B3A`
- `AppIcon.ico` 含 7 個尺寸（16、24、32、48、64、128、256 px）
- 以像素分析確認 16px frame 保有剪貼簿輪廓與閃電；24px 以上加入夾子凹槽
- 建置輸出的 PNG 與 exe 內嵌圖示取樣結果為新圖示；`FileVersion` 為 `1.4.6.0`
- `dotnet build`：0 個警告 0 個錯誤
- `dotnet test --no-build`：101/101 通過
- `dotnet format --verify-no-changes --no-restore`：通過

### 待辦
- 實機檢視 16px 系統匣與工作列在淺色、深色、高對比模式下的效果
- 既有 `artifacts/` 發佈檔未重新產生，需要時另行 publish

---

## 2026-11-16 - v1.4.5

### 目標
將 App 圖示底色由黃色改為森林綠，並重新產生所有尺寸的圖示資產。

### 變更
- `tools/generate-icon.ps1`：背景色改為 `#2E6B3A`（森林綠），更新註解
- `src/SaveClipboard/Assets/AppIcon.ico`、`AppIcon-256.png`：重新產生 16/24/32/48/64/128/256 px
- `Directory.Build.props`：版本提升至 `1.4.5`（`AssemblyVersion` 維持 `1.4.0.0`、`FileVersion` 1.4.5.0）
- `CHANGELOG.md`、`RELEASE_NOTES.md`：新增 1.4.5 紀錄

### 問題與解法
- 無；沿用既有多尺寸產生腳本與 PNG 壓縮 ICO 流程

### 驗證結果
- `AppIcon-256.png` 底色取樣為 `#2E6B3A`、圓角外透明
- `AppIcon.ico` 含 7 個尺寸（16、24、32、48、64、128、256 px）
- 建置輸出的 PNG 與 exe 內嵌圖示取樣同為 `#2E6B3A`；`FileVersion` 為 `1.4.5.0`
- `dotnet build`：0 個警告 0 個錯誤
- `dotnet test --no-build`：101/101 通過
- `dotnet format --verify-no-changes --no-restore`：通過

### 待辦
- 實機檢視淺色、深色與高對比工作列／系統匣顯示效果
- 既有 `artifacts/` 發佈檔未重新產生，需要時另行 publish

---

## 2026-11-16 - v1.4.4

### 目標
補齊專案文件並建立公開 Release：以繁體中文撰寫專案說明、使用指南、系統架構與技術文件、系統設計文件、建置與發佈指南、隱私與資料處理，採用 MIT License，並提供 x64 與 ARM64 的非 Self-Contained zip。

### 變更
- `README.md`：改寫為完整專案說明、功能、系統需求、下載、操作、設定、資料位置、建置與文件索引
- `LICENSE`（新）：MIT License
- `CONTRIBUTING.md`、`SECURITY.md`（新）：貢獻與安全性政策
- `docs/README.md`（新）：文件索引
- `docs/USAGE.md`（新）：安裝、操作、快捷鍵、設定、常見問題與限制
- `docs/ARCHITECTURE.md`（新）：技術堆疊、專案結構、DI、剪貼簿管線、執行緒模型、P/Invoke、生命週期
- `docs/DESIGN.md`（新）：需求、使用案例、介面、狀態、資料模型、錯誤處理、設計決策
- `docs/BUILD.md`（新）：建置、測試、格式化、x64／ARM64 發佈、打包與 Release 流程
- `docs/PRIVACY.md`（新）：資料位置、DPAPI、日誌、隱私標記、清除方式
- `Strings/zh-Hant/Resources.resw`、`Strings/en-US/Resources.resw`：關於頁授權改為 MIT License
- `Directory.Build.props`：版本 `1.4.4`（`AssemblyVersion` 維持 1.4.0.0、`FileVersion` 1.4.4.0）
- `CHANGELOG.md`、`RELEASE_NOTES.md`：新增 1.4.4 紀錄

### 問題與解法
- **ARM64 發佈與 csproj 平台設定衝突**：`Platforms` 僅列 x64，發佈時以 `-p:PlatformTarget=arm64 -p:Platform=arm64` 覆寫，並將輸出導向 `artifacts`；文件已註明若要 IDE 切換需調整 `Platforms`
- **非 Self-Contained 需求**：明確設定 `SelfContained=false` 與 `WindowsAppSDKSelfContained=false`，zip 不含 .NET 與 Windows App SDK Runtime，文件與版本說明均標示需先安裝 Runtime

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：101/101 通過
- `dotnet format --verify-no-changes`：通過
- x64／ARM64 `dotnet publish`：成功
- 兩份 zip 已上傳 GitHub Release v1.4.4

### 待辦／已知限制
- zip 未進行程式碼簽章，首次執行會出現 SmartScreen 提示
- 未在 Windows 10 1809 與 Windows on ARM 實機完整驗證
- 未提供安裝檔與 MSIX

---

## 2026-11-15 - v1.4.3

### 目標
讓主頁與設定頁的標題列背景跟隨主題顏色，但以淡色調呈現，強化頁面層次。

### 變更
- `Styles/Themes/CommonBrushes.xaml`：新增 `TitleBarBrush`（預設 15% 主題色）
- `Services/ThemeService.cs`：依目前主題色設定 `TitleBarBrush`（Alpha 0x26）；高對比模式設為透明
- `Views/HistoryPanelWindow.xaml`：`HeaderBar` 與設定頁標題列加上 `Background="{ThemeResource TitleBarBrush}"`
- 版本 `1.4.3`（`AssemblyVersion` 維持 1.4.0.0、`FileVersion` 1.4.3.0）

### 問題與解法
- 標題列使用半透明主題色疊在頁面背景上，淺色／深色模式自動得到正確色調，不需另外混色計算

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：101/101 通過
- `dotnet format --verify-no-changes`：通過

### 待辦／已知限制
- 未在 Windows 10 1809 實機驗證
- 標題列濃淡需實際目視確認，Alpha 值可於 `ThemeService` 調整

---

## 2026-11-15 - v1.4.2

### 目標
把「選取後直接貼上」移到「面板置頂」下方，讓相關的貼上／置頂選項集中。

### 變更
- `Views/HistoryPanelWindow.xaml`：`PasteOnSelectCheckBox` 與說明文字移到「啟動」區塊的「面板置頂」說明下方；移除原本的「貼上」區塊
- 版本 `1.4.2`（`AssemblyVersion` 維持 1.4.0.0、`FileVersion` 1.4.2.0）

### 問題與解法
- 無

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：101/101 通過
- `dotnet format --verify-no-changes`：通過

### 待辦／已知限制
- 未在 Windows 10 1809 實機驗證

---

## 2026-11-15 - v1.4.1

### 目標
調整設定頁選項順序，並修正「面板置頂＋選取後直接貼上」時面板會先隱藏的問題；改為貼上期間保持面板顯示在最上層。

### 變更
- `Views/HistoryPanelWindow.xaml`：「歷史保存」移到「保存上限」上方；「面板置頂」移到「啟動」區塊的「開機自動啟動」下方
- `Helpers/PanelVisibilityRules.cs`（新）：集中判斷貼上期間是否保持面板顯示、複製後是否自動關閉
- `Services/PasteAutomationService.cs`：新增 `keepPanelVisible` 判斷；保持顯示時不隱藏面板，直接以前一個視窗為貼上目標
- `Views/HistoryPanelWindow.xaml.cs`：貼上期間以 `_pasteInProgress` 抑制失焦自動隱藏
- `ViewModels/HistoryPanelViewModel.cs`：自動關閉改用 `PanelVisibilityRules.ShouldHideAfterCopy`
- `tests/SaveClipboard.Tests/PanelVisibilityRulesTests.cs`（新）：覆蓋置頂／選取即貼上／自動關閉組合
- 版本 `1.4.1`（`AssemblyVersion` 維持 1.4.0.0、`FileVersion` 1.4.1.0）

### 問題與解法
- **貼上時面板消失**：原本 `PasteAsync` 一律先 `HidePanel()`，且 `CopyAsync` 在自動關閉模式再觸發 `CloseRequested`。改為置頂＋選取即貼上時不隱藏，並跳過自動關閉；貼上目標改用已記錄的前一個視窗。
- **啟用置頂後失焦仍被隱藏**：貼上時會切換前景，面板收到 `Deactivated`；新增 `_pasteInProgress` 讓失焦計時器在貼上期間不排程隱藏。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：101/101 通過
- `dotnet format --verify-no-changes`：通過
- 前景切換與實際貼上仍需實機以滑鼠確認

### 待辦／已知限制
- 置頂＋選取即貼上時，貼上後面板保持顯示、焦點在前一個視窗；需按 X、`Esc` 或快捷鍵收起
- 未在 Windows 10 1809 實機驗證

---

## 2026-11-15 - v1.4.0

### 目標
執行第 7、8、9、14、15、16、20、22 項：拆分大型程式碼後置、建立資源與樣式字典、托盤定位與 DPI、清單增量同步、常用操作補齊、可選加密持久化，以及日誌／搜尋小修。

### 變更
- `Services/WindowPlacementService.cs`（新）：面板位置／大小計算、DPI 換算、工作區夾限、優先對齊托盤圖示；`TrayIconService.TryGetIconRect` 與 `Shell_NotifyIconGetRect` 支援
- `Services/PasteAutomationService.cs`（新）：集中前景目標判斷、`Ctrl+V` 送出與失敗提示
- `Services/DialogService.cs`（新）：ContentDialog 單一實例防護與例外處理
- `Services/KeyboardNavigationService.cs`（新）：方向鍵、`Home`／`End`／`PageUp`／`PageDown`、`Ctrl+A`、`Ctrl+F`、`Enter`／`Delete` 與焦點移動
- `Services/VisibleEntrySynchronizer.cs`（新）：`VisibleItems` 以插入／移除／移動增量同步，不再每次清空重建
- `Services/HistoryPersistenceService.cs`、`HistoryPersistenceCoordinator.cs`（新）：DPAPI 加密的 `%LOCALAPPDATA%\SaveClipboard\history.dat`，變更後延遲 1 秒寫入，結束時同步 flush；關閉選項會刪除檔案
- `Models/AppSettings.cs`：新增 `AlwaysOnTop`、`PersistHistory`、`HasSeenWelcome`、`PanelDpi`；`Clone`／`CopyFrom`／`Normalize` 同步
- `Models/DeletedClipboardEntry.cs`（新）：刪除項目與原索引，供復原使用
- `Services/ClipboardHistoryService.cs`：新增 `Restore(entry, index)` 與載入用 `Restore(entries)`
- `ViewModels/HistoryPanelViewModel.cs`：儲存用量摘要、首次導引狀態、刪除／復原清單、`VisibleEntrySynchronizer`、AppStrings
- `ViewModels/SettingsViewModel.cs`：置頂／加密保存草稿、重設預設值、開啟日誌資料夾、`AppStrings`、持久化啟用切換
- `Views/HistoryPanelWindow.xaml(.cs)`：新增置頂／加密保存／重設／開日志 UI、清除搜尋按鈕、Undo 提示、首次導引、Shift／Ctrl 多選；改用抽取後的服務
- `Styles/Themes/CommonBrushes.xaml`、`Styles/TextStyles.xaml`（新）：集中筆刷與文字樣式，`HistoryPanelWindow.xaml` 改用 `ThemeResource`
- `Services/ThemeService.cs`：支援 `UISettings.ColorValuesChanged`、高對比系統色彩、`IsDark`，並更新 swatch 深色預覽
- `Strings/zh-Hant/Resources.resw`、`Strings/en-US/Resources.resw`、`Helpers/AppStrings.cs`（新）：UI 文案與程式碼字串資源化，主要 XAML 使用 `x:Uid`
- `Services/DiagnosticsFileLoggerProvider.cs`：註解路徑修正；超過上限輪替 `app.log.1`；寫入例外含 `UnauthorizedAccessException`
- `Services/ClipboardSearchFilter.cs`：改用 `OrdinalIgnoreCase`
- `SaveClipboard.csproj`：加入 `DefaultLanguage=zh-Hant`、`ProjectPriFileName=resources.pri`、`System.Security.Cryptography.ProtectedData`
- 版本 `1.4.0`（`AssemblyVersion` 1.4.0.0、`FileVersion` 1.4.0.0）

### 問題與解法
- **`AccessibilitySettings.HighContrastChanged` 在未封裝環境拋 COMException**：改用 `UISettings.ColorValuesChanged` 監聽色彩變化，`AccessibilitySettings` 只用來讀取目前高對比狀態。
- **首次導引的 `XamlRoot` 尚未建立**：`ShowWelcomeAsync` 先等待 `RootGrid.XamlRoot` 出現，再開啟 `ContentDialog`，避免 `ArgumentException`。
- **PowerShell 批次取代插入字面跳脫序列**：多個檔案曾出現未轉義字元，已逐一修正並以建置確認。
- **`NativeMethods` 為 internal，卻出現在 public 方法簽章**：將 `TryGetIconRect`／`ClampToWorkArea` 改為 internal，避免 CS0051。
- **`SettingsViewModel` 儲存順序**：先寫設定檔，成功後才更新開機啟動與持久化；失敗時保留頁面並顯示錯誤。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：92/92 通過
- `dotnet format --verify-no-changes`：通過
- 啟動實測：首次導引按鈕可被 UIA 找到；設定頁可找到「面板置頂」「保存歷史到本機（加密）」「開啟日誌資料夾」「重設為預設值」
- 加密保存實測：啟用後複製文字，`history.dat` 產生且內容不含明文；測試後已還原設定並刪除檔案
- 未產生 `crash.log`

### 待辦／已知限制
- 加密歷史載入在 UI 執行緒同步解密，超大圖片歷史可能延長啟動時間
- 高對比與多螢幕／不同 DPI 僅以程式邏輯與單元測試驗證，仍需實機目視
- Shift 多選以目前可見清單為範圍，不會跨搜尋結果選取
- 未在 Windows 10 1809 實機驗證
- 未封裝 MSIX 與程式碼簽章仍未處理

---

## 2026-11-15 - v1.3.5

### 目標
修正常駐區圖示需要雙擊才能開啟面板，以及從工作列圖示開啟面板後「選取後直接貼上」沒有作用；常駐區左鍵改為只開啟、不再切換。

### 變更
- `Services/TrayIconService.cs`：記錄 `NIM_SETVERSION` 是否成功；v4 成功時只處理 `NIN_SELECT`／`NIN_KEYSELECT`／`WM_CONTEXTMENU`，否則才退回 `WM_LBUTTONUP`／`WM_LBUTTONDBLCLK`／`WM_RBUTTONUP`；開啟與右鍵各自加 300ms 去重
- `App.xaml.cs`：常駐區左鍵回呼由 `TogglePanel` 改為 `ShowPanel`（只開啟）；建立並在結束時釋放 `ForegroundWindowTracker`
- `Helpers/PasteTargetRules.cs`（新）：集中「可否作為貼上目標」判斷（排除本程式、不可見、`Shell_TrayWnd`／`Progman`／`WorkerW` 等）
- `Helpers/ForegroundWindowTracker.cs`（新）：以 `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, OUTOFCONTEXT | SKIPOWNPROCESS)` 持續記錄最後一個外部前景視窗
- `Helpers/NativeMethods.cs`：新增 `SetWinEventHook`／`UnhookWinEvent`、`WinEventDelegate` 與事件常數
- `Views/HistoryPanelWindow.xaml.cs`：移除原本的目標判斷，改用 `PasteTargetRules`；`CapturePasteTarget`、失焦更新與貼上流程都會在目前前景無效時改用追蹤值；版本 `1.3.5`（`FileVersion` 1.3.5.0，`AssemblyVersion` 維持 1.3.0.0）

### 問題與解法
- **圖示要雙擊／一開即關**：`app.log` 可見同一次點擊在 1ms 內「顯示面板」後立刻「切換：隱藏面板」。原因是一次點擊同時收到 v4 與舊式訊息，`TogglePanel` 被呼叫兩次。改為依 `NIM_SETVERSION` 結果只處理一種訊息，並以 300ms 去重；左鍵改為只開啟。
- **工作列開啟後不貼上**：日誌顯示 `貼上目標：無`，貼上時 `沒有可自動貼上的目標視窗`。點工作列時前景先變成殼層，無論開啟或失焦當下都抓不到真正的前一個 App。新增前景事件追蹤器，記住最後一個非本程式、非殼層的可見前景視窗，並作為貼上目標的備援。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：80/80 通過
- `dotnet format --verify-no-changes`：通過
- 常駐區單擊與工作列貼上屬前景行為，需在實機以滑鼠確認

### 待辦／已知限制
- 前景事件追蹤器無法辨識「最後一個 App 是否仍適合貼上」，例如目標已關閉時會退回複製
- 系統匣去重視窗為 300ms，理論上會忽略 300ms 內連點的第二次左鍵

---

## 2026-11-15 - v1.3.4

### 目標
修正設定頁「選取後直接貼上」沒有作用：點選歷史項目只會複製，不會貼回前一個視窗。

### 變更
- `Views/HistoryPanelWindow.xaml.cs`：`PasteIntoPreviousWindowAsync` 改為先 `HidePanel()`，待焦點回到前一個視窗後再以 `GetForegroundWindow()` 決定貼上目標；顯示時擷取的值作為備援
- 不再於貼上一開始就把 `_pasteTarget` 清成 0；成功貼上後保留目標，視窗失效才清除，支援連續點選重複貼上
- 新增 `ActivateForegroundWindow`：以 `AttachThreadInput` + `SetForegroundWindow` 切換目標，避免直接 `SetForegroundWindow` 失敗
- `OnActivated`：面板失焦到其他程式時更新 `_pasteTarget`；「外部啟用」分支加上 `!AppWindow.IsVisible`，面板已可見時不再重跑 `ShowNearTray`
- 貼上流程補上目標視窗日誌；沒有可貼上目標時僅記錄 Debug
- 版本 `1.3.4`（`AssemblyVersion` 維持 `1.3.0.0`，`FileVersion` 改 `1.3.4.0`）

### 問題與解法
- **根因**：`_pasteTarget` 只在「面板隱藏→顯示」時擷取一次。`app.log` 可見開頭 `貼上目標：無` 之後面板持續可見，後續多次「略過本程式設定剪貼簿造成的更新」（代表使用者真的點選項目複製）都沒有任何貼上嘗試，因為 `_pasteTarget` 一直是 0。原流程在貼上開頭還會把 `_pasteTarget` 清成 0，失敗後也無法恢復。
- **解法**：改用「面板隱藏後實際取得前景的視窗」作為主要目標，這正是使用者開啟面板前的視窗；顯示時擷取值與失焦時更新的值作為備援。連續貼上不再清空目標。
- **焦點搶奪**：原本任何超過 2 秒的啟用都當成外部顯示請求，連點回已可見的面板也會重跑 `ShowNearTray` 並把 `_everActivated` 重設為 `false`。加上 `!AppWindow.IsVisible` 後，已可見時只當作一般取得焦點。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：80/80 通過
- `dotnet format --verify-no-changes`：通過
- 自動貼上涉及前景切換，仍須在實機以滑鼠確認（自動化環境無法穩定取得前景）

### 待辦／已知限制
- 從工作列／桌面手動啟動且沒有任何前一個視窗時，仍只會複製，無目標可貼上
- 目標視窗若以更高權限執行，`SendInput` 仍會被 UIPI 阻擋，僅顯示系統列提示

---

## 2026-11-15 - v1.3.3

### 目標
修正「剪貼簿歷史有時候只留下第一筆、之後複製都不會出現」的問題，並補足能判斷漏記原因的診斷資訊。

### 變更
- `Services/ClipboardCaptureQueue.cs`（新）：把 `WM_CLIPBOARDUPDATE` 依序排隊處理，取代原本的 `_processing`／`_pending` 單一旗標；每次擷取有 15 秒逾時，逾時會記錄並繼續下一筆；待處理超過 8 筆時丟棄最舊序號並記錄
- `Services/ClipboardListenerService.cs`：改由 `ClipboardCaptureQueue` 驅動擷取；每個更新記錄序號；文字／圖片成功加入改為 Information；新增 WinRT 失敗或格式為空時的 Win32 `CF_UNICODETEXT` 後援，且後援前會先以 Win32 檢查隱私標記；`AvailableFormats`／`Contains` 的 `OutOfMemoryException` 不再中斷整筆擷取；不支援格式改記錄格式清單
- `Helpers/ClipboardRetry.cs`：`IsClipboardBusy` 納入 `OutOfMemoryException`（0x8007000E），WinRT 剪貼簿暫時無法讀取時會重試
- `Helpers/ClipboardFormatReader.cs`：新增 `TryReadUnicodeText`，以 Win32 讀取 `CF_UNICODETEXT`
- `App.xaml.cs`：日誌最低等級改為 Debug、`Microsoft.*` 限制在 Warning；建立 `ClipboardCaptureQueue` 的 logger 並傳入監聽器
- `Services/DiagnosticsFileLoggerProvider.cs`：`IsEnabled` 改為 Debug 以上
- `Views/HistoryPanelWindow.xaml.cs`：顯示面板時的日誌加入 `Items`／`VisibleItems` 數量
- `tests/SaveClipboard.Tests/ClipboardCaptureQueueTests.cs`（新）：連續更新排序、待處理上限丟棄、逾時後續處理、取消後不處理（4 項）
- `tests/SaveClipboard.Tests/ClipboardRetryTests.cs`：新增 `OutOfMemoryException` 重試與 `IsClipboardBusy` 判斷（4 項）
- 版本 `1.3.3`（`AssemblyVersion` 維持 `1.3.0.0`，`FileVersion` 改 `1.3.3.0`）

### 問題與解法
- **只有第一筆（已重現）**：以 `Set-Clipboard` 連續設定兩次文字時，第二次的 `WM_CLIPBOARDUPDATE` 對應的 `DataPackageView.AvailableFormats` 會回傳空清單，`Contains(Text)`／`Contains(Bitmap)` 皆為 false，舊版直接視為不支援格式而略過，因此清單只剩第一筆。現在遇到空清單會改以 Win32 `CF_UNICODETEXT` 讀取，`e2e-check.ps1` 連續多輪都成功收錄 Alpha 與 Beta。
- **OOM 造成整筆略過**：`app.log` 另有多筆 `System.OutOfMemoryException` 發生在 `DataPackageView.AvailableFormats`，原本只攔 `COMException`／`InvalidOperationException`／`UnauthorizedAccessException`，例外往上冒後整筆擷取被略過。現在 OOM 視為暫時性忙碌並重試。
- **格式讀取失敗時的隱私漏洞**：`AvailableFormats` 失敗時原本直接視為「沒有標記」並繼續收錄，可能記到 `CanIncludeInClipboardHistory=0` 的內容（`e2e-privacy.ps1` 曾因此失敗）。現在格式無法取得時，會先以 Win32 檢查兩個隱私標記再決定是否收錄。
- **可能永久停擺**：原本 `CaptureAsync` 沒有逾時，來源程式無回應時 `_processing` 會一直是 `true`，之後的更新永遠只被標記 pending。改由 `ClipboardCaptureQueue` 以 `Task.WhenAny` 逾時後繼續下一筆。
- **無法事後定位**：檔案日誌原本只收 Information，成功收錄是 Debug，事後看不到。已調整最低等級並補上序號與結果記錄。
- **e2e 誤判**：`e2e-check.ps1` 原本只計算名稱含 `SaveClipboardE2E` 的元素數量，同一項目的 Grid／CheckBox 會重複計數而造成假 PASS。改為去重並分別驗證 Alpha、Beta 都存在。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：80/80 通過（原 72 + 新增 8）
- `dotnet format --verify-no-changes`：通過
- `tools/e2e-check.ps1`：連續多輪 E2E PASS，`Items=2` 且 Alpha／Beta 都收錄；日誌可見第二次更新走 Win32 後援
- `tools/e2e-privacy.ps1`：連續兩輪 PRIVACY E2E PASS，Control 收錄、Excluded 與 NoHistory 皆未收錄

### 待辦／已知限制
- Win32 後援只處理文字（`CF_UNICODETEXT`），圖片仍依賴 WinRT；來源程式若持續無回應，圖片仍可能被逾時略過
- 逾時後原本的 WinRT 讀取工作不會強制取消（`GetTextAsync` 不支援取消權杖），只會被放行並在完成後才加入歷史
- 待人工以「複製檔案／大圖／超長文字」等情境重現，確認新日誌與收錄行為

---

## 2026-11-15 - v1.3.2

### 目標
統一主畫面底部列「暫停」與「清除全部」按鈕的高度，並讓「清除全部」只顯示圖示。

### 變更
- `Views/HistoryPanelWindow.xaml`：清除全部按鈕改為圖示按鈕（`FontIcon`），`Padding` 改為 `7,4`、透明背景、無邊框，與暫停按鈕一致；名稱改由 `AutomationProperties.Name` 與 `ToolTipService.ToolTip` 呈現，並加上 `AutomationId="ClearAllButton"`。
- 版本提升至 `1.3.2`（`FileVersion` `1.3.2.0`，`AssemblyVersion` 維持 `1.3.0.0`）。

### 問題與解法
- 高度不一致的原因：清除全部按鈕內含 `TextBlock`，且 `Padding` 為 `8,4`；改為純圖示並對齊 `Padding` 後高度一致。

### 待辦
- 尚未實機檢視高 DPI 下的按鈕對齊。

## 2026-11-15 - v1.3.1

### 目標
執行穩定性優化項目 1–6：降低常駐程式因非同步 UI 例外而結束的風險、讓剪貼簿複製失敗有明確回饋、補齊設定儲存錯誤處理、處理 Alt+F4 關閉路徑、結束時取消背景工作，並把自動貼上改用 `SendInput`。

### 變更
- `Services/ClipboardWriterService.cs`（新）：`IClipboardWriter`／`ClipboardWriterService`，集中處理文字與圖片寫入、`ClipboardRetry`、自我更新抑制、取消與例外；回傳 `ClipboardWriteStatus` 與使用者訊息
- `Services/AppLifetime.cs`（新）：應用程式層級 `CancellationTokenSource`，結束時統一取消背景流程
- `Helpers/ClipboardRetry.cs`：新增 `CancellationToken` 參數與 `IsClipboardBusy`；忙碌判斷涵蓋 WinRT 一般例外（HRESULT `0x800401D0`）與 `COMException`
- `Helpers/NativeMethods.cs`：新增 `INPUT`／`KEYBDINPUT` 結構與 `SendInput`；`SendPasteShortcut(out int errorCode)` 改以 `SendInput` 送出 `Ctrl+V`
- `Services/TrayIconService.cs`：新增 `ShowBalloon`，用於面板關閉後仍要告知使用者的貼上失敗訊息
- `ViewModels/HistoryPanelViewModel.cs`：改注入 `IClipboardWriter`，移除直接呼叫 `Clipboard`／`NativeMethods`；失敗時顯示回傳訊息，取消時不顯示提示
- `ViewModels/SettingsViewModel.cs`：儲存時收集開機啟動與設定檔錯誤；新增 `HasSaveError`／`SaveErrorText`／`SaveErrorVisibility`，失敗時留在設定頁並顯示原因
- `Views/HistoryPanelWindow.xaml`：設定頁新增儲存錯誤提示列（`AutomationProperties.LiveSetting="Assertive"`）
- `Views/HistoryPanelWindow.xaml.cs`：所有 `async void` 事件加 `try/catch`；`ConfirmAsync` 加 `_dialogOpen` 防護與例外處理；`AppWindow.Closing` 攔截 Alt+F4 改為隱藏；貼上流程改用 `CancellationToken` 與 `SendPasteShortcut` 結果提示；`OnCopyCompleted` 在自動隱藏模式下失敗仍會顯示提示
- `App.xaml.cs`：建立 `AppLifetime` 並傳入 writer／listener／視窗；結束時先 `Cancel`、`PrepareForExit`，再釋放服務
- `Services/ClipboardListenerService.cs`：接收 `CancellationToken`；文字、圖片、隱私標記讀取與背景解碼都可取消；圖片加入歷史前檢查取消與是否已釋放
- `Models/AppSettings.cs`：新增 `CopyFrom`，設定頁按「關閉」時把記憶體中的設定還原為快照，避免儲存失敗後 UI 與實際狀態不一致
- `tests/`：新增 `AppLifetimeTests`（3 項）、`ClipboardRetryTests`（2 項）、`AppSettingsTests.CopyFrom`（1 項）；`SaveClipboard.csproj` 加 `InternalsVisibleTo`
- 版本 `1.3.1`（`AssemblyVersion` 維持 `1.3.0.0`，`FileVersion` 改 `1.3.1.0`）

### 問題與解法
- **`HistoryPanelWindow` 建構子參數增加**：面板需要生命週期 token 與系統列提示回呼，於 `App.xaml.cs` 一次傳入；`_trayIcon` 較晚建立，改用 closure 延後解析。
- **貼上失敗原本只寫日誌**：面板在貼上前已隱藏，無法用原有 toast 顯示；改由 `TrayIconService.ShowBalloon` 顯示系統列氣泡提示。
- **自動隱藏模式的複製失敗沒有回饋**：`OnCopyCompleted` 原本在自動隱藏模式直接 return；移除該判斷後，成功時面板立即隱藏，失敗時面板仍開著並顯示提示。
- **`ContentDialog` 同時開啟會拋例外**：以 `_dialogOpen` 阻擋重複進入，並在 `finally` 重置；`ShowAsync` 例外也改為記錄並回傳取消。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：72/72 通過（原 66 + 新增 6）
- `dotnet format --verify-no-changes`：通過
- 啟動實測：`--autostart` 啟動後程序存活；對面板送 `WM_CLOSE` 後程序仍存活、面板 `IsWindowVisible=False`，未產生 `crash.log`

### 待辦／已知限制
- `SendInput` 對較高權限視窗仍會被 UIPI 阻擋，目前以 Win32 錯誤碼判斷並顯示氣泡提示，未做完整性等級（integrity level）比對
- Alt+F4 改為隱藏需人工在實機確認；系統關機時的視窗關閉行為未特別處理
- 設定儲存失敗提示尚未涵蓋所有可能的例外類型（例如極端記憶體不足）
- 未在 Windows 10 1809 實機驗證

---

## 2026-11-15 - v1.3.0

### 目標
Sprint 2 體驗升級：搜尋、鍵盤導航、選取即貼上、可調整大小並記憶位置、暫停記錄，並讓托盤左鍵與全域快捷鍵行為一致。

### 變更
- `Services/ClipboardSearchFilter.cs`（新）：純函式比對，只比對文字項目，忽略前後空白與大小寫
- `Services/ClipboardRecordingState.cs`（新）：共用暫停狀態，`Pause`／`Resume`／`Toggle`、到期自動恢復（預設 30 分鐘）、`StateChanged`
- `Models/AppSettings.cs`：新增 `PasteOnSelect`、`HasPanelBounds`、`PanelLeft`／`Top`／`Width`／`Height` 與尺寸常數；`Clone`／`Normalize` 同步處理
- `ViewModels/HistoryPanelViewModel.cs`：新增 `VisibleItems`、`SearchText`（變更即重建過濾結果）、`RecordingPaused`／`PauseActionText`／`PauseActionGlyph`／`FooterHint`、`TogglePauseRecordingCommand`、`ItemCopied` 事件、`SavePanelBounds`、`ResetSearch`、`RefreshRecordingState`
- `ViewModels/SettingsViewModel.cs`：新增 `PasteOnSelectDraft`，載入與儲存時同步
- `Views/HistoryPanelWindow.xaml`：改為 4 列（標題／搜尋／清單／底部），新增 `SearchBox`、空狀態改為可切換文字、底部加入暫停按鈕與狀態提示、設定頁新增「貼上」區塊、`KeyDown` 改 `PreviewKeyDown`、加入 `SearchBox`／`ClosePanelButton` 的 AutomationId
- `Views/HistoryPanelWindow.xaml.cs`：`↑`／`↓` 焦點移動、`Enter` 複製（搜尋框內按 Enter 取第一筆）、`Esc` 先清搜尋再關閉、焦點收回改為僅在焦點遺失時、顯示時聚焦搜尋框、貼上目標捕捉與 `Ctrl+V` 流程、可調整大小與最小尺寸、位置／大小保存與還原、暫停倒數刷新
- `Helpers/NativeMethods.cs`：新增 `MonitorFromRect`、`GetDpiForMonitor`、`IsWindow`、`IsWindowVisible`、`GetClassNameW`、`keybd_event`、`SendPasteShortcut`、`MF_CHECKED`
- `Services/TrayIconService.cs`：新增暫停選單（含 `MF_CHECKED`）、`UpdatePausedState`、提示文字反映暫停狀態；建構子新增 `togglePause`／`isPaused`
- `Services/ClipboardListenerService.cs`：收到 `WM_CLIPBOARDUPDATE` 時先檢查暫停狀態
- `App.xaml.cs`：註冊 `ClipboardRecordingState`、托盤與快捷鍵共用 `TogglePanel`、接上暫停切換與提示更新、結束前保存面板位置
- `tests/`：新增 `ClipboardSearchFilterTests`（7 項）、`ClipboardRecordingStateTests`（7 項）、`AppSettingsTests`（5 項）
- `tools/e2e-experience.ps1`（新）：搜尋過濾／清除、暫停與恢復記錄、面板位置與大小保存、下次啟動還原
- 版本 `1.3.0`（`AssemblyVersion` 改為 `1.3.0.0`）

### 問題與解法
- **`TextBox.IsClearButtonVisible` 無法編譯**：目前 WinUI 版本回報 `WMC0011: Unknown member`。改為不新增清除鈕，按 `Esc` 在搜尋框有內容時先清除搜尋，再按一次才關閉面板。
- **清單重建會搶走搜尋焦點**：原 `UpdateEmptyState` 在清單變空時主動 `RootGrid.Focus`，輸入搜尋字串導致焦點被移走。改為僅在 `FocusManager.GetFocusedElement` 為 `null`（焦點真的消失）時才收回焦點。
- **高 DPI 下尺寸上下限衝突**：面板尺寸以實體像素保存，原本上限 1000／1200 在 200% 縮放會被夾小。`Normalize` 改為只做粗粒度檢查（200–2400），實際的 320×400 DIP 最小尺寸由視窗依 `GetDpiForWindow` 強制。
- **測試機無法搶到前景**：UIA 取得的視窗 `SetForegroundWindow` 後仍為 `Foreground=False`，`SendKeys` 與 `PostMessage(WM_KEYDOWN)` 都無法觸發 XAML 鍵盤事件，因此鍵盤導航與自動貼上只能人工驗證（與 v1.0.0／v1.2.7 的環境限制相同）。可自動化的搜尋與視窗記憶改以 UIA `ValuePattern` 與 `InvokePattern` 驗證。
- **`GetDpiForMonitor` 相依 shcore.dll**：呼叫失敗或回傳 0 時退回比例 1.0，避免預設尺寸計算失敗。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：66/66 通過（原 47 + 新增 19）
- `dotnet format --verify-no-changes`：通過
- `tools/e2e-check.ps1`：E2E PASS
- `tools/e2e-privacy.ps1`：PRIVACY E2E PASS
- `tools/e2e-experience.ps1`：EXPERIENCE E2E PASS（搜尋只剩符合項目、清除後恢復兩筆；暫停時新複製內容未收錄、恢復後正常收錄；MoveWindow 640×520 保存後重啟還原為 200,150 640×520）

### 待辦／已知限制
- 鍵盤導航（`↑`／`↓`／`Enter`）與「選取即貼上」需人工在正常前景環境確認
- 托盤右鍵「暫停記錄 30 分鐘」選單與圖示提示需人工確認
- 多螢幕／變更 DPI 後的面板還原僅以程式邏輯驗證，未實機切換螢幕測試
- 未在 Windows 10 1809 實機驗證

---

## 2026-11-15 - v1.2.7

### 目標
收斂產品風險：尊重來源應用程式的隱私標記、消除剪貼簿自我抑制的時間窗競態、Explorer 重新啟動後復原系統列圖示、設定檔損毀不再靜默重置，並讓全域例外處理不再一律吞掉錯誤。

### 變更
- `Helpers/NativeMethods.cs`：新增 `GetClipboardSequenceNumber`、`RegisterClipboardFormatW`、`RegisterWindowMessageW` 與讀取剪貼簿 DWORD 所需的 `OpenClipboard`／`GetClipboardData`／`GlobalLock`／`GlobalUnlock`
- `Services/ClipboardChangeGate.cs`：改為比對剪貼簿序號（`ExpectOwnChange`／`TryConsume`），移除 300ms 時間窗；序號 0 視為未知、不消耗
- `ViewModels/HistoryPanelViewModel.cs`：`SetContent` + `Flush` 成功後才記錄期待序號
- `Services/ClipboardListenerService.cs`：收到更新時以目前序號比對；新增 `EvaluatePrivacyAsync`，在解析文字／圖片前先判定隱私標記
- 新增 `Services/ClipboardPrivacyPolicy.cs`、`Models/ClipboardSkipReason.cs`、`Helpers/ClipboardFormatReader.cs`：純決策函式 + Win32 DWORD 讀取（含 3 次短重試）
- `Services/TrayIconService.cs`：註冊並處理 `TaskbarCreated`，Explorer 重啟後重新加入圖示
- `Services/SettingsService.cs`、`Models/SettingsLoadStatus.cs`：區分 `Ok`／`Missing`／`Recovered`／`Unavailable`，損毀時將原檔移至 `settings.corrupt-{ticks}.json`
- `ViewModels/SettingsViewModel.cs`、`Views/HistoryPanelWindow.xaml(.cs)`：設定頁新增自訂提示列顯示設定已還原，儲存後隱藏
- `Helpers/DiagnosticLog.cs`：集中崩潰日誌至 `%LOCALAPPDATA%\SaveClipboard\Logs\crash.log`，1MB 上限
- `App.xaml.cs`：`UnhandledException` 改為只吞 `COMException`／`IOException`／`UnauthorizedAccessException`，其餘寫日誌後讓程式結束；`AddDebug` 只在 Debug 建置啟用
- `Services/DiagnosticsFileLoggerProvider.cs`：日誌路徑改為 `%LOCALAPPDATA%\SaveClipboard\Logs\app.log`
- `Helpers/HelperWindow.cs`：原生回呼的裸 `catch` 改為寫入 `DiagnosticLog`
- `tests/`：改寫 `ClipboardChangeGateTests`（6 項）；新增 `ClipboardPrivacyPolicyTests`（5 項）；`SettingsServiceTests` 新增備份、狀態與重複載入（4 項）
- `tools/e2e-privacy.ps1`：以 Win32 設定帶隱私標記的剪貼簿內容，透過 UIA 驗證面板未收錄
- 版本 `1.2.7`（`AssemblyVersion` 維持 `1.2.0.0`）

### 問題與解法
- **InfoBar 造成 XamlParseException**：`Cannot find a Resource with the Name/Key TabViewButtonBackground`。與 v1.0.0 的 NumberBox／ToggleSwitch 同源，目前 WinUI 1.8 環境缺少該資源鍵。改為自訂 `Border` 提示列，並在 `OpenSettings_Click` 依 `HasSettingsRecoveryNotice` 設定 `Visibility`，避免在 Window 根層使用 x:Bind 轉換器。
- **WinRT 是否能取得自訂剪貼簿格式**：實測 `DataPackageView.AvailableFormats` 會列出 `ExcludeClipboardContentFromMonitorProcessing` 與 `CanIncludeInClipboardHistory`；值為 DWORD，改用 Win32 `GetClipboardData` 讀取，並放在 `Task.Run` 避免卡住 UI。
- **設定檔損毀的測試假設**：損毀後原檔會被移走，因此 `Load()` 第二次的狀態是 `Missing` 而非 `Recovered`；測試改為驗證備份存在且不會重複備份。
- **e2e 點擊／Enter 無法驗證**：測試機的面板右上區域被其他視窗覆蓋，滑鼠點擊會落到別的視窗並使面板失焦（日誌 `忽略失焦（取得前景=False）`）。與 v1.0.0 記錄的限制相同，維持人工驗證。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：47/47 通過
- `dotnet format --verify-no-changes`：通過
- `tools/e2e-check.ps1`：E2E PASS（文字擷取與面板顯示）
- `tools/e2e-privacy.ps1`：PRIVACY E2E PASS（一般內容收錄、兩種隱私標記皆未收錄，日誌各留一筆略過原因）
- 設定損毀實測：放入無效 JSON 後啟動，設定頁顯示「設定檔已還原為預設值」，`%AppData%\SaveClipboard` 產生一份 `settings.corrupt-*.json`，測試後已還原原設定

### 待辦／已知限制
- `TaskbarCreated` 需人工重啟 Explorer 驗證圖示會復原
- 從歷史點擊複製與 Enter 複製仍待人工確認（測試機面板被其他視窗覆蓋）
- 序號式 Gate 假設 `GetClipboardSequenceNumber` 正常運作；若回 0 則不抑制（可能多一筆重複，不會漏記）
- 未在 Windows 10 1809 實機驗證

---

## 2026-11-15 - v1.2.6

### 目標
在既有行為不變的前提下，降低記憶體與圖片處理成本，並補齊幾個容易造成使用者困惑的小問題（快捷鍵占用、面板跑出畫面、鍵盤操作、螢幕閱讀器名稱）。

### 變更
- `Services/ClipboardHistoryService.cs`：文字索引改為 `"T:" + SHA256(UTF8(text))`，不再把整段文字複製到 `Key`；圖片容量統計納入 `ThumbnailBytes`；沒有釘選項目時略過重排；移除未使用的 `ItemsChanged`
- `Services/ClipboardListenerService.cs`：縮圖改由原始圖片資料縮放解碼，移除對已編碼 PNG 的再解碼；單筆文字超過 200 萬字元時不保存並記錄警告
- `Services/ClipboardChangeGate.cs`：預設自我抑制視窗 1 秒 → 300ms
- `Views/HistoryPanelWindow.xaml.cs`：`PositionNearTray` 將尺寸與座標限制在目前螢幕工作區；新增 Enter 複製、Delete 刪除（含二次確認）與焦點項目解析；刪除流程抽出 `DeleteSelectedAsync`
- `Views/HistoryPanelWindow.xaml`：勾選框的 `AutomationProperties.Name` 改綁項目預覽、`HelpText` 說明選取；設定頁新增快捷鍵狀態提示
- `ViewModels/SettingsViewModel.cs`：注入 `HotkeyService`，提供 `IsShortcutUnavailable`／`ShortcutStatusText`／`ShortcutWarningVisibility`
- `ViewModels/HistoryPanelViewModel.cs`：改注入 `AppSettings` 與 `HotkeyService`，移除重複的 `SettingsService.Load()`
- `App.xaml.cs`：`AppSettings` 註冊為單例；`HotkeyService` 提前建立並傳入 ViewModel；第二實例改用 `WaitForHelperWindow` 短暫輪詢
- `Models/ClipboardEntry.cs`：`Preview` 首次計算後快取
- `tests/SaveClipboard.Tests/ClipboardHistoryServiceTests.cs`：釘選圖片上限測試的門檻改為含縮圖的 6 bytes，維持原本驗證意圖
- 版本 `1.2.6`（`AssemblyVersion` 維持 `1.2.0.0`）

### 問題與解法
- **x:Bind 在 Window 根層使用 Converter 失敗**：`SetConverterLookupRoot(this)` 需要 `FrameworkElement`，但根是 `Window`，建置出現 CS1503。改由 ViewModel 直接回傳 `Visibility`，避免在根層使用 Converter。
- **含縮圖的容量統計讓既有測試失敗**：測試原本假設只計算 `ImageBytes`，釘選圖片 5 bytes 在門檻 5 下就被淘汰。將測試門檻調整為 6 bytes，保留「釘選不受上限淘汰」的驗證目的。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：36/36 通過
- `dotnet format --verify-no-changes`：通過

### 待辦／已知限制
- 縮圖改由原始格式縮放解碼，尚未在 Windows 10 1809 與 JPEG／BMP／HEIF 等來源格式實測
- Enter／Delete 僅在清單內元素取得焦點時生效，尚未加入自訂焦點樣式
- 第二實例輪詢上限仍為 3 秒，極慢速開機環境下可能仍找不到既有視窗

---

## 2026-11-15 - v1.2.5

### 目標
全域快捷鍵改為切換主面板：面板隱藏時開啟，面板可見時收起；系統匣左鍵與重複啟動維持只開啟。

### 變更
- `Views/HistoryPanelWindow.xaml.cs`：新增 `TogglePanel()`；設定頁開啟時先執行 `Settings.CloseCommand`（復原未儲存設定）再隱藏；`_allowHide == false`（確認對話框開啟）時忽略切換
- `App.xaml.cs`：`HotkeyService` 改接 `TogglePanel`；`ShowPanel` 保留給系統匣與第二實例
- `Services/HotkeyService.cs`：欄位與參數改名為 `togglePanel`，註解改為切換顯示／隱藏
- `Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`README.md`：版本 `1.2.5` 與行為說明
- 版本 `1.2.5`（`AssemblyVersion` 維持 `1.2.0.0`）

### 問題與解法
- **自動化驗證找不到面板視窗**：以 PowerShell `FindWindow(null, "SaveClipboard")` 查詢失敗，原因是 `$null` 被轉成空字串而比對不到 class。改用實際 class `WinUIDesktopWin32WindowClass` + title 即能取得面板 hwnd。
- **避免失焦計時器重複隱藏**：`HidePanel()` 會先將 `_everActivated` 設回 `false` 並停止計時器，因此隱藏觸發的 `Deactivated` 不會再排程一次隱藏。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：36/36 通過
- `dotnet format --verify-no-changes`：通過
- 實測（對 `SaveClipboardHelperWindow` 送 `WM_HOTKEY`，wParam `0x5343`）：可見→隱藏→可見→隱藏皆正確
- 實測第二實例：面板隱藏時重複啟動仍會顯示面板且第二實例結束，之後快捷鍵可收起
- 實測設定頁開啟時按快捷鍵：面板收起；再次開啟後設定頁已關閉（未儲存設定已復原）

### 待辦／已知限制
- 系統匣左鍵仍維持只開啟，未改為切換（依需求確認的範圍）
- 確認對話框開啟時快捷鍵會被忽略，需先關閉對話框
- 系統匣左鍵與工作列釘選圖示仍需實機人工確認

---

## 2026-11-15 - v1.2.4

### 目標
在不改變產品行為的前提下，強化剪貼簿擷取路徑與常駐資源管理：降低大圖造成的 UI 卡頓、避免剪貼簿競態漏記、限制圖片記憶體用量，並減少日誌 I/O 與檔案成長。

### 變更
- `Helpers/ClipboardRetry.cs`：新增剪貼簿操作重試（3 次、40/80ms），只重試 `COMException`。
- `Services/ClipboardChangeGate.cs`：新增自我更新抑制旗標（消費一次、1 秒到期），避免從歷史複製後又重跑一次擷取流程。
- `Services/ClipboardListenerService.cs`：文字／圖片讀取加重試；圖片解碼、PNG 重編碼、縮圖與 SHA256 移到 `Task.Run`，完成後才以 `DispatcherQueue.TryEnqueue` 加入歷史；熱路徑日誌降為 `LogDebug`。
- `Services/ClipboardHistoryService.cs`：新增 `MaxTotalImageBytes`（預設 100MB）與 `AddImage(..., pixelHash)` 多載；`Trim()` 同步淘汰最舊的未釘選圖片。
- `ViewModels/HistoryPanelViewModel.cs`：`SetContent`／`Flush` 加重試並搭配 `ClipboardChangeGate`。
- `App.xaml.cs`：改用 `Host.CreateApplicationBuilder()` 建立 DI／Logging；關閉時釋放 `IHost`；單一實例 Mutex 改為 `Local\`。
- `Services/SettingsService.cs`：設定檔改為先寫 `.tmp` 再 `File.Move(overwrite: true)`。
- `Services/DiagnosticsFileLoggerProvider.cs`：有界 `Channel` + 背景批次寫入，檔案上限 1MB，`Dispose` 時排空。
- `Helpers/BytesToImageSourceConverter.cs`：以 `ConditionalWeakTable` 快取縮圖 `BitmapImage`。
- 單元測試新增圖片位元組上限、預先計算雜湊、`ClipboardChangeGate`、設定暫存檔清除。
- 版本 `1.2.4`。

### 問題與解法
- **`ConfigureAwait` 不支援 WinRT 非同步型別**：`IAsyncOperation<T>`／`IAsyncAction`／`DataWriterStoreOperation` 沒有 `ConfigureAwait`，建置出現 CS1929。移除這些呼叫的 `ConfigureAwait`，僅保留在一般 `Task` 上使用。
- **XAML 型別解析失敗（WMC0001／WMC0909）**：其實是 C# 編譯失敗導致 MarkupCompilePass2 缺少 LocalAssembly 的連鎖錯誤；修正 C# 後即恢復。
- **圖片處理切到背景後的集合執行緒**：`ObservableCollection` 仍只在 UI 執行緒修改，背景僅產生位元組與雜湊，避免跨執行緒更新 UI。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：36/36 通過
- `dotnet format --verify-no-changes`：通過
- 啟動實測：行程存活、`Set-Clipboard` 後剪貼簿內容正確、無 crash log

### 待辦／已知限制
- 背景 WinRT 影像解碼／編碼需在 Windows 10 1809 與不同圖片格式（JPEG／BMP／HEIF）實機確認
- 圖片位元組上限只淘汰未釘選項目；全部釘選且超過上限時仍會保留
- 非同步日誌在程式被強制結束時可能遺漏最後幾筆

---

## 2026-11-15 - v1.2.3

### 目標
修正「在其他程式複製內容後，主面板會自動彈出」的問題；複製行為只應更新歷史清單，不應顯示面板。

### 變更
- `Views/HistoryPanelWindow.xaml.cs`：`UpdateEmptyState()` 不再對隱藏中的面板呼叫 `RootGrid.Focus()`；改為僅在面板已顯示（`AppWindow.IsVisible`）且為前景視窗（`GetForegroundWindow()` 等於面板 hwnd）時才收回焦點
- 版本 `1.2.3`

### 問題與解法
- **每次複製都彈出面板**：`ConfigureEmptyState()` 訂閱 `Items.CollectionChanged`，任何剪貼簿新增項目都會觸發 `UpdateEmptyState()`，其中無條件執行 `RootGrid.Focus(FocusState.Programmatic)`。對隱藏中的 WinUI 視窗設定焦點會使其被啟用，`OnActivated()` 因此判定為「外部啟用」並呼叫 `ShowNearTray()`。日誌可見固定序列：`收到 WM_CLIPBOARDUPDATE` → `已加入文字歷史` → `外部啟用面板，改為顯示請求` → `顯示面板`。加上「已顯示且在前景」條件後即不再發生。
- 保留原「刪除項目後收回焦點避免失焦關閉」的行為（刪除時面板可見且在前景，條件仍成立）。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：27/27 通過
- `dotnet format --verify-no-changes`：通過
- 實測（`--autostart` 啟動並保持隱藏）：以 `Set-Clipboard` 連續複製文字，面板視窗維持 `IsWindowVisible=False`，日誌不再出現 `外部啟用面板`／`顯示面板`
- 回歸：第二實例啟動仍會顯示面板並取得前景；面板顯示時按關閉可隱藏

### 待辦／已知限制
- 系統匣左鍵與工作列釘選圖示需在實機人工確認（自動化以第二實例模擬）
- 面板顯示中於其他程式複製時，焦點不再被搶回面板，需人工確認游標與輸入不受影響

---

## 2026-11-15 - v1.2.2

### 目標
修正工作列釘選圖示「第一次點擊只閃一下、不顯示主面板」；使用者手動啟動時應立即顯示面板，開機自動啟動則保持隱藏。

### 變更
- `App.xaml.cs`：讀取命令列 `--autostart` 區分手動／開機啟動；手動啟動改呼叫 `ShowNearTray()`，不再先 `Activate()` 再 `Hide()`；啟動時自動遷移舊版開機登錄值
- `Views/HistoryPanelWindow.xaml.cs`：新增 `StartHidden()`，開機啟動時標記為程式自身啟用後再隱藏，避免被誤判外部顯示請求；`OnActivated` 的外部啟用改為延後呼叫 `ShowNearTray()`，不再只重新定位
- `Services/StartupService.cs`：開機啟動登錄值改寫為 `"<exe>" --autostart`；新增 `HasAutoStartArgument()`
- 版本 `1.2.2`

### 問題與解法
- **第一次點擊閃一下**：App 未執行時點釘選圖示會全新啟動，`OnLaunched` 建立面板後執行 `Activate()` + `AppWindow.Hide()`，因此只看到閃一下且無人顯示面板。改為手動啟動時直接顯示、開機啟動才隱藏。
- **開機啟動誤彈面板**：若手動補上 `Activate()` 當作外部啟用，會因 `_lastShowRequestAt` 為預設值而被判定為外部顯示請求。新增 `StartHidden()` 先設定顯示請求時間再啟用、隱藏。
- **已執行時點釘選圖示仍可能不顯示**：外部啟用分支只重設前景保護，沒有 `AppWindow.Show()`。改為完整走 `ShowNearTray()` 顯示流程。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- 手動啟動：UIA 尋得面板且 `IsOffscreen=False`
- `--autostart` 啟動：UIA 找不到可見面板（保持隱藏）
- `--autostart` 後再啟動第二實例：面板顯示且取得前景
- 登錄值已由舊格式自動遷移為含 `--autostart`
- `dotnet test`：27/27 通過
- `dotnet format --verify-no-changes`：通過

### 待辦／已知限制
- 工作列釘選第一次點擊仍需在實機以滑鼠確認（自動化以直接啟動／第二實例模擬）
- 需實機確認開機登入後不彈面板、點釘選圖示第一次即顯示

---

## 2026-11-15 - v1.2.1

### 目標
修正工作列釘選圖示第一次點擊只閃一下、不顯示面板；「剪貼簿歷程」統一改為「剪貼簿歷史」；App 圖示加上黃色圓角底色。

### 變更
- `Helpers/HelperWindow.cs`：輔助視窗改為預設隱藏（移除 `WS_VISIBLE`），避免成為 shell 的「最後作用視窗」
- `Services/TrayIconService.cs`：顯示右鍵選單前暫時顯示輔助視窗，選單關閉後立即隱藏
- `App.xaml.cs`：第二實例改用同步 `SendMessageTimeoutW`（3 秒）等待第一實例完成顯示，取代 `PostMessageW` 後立即結束
- `Helpers/NativeMethods.cs`：新增 `SendMessageTimeoutW`、`SMTO_*`、`SW_HIDE`、`SW_SHOWNOACTIVATE`
- `Views/HistoryPanelWindow.xaml.cs`：`ShowNearTray` 合併為單一流程、一律重新定位並重設前景保護；`Deactivated` 需面板真正取得前景 500ms 以上才排程隱藏；外部啟用視為顯示請求；+120ms 再確認前景；顯示／隱藏狀態寫入診斷日誌
- 使用者可見文字「歷程」改「歷史」：面板標題、空狀態、自動化名稱、清除全部提示、關於說明、系統匣提示與右鍵選單、`csproj` 描述、README
- `tools/generate-icon.ps1`：新增 #FFC107 圓角方形底色並重新產生 `AppIcon.ico`（16–256）與 `AppIcon-256.png`
- 版本 `1.2.1`

### 問題與解法
- **工作列第一次點擊只閃一下**：三個原因疊加。(1) `ShowNearTray` 在視窗已可見時提前 return，未重設「不因失焦隱藏」的保護，shell 還原視窗後的失焦會把面板藏起；(2) 第二實例非同步送出訊息後立刻結束，前景交接競態；(3) 畫面外的輔助視窗原本是 visible，可能被 shell 當成啟用目標。改為同步交接、單一顯示流程、500ms 最短前景時間與 2 秒保護，並讓輔助視窗保持隱藏。
- **輔助視窗仍需接收剪貼簿／系統匣訊息**：隱藏視窗仍可接收 `WM_CLIPBOARDUPDATE`、`WM_HOTKEY` 與 `Shell_NotifyIcon` 回呼；僅在右鍵選單期間以 `SW_SHOWNOACTIVATE` 暫時顯示，讓 `TrackPopupMenu` 能取得前景並於點擊外部時關閉。

### 測試結果
- `dotnet build`：0 警告 0 錯誤
- `dotnet test`：27/27 通過
- `dotnet format --verify-no-changes`：通過
- `tools/e2e-check.ps1`：E2E PASS
- 以啟動第二個 exe 模擬工作列點擊：面板 1.5 秒內可見、前景為面板視窗，5.5 秒後仍可見；輔助視窗維持隱藏
- 圖示：ICO 含 7 個尺寸（16–256，PNG 條目），黃底與圓角透明已確認

### 待辦／已知限制
- 工作列釘選第一次點擊仍需在實機以滑鼠確認（自動化以第二實例模擬）
- 新圖示在 16px／深色／高對比下的辨識度需人工目視確認
- 右鍵選單改為「顯示輔助視窗 → 選單 → 隱藏」，需人工確認點擊選單外部可正常關閉

---

## 2026-11-15 - v1.2.0

### 目標
釘選項目固定置頂；精簡剪貼列與設定頁色塊；修正點擊圖片項目無法複製；新增複製完成提示與「關於」資訊。

### 變更
- `Services/ClipboardHistoryService.cs`：新增 `RepositionPinned()`，`Add`／`TogglePin`／`SetPinned` 後將釘選項目穩定重排在清單上方
- `ViewModels/HistoryPanelViewModel.cs`：圖片改以 `InMemoryRandomAccessStream` + `DataWriter` 寫入並 `Clipboard.Flush()`；新增 `CopyCompleted` 事件
- `Views/HistoryPanelWindow.xaml(.cs)`：列高、CheckBox、縮圖與字級縮小；釘選徽章改疊在縮圖右下角；標題列圖示放大至 34；主題色塊縮小；新增左下角複製提示；設定頁順序調整並新增「關於」
- `ViewModels/SettingsViewModel.cs`：新增 `AppName`、`VersionText`、`FileVersionText`（由組件屬性讀取）
- `Services/ThemeService.cs`：新增 `ToastBackgroundBrush` 深淺色切換
- 單元測試新增釘選置頂／取消釘選／新項目排序，修正既有排序假設
- 版本 `1.2.0`

### 問題與解法
- **點擊圖片項目沒反應**：原以 `MemoryStream.AsRandomAccessStream()` 設定剪貼簿，方法結束後底層資料可能失效。改用 WinRT `InMemoryRandomAccessStream` 完整寫入、游標歸零並 `Clipboard.Flush()`；縮圖設 `IsHitTestVisible="False"` 確保點擊冒泡到複製處理。
- **釘選後新項目仍排在最上方**：`Add` 固定在索引 0 插入。改為插入後統一重排，釘選區維持在上、未釘選依新舊排序。
- **既有測試假設新項目在 `Items[0]`**：釘選置頂後 `Items[0]` 可能是釘選項目；測試改以 `AddText` 回傳的項目或實際索引驗證。

### 測試結果
- `dotnet build` 0 警告 0 錯誤
- 單元測試 27/27 通過
- `tools/e2e-check.ps1`：面板顯示與文字項目擷取通過
- 待人工目視確認：列密度、釘選徽章位置、圖片點擊複製、手動模式左下角提示

### 待辦／已知限制
- 圖片複製與提示需在實機以滑鼠點擊確認（自動化無法模擬 DataTemplate 內的 Tapped）
- 自動關閉模式下提示不會顯示（依需求選擇立即關閉）

---

## 2026-11-15 - v1.1.0

### 目標
修正工作列釘選第一次點擊不顯示面板；新增面板內設定頁（保存上限、開機啟動、顯示模式、10 款主題色、主視窗關閉方式）；設定支援儲存／關閉復原；釘選與刪除改為勾選式統一按鈕；新增全域快捷鍵；縮小底部設定列。

### 變更
- `Models/`：新增 `DisplayMode`、`WindowCloseBehavior`、`ThemeOption`、`ThemeCatalog`（10 色，含淺／深變體）；`AppSettings` 新增顯示模式、主題、關閉方式與 `Clone()`／`Normalize()`
- `Services/ThemeService.cs`：即時套用 `ElementTheme`，直接修改共用 `SolidColorBrush.Color`，跟隨系統時監聽 `ActualThemeChanged`
- `Services/HotkeyService.cs`：`RegisterHotKey`（Ctrl+Alt+V），收到 `WM_HOTKEY` 顯示面板
- `Services/SettingsService.cs`：JSON 使用 `JsonStringEnumConverter`，舊設定檔缺欄位時套預設
- `Services/ClipboardHistoryService.cs`：新增 `SetPinned`
- `ViewModels/SettingsViewModel.cs`、`ThemeSwatchViewModel.cs`：設定草稿／快照、即時預覽、儲存才落盤、關閉復原、主題 swatch 選取
- `ViewModels/HistoryPanelViewModel.cs`：勾選狀態追蹤、`HasSelection`、`PinSelected`／`DeleteSelected`、依關閉方式決定複製後是否關閉
- `Views/HistoryPanelWindow.xaml(.cs)`：右上角釘選／刪除／設定／X；每列 CheckBox；底部列縮成單列；覆蓋式設定頁；刪除與清除全部二次確認
- `App.xaml.cs`：注入 `ThemeService`、建立 `HotkeyService`；第二實例先 `AllowSetForegroundWindow`
- 新增 `Helpers/BoolToVisibilityConverter.cs`；`NativeMethods` 新增前景／熱鍵相關 P/Invoke
- 單元測試新增設定列舉／主題清單／`SetPinned`；版本 `1.1.0`

### 問題與解法
- **工作列第一次點擊閃一下即消失**：第二實例顯示面板時搶不到前景，`Deactivated` 後被自動隱藏。改為第二實例先 `AllowSetForegroundWindow(ASFW_ANY)`，顯示端以 `AttachThreadInput` 強制前景，並要求「顯示後曾啟用」且 1200ms 內不因失焦隱藏。
- **x:Bind + DataTemplate 轉換器造成 CS1503**：Window 不是 `FrameworkElement`，`SetConverterLookupRoot(this)` 失敗。改為在 VM 提供 `SelectedVisibility`，DataTemplate 不再使用轉換器。
- **設定無法保留（ThemeId 回到預設）**：`SettingsService.Load` 未傳入 `JsonOptions`，字串列舉解析失敗被 catch 成預設值。改為 `Deserialize(json, JsonOptions)`。
- **主題 swatch 點擊無效**：`ThemeSwatch_Click` 以 `DataContext` 取 `ThemeSwatchViewModel` 在 ItemsRepeater 範本中失敗。改為 Button `Tag="{x:Bind Id}"`，以主題識別碼選取。
- **建置失敗（檔案被占用）**：先前啟動的 `SaveClipboard.exe` 鎖住輸出檔，建置前先結束程序。
- **UIA 驗證找不到按鈕**：ToolTip 與按鈕同名，`FindFirst(Name)` 會命中 ToolTip。測試腳本改以 Name + ControlType.Button 篩選。

### 測試結果
- 單元測試 23/23 通過
- UIA 實測：面板顯示、設定頁開啟、10 個主題 swatch、2 個 ComboBox、儲存／關閉、主題「關閉復原／儲存保留」、顯示模式與關閉方式寫入 settings.json、手動模式失焦不關閉、自動模式失焦關閉、Ctrl+Alt+V 開啟面板
- `dotnet build` 0 警告 0 錯誤；`dotnet format --verify-no-changes` 通過

### 待辦／已知限制
- 工作列釘選第一次點擊需在實機再確認（前景鎖定行為依系統狀態而異）
- 10 款主題色在深色／高對比下的對比度需人工目視確認
- 快捷鍵固定為 Ctrl+Alt+V，若被占用僅記錄日誌，尚未提供自訂
- 未在 Windows 10 1809 實機驗證

---

## 2026-11-15 - v1.0.1

### 目標
修正面板底部橫線與按鈕重疊、清除全部按鈕溢出、系統匣圖示需點兩下才開啟；調整保存上限預設與最大值；簡化 App 圖示線條。

### 變更
- `Views/HistoryPanelWindow.xaml`：Grid 改為 4 列，分隔線獨立成列；底部設定列改兩排（上限／開機啟動、清除全部）
- `Models/AppSettings.cs`：`DefaultMaxItems=30`、`MinMaxItems=10`、`MaxMaxItems=50`
- `Helpers/NativeMethods.cs`、`Services/TrayIconService.cs`：新增 `NIN_SELECT`、`NIN_KEYSELECT`、`WM_CONTEXTMENU`，v4 與舊式系統匣訊息並存處理
- `Views/HistoryPanelWindow.xaml.cs`：面板已顯示時僅重新啟用；顯示時呼叫 `SetForegroundWindow`，並在 350ms 內忽略失焦隱藏
- `tools/generate-icon.ps1`：改為兩份文件、移除摺角、三條加粗文字線；重新產生 `Assets/AppIcon.ico`、`AppIcon-256.png`
- 更新單元測試預期值、README、CHANGELOG；版本 `1.0.1`

### 問題與解法
- **需點兩下才開啟面板**：程式使用 `NOTIFYICON_VERSION_4`，左鍵事件為 `LOWORD(lParam)` 的 `NIN_SELECT`，舊程式只處理 `WM_LBUTTONUP`，單擊被忽略、雙擊才觸發 `WM_LBUTTONDBLCLK`。改為同時處理 v4 與舊式訊息。
- **右鍵選單未顯示**：v4 右鍵事件為 `WM_CONTEXTMENU`，補上判斷。
- **底部橫線壓住按鈕**：分隔線與設定列同列，`Height=1` 被垂直置中；改為獨立列。
- **清除全部溢出**：400 DIP 寬度下單列放不下上限群組、開機啟動與清除全部；拆成兩排解決。

### 待辦／已知限制
- 圖示改為程式繪製，仍需人工目視確認 16px／深色與高對比下的辨識度
- 系統匣單擊與右鍵需在實機人工確認

## 2026-11-14 - v1.0.0

### 目標
從零開發 Windows 剪貼簿歷程常駐工具：監聽剪貼簿（文字 + 圖片）、可設定保存上限、釘選、系統列開啟彈出面板。

### 變更
- `src/SaveClipboard/`：WinUI 3（WindowsAppSDK 1.8, .NET 10）未封裝應用程式骨架
- `Helpers/NativeMethods.cs`、`Helpers/HelperWindow.cs`：P/Invoke（RegisterClassEx、AddClipboardFormatListener、Shell_NotifyIconW、TrackPopupMenu、FindWindow 等）與 1×1 隱藏工具視窗（接收 WM_CLIPBOARDUPDATE / WM_TRAYICON）
- `Services/ClipboardListenerService.cs`：WM_CLIPBOARDUPDATE → 讀 WinRT Clipboard，文字直接存；圖片經 BitmapDecoder 重新編碼為 PNG + 產生 96px 縮圖
- `Services/ClipboardHistoryService.cs`：歷程核心邏輯（上限淘汰、釘選保護、SHA256 去重置頂）+ 15 個單元測試
- `Services/SettingsService.cs`、`StartupService.cs`：`%AppData%\SaveClipboard\settings.json`；開機啟動寫 `HKCU\...\Run`
- `Views/HistoryPanelWindow.xaml(.cs)`：無邊框面板（SetBorderAndTitleBar(false,false)），失焦或 Esc 隱藏，AppWindow 定位至游標所在螢幕的工作區右下角
- `Services/TrayIconService.cs`：Shell_NotifyIcon 系統列圖示，左鍵開面板、右鍵 TrackPopupMenu（開啟／結束）
- 單一實例：Mutex；第二實例透過 FindWindow(工具視窗) + PostMessage(WM_SHOW_PANEL) 喚起既有面板
- `tools/generate-icon.ps1`：產生扁平多文件風格 `Assets/AppIcon.ico`（16–256 px）與 `AppIcon-256.png`
- `tools/e2e-check.ps1`、`tools/e2e-image.ps1`：UI Automation + 剪貼簿端到端檢查腳本

### 問題與解法
- **XamlParseException：`TabViewButtonBackground` 資源找不到（NumberBox）**：WinUI 1.8 的 NumberBox 預設樣式引用的鍵在此環境解析失敗（ToggleSwitch 同樣使用）。解法：NumberBox 改為自訂 −/+ 步進按鈕 + TextBlock；ToggleSwitch 改為 CheckBox。
- **XamlParseException：`DividerStrokeColorDefaultBrush`、`ControlStrokeColorDefaultBrush` 等 WinUI 內建 Brush 鍵不存在**：改用自訂 SolidColorBrush（DividerBrush / SubtleFillBrush / AccentBrush / DangerBrush）。
- **第 2 實例找不到隱藏的面板視窗**：FindWindow 對 WinUI 頂層視窗以 title 查詢失敗（class+title 才找得到），且隱藏視窗無法用 SW_SHOW 喚起；改以「工具視窗」PostMessage(WM_SHOW_PANEL) 通知主實例顯示面板。
- **點擊刪除後面板意外關閉**：刪除按鈕移除後焦點落到面板外，觸發 Deactivated → 隱藏。修正：Focus 收回 RootGrid + Deactivated 後延遲 220ms 才隱藏（使用者按 Esc 仍即時關閉）。
- **ListView 項目無障礙名稱顯示型別全名**：DataTemplate 根元素補 `AutomationProperties.Name="{Binding Preview}"`。
- **e2e 自動化限制**：測試環境無法取得前景焦點（前景視窗鎖定），且面板位置（工作區右下角）被 Chrome 覆蓋，真實滑鼠點擊無法自動化驗證；按鈕 Command 綁定改以 UIA Invoke 驗證（刪除成功），文字/圖片擷取、面板顯示、去重皆已自動驗證。真實滑鼠操作（點擊項目複製、釘選、拖曳區域外點擊關閉）需人工實機確認。
- **未設定 DataContext**：ListView 的 ItemsSource 綁定失效（歷程不顯示），改在 ctor 設定 RootGrid.DataContext。

### 測試結果
- 單元測試 16/16 通過（歷程淘汰、釘選保護、上限調整、去重、設定序列化）
- e2e：文字擷取（含去重）、圖片擷取（PNG 重編碼 + 縮圖）、面板顯示、刪除按鈕、單一實例、崩潰防護皆驗證通過
- `dotnet build` 0 錯誤 0 警告；`dotnet format` 通過

### 問題與解法（續）
- **第二次啟動後主進程自行結束（0xC000027B）**：與 XAML 解析例外同源，修復 XAML 後穩定存活超過 12 秒壓力觀察，且無 crash log。
- **PowerShell 5.1 執行含中文的 .ps1 解析錯位**：腳本改為純 ASCII。

### 待辦／已知限制
- 圖示產出為程式繪製，視覺細節需人工目視確認（AppIcon-256.png）
- 圖片歷程全部保存在記憶體，大量大圖可能增加記憶體用量（未設位元組上限）
- 未在 Windows 10 1809 實機驗證；目前於 Windows 11 + WinAppSDK 1.8 實測
- 未測試高對比主題下的圖示辨識度
- 未封裝發佈（MSIX）未驗證；目前以未封裝（WindowsPackageType=None）執行

## YYYY-MM-DD - vX.Y.Z

### 目標
（這次要做什麼、為什麼）

### 變更
- （檔案／功能）

### 問題與解法
- （錯誤訊息 → 最後怎麼解）

### 待辦／已知限制
- （尚未完成或未驗證的部分）
