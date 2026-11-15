# 貢獻指南

感謝您願意協助改進 SaveClipboard。本專案以 Windows、.NET 10 與 WinUI 3 開發，請在送出變更前先閱讀以下規範。

## 開發環境

- Windows 10 1809（10.0.17763）以上
- .NET 10 SDK
- Windows App SDK 1.8
- 支援 C# 14／file-scoped namespace／nullable reference types 的 IDE（Visual Studio 2026 或 VS Code + C# Dev Kit 皆可）

## 取得原始碼與建置

```powershell
git clone https://github.com/mark216tw/win-saveclipboard-01.git
cd win-saveclipboard-01
dotnet build
dotnet test
dotnet format --verify-no-changes
```

- 開發與測試一律使用 `Debug` 組態。
- 除非維護者要求，請不要自行建立 `Release` 或變更版本號。
- 所有套件版本集中於 `Directory.Packages.props`，請勿在 `*.csproj` 寫死版本。

## 程式風格

- 4 空格縮排；公開型別與檔名一致。
- 類別／方法使用 PascalCase，區域變數與參數使用 camelCase，私有欄位使用 `_camelCase`。
- 非同步方法以 `Async` 結尾並回傳 `Task`／`Task<T>`。
- 使用者可見文字放在 `src/SaveClipboard/Strings/**/*.resw`，不要寫死在 XAML 或程式碼。
- 樣式與顏色放在 `Styles/` 資源字典，使用 `ThemeResource`／`StaticResource`。
- 不要新增未在 `Directory.Packages.props` 宣告的套件。
- 不要留下 `TODO`、測試假資料、`Console.WriteLine` 或 `Debug.WriteLine`。

## 測試

- 商業邏輯與純函式請補 xUnit 測試，放在 `tests/SaveClipboard.Tests/`。
- 送出前請執行：

```powershell
dotnet build
dotnet test
dotnet format --verify-no-changes
```

- 涉及系統匣、全域快捷鍵、前景切換、DPI 與多螢幕的行為，請在 Pull Request 描述中註明實機驗證結果。

## 送出變更

1. 從 `main` 建立分支，例如 `fix/tray-single-click`。
2. 保持變更聚焦，避免混入無關的格式調整。
3. 更新 `docs/DEV_LOG.md`，記錄目標、變更、問題與解法。
4. 若功能有使用者可見變更，同步更新 `CHANGELOG.md` 與 `RELEASE_NOTES.md`。
5. 確認 `git status` 沒有 `bin/`、`obj/`、`.vs/`、日誌或機密檔案。
6. 建立 Pull Request，說明動機、作法、測試方式與已知限制。

## 版本與文件

- 版本號在 `Directory.Build.props`：`Version` 為主.次.修，`FileVersion` 對應版號，`AssemblyVersion` 只在主次版變更時調整。
- 文件使用繁體中文；程式碼註解依現有檔案語言保持一致。
- 不要提交 API Key、密碼、憑證或個人資料。

## 授權

送出貢獻即表示您同意以本專案的 [MIT License](LICENSE) 授權您的貢獻。
