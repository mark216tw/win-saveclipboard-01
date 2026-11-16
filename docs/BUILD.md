# 建置與發佈指南

本文件說明 SaveClipboard 的開發建置、測試、格式化、x64／ARM64 發佈與 GitHub Release 流程。

## 1. 開發環境

| 工具 | 版本／說明 |
| --- | --- |
| Windows | 10 1809（10.0.17763）以上 |
| .NET SDK | .NET 10（本專案以 10.0.404 驗證） |
| Windows App SDK | 1.8（`Microsoft.WindowsAppSDK` 1.8.260921001） |
| IDE | Visual Studio 2026、VS Code + C# Dev Kit 或 Rider |
| PowerShell | 7 以上（發佈腳本使用） |

確認環境：

```powershell
dotnet --version
dotnet workload list
```

本專案使用 Windows App SDK 的 NuGet 套件，不需要安裝額外 workload 即可建置，但執行未封裝程式時需安裝 **Windows App SDK 1.8 Runtime**。

## 2. 還原與建置

```powershell
dotnet restore
dotnet build
```

- 開發與測試一律使用 `Debug` 組態。
- `TreatWarningsAsErrors=true`，警告視為錯誤。
- 建置輸出：

```text
src\SaveClipboard\bin\Debug\net10.0-windows10.0.19041.0\SaveClipboard.exe
```

直接執行：

```powershell
.\src\SaveClipboard\bin\Debug\net10.0-windows10.0.19041.0\SaveClipboard.exe
```

## 3. 測試

```powershell
dotnet test
```

- 測試專案：`tests/SaveClipboard.Tests`
- 測試框架：xUnit
- 目前測試數以 `docs/DEV_LOG.md` 最新紀錄為準。
- 涉及系統匣、前景切換、DPI 與多螢幕的行為無法完全自動化，仍需實機確認。

只跑特定測試：

```powershell
dotnet test --filter FullyQualifiedName~ClipboardHistoryServiceTests
```

## 4. 格式化

```powershell
dotnet format --verify-no-changes
```

若需要自動修正：

```powershell
dotnet format
```

送出變更前請確認 `--verify-no-changes` 通過。

## 5. 發佈架構

本專案提供兩種架構的 **framework-dependent（非 Self-Contained）** 版本：

| 識別碼 | 架構 | 適用裝置 |
| --- | --- | --- |
| `win-x64` | x64 | 多數 Intel／AMD Windows 電腦 |
| `win-arm64` | ARM64 | Snapdragon X 系列等 Windows on ARM 裝置 |

發佈設定：

- `SelfContained=false`：需要 .NET 10 Desktop Runtime。
- `WindowsAppSDKSelfContained=false`：需要 Windows App SDK 1.8 Runtime。
- `WindowsPackageType=None`：未封裝模式。
- `PlatformTarget` 與 `Platform` 需與 RID 一致。

## 6. 發佈 x64

```powershell
dotnet publish src\SaveClipboard `
  -c Release `
  -r win-x64 `
  -p:SelfContained=false `
  -p:WindowsAppSDKSelfContained=false `
  -p:WindowsPackageType=None `
  -p:PlatformTarget=x64 `
  -p:Platform=x64 `
  -o artifacts\publish\win-x64
```

## 7. 發佈 ARM64

```powershell
dotnet publish src\SaveClipboard `
  -c Release `
  -r win-arm64 `
  -p:SelfContained=false `
  -p:WindowsAppSDKSelfContained=false `
  -p:WindowsPackageType=None `
  -p:PlatformTarget=arm64 `
  -p:Platform=arm64 `
  -o artifacts\publish\win-arm64
```

> `SaveClipboard.csproj` 的 `Platforms` 目前列出 `x64`；發佈 ARM64 時以上述 `-p:Platform=arm64` 覆寫即可。若要在 IDE 中直接切換 ARM64，請將 `Platforms` 改為 `x64;ARM64`。

## 8. 打包 zip

發佈輸出包含 `.pdb` 偵錯符號檔。Release 資產可選擇排除：

```powershell
$version = "1.5.1"
$root = "artifacts\publish"

# x64
$zipX64 = "artifacts\SaveClipboard-$version-win-x64.zip"
if (Test-Path $zipX64) { Remove-Item $zipX64 }
Compress-Archive -Path "$root\win-x64\*" -DestinationPath $zipX64 -CompressionLevel Optimal

# ARM64
$zipArm = "artifacts\SaveClipboard-$version-win-arm64.zip"
if (Test-Path $zipArm) { Remove-Item $zipArm }
Compress-Archive -Path "$root\win-arm64\*" -DestinationPath $zipArm -CompressionLevel Optimal
```

若要排除 PDB：

```powershell
Get-ChildItem "$root\win-x64" -Recurse -Include *.pdb | Remove-Item -Force
Get-ChildItem "$root\win-arm64" -Recurse -Include *.pdb | Remove-Item -Force
```

zip 解壓後應可直接執行 `SaveClipboard.exe`，內容包含：

```text
SaveClipboard.exe
SaveClipboard.dll
SaveClipboard.deps.json
SaveClipboard.runtimeconfig.json
Assets\AppIcon.ico
Assets\AppIcon-256.png
共用的 .NET／Windows App SDK 相依 DLL
```

## 9. 版本管理

版本設定位於 `Directory.Build.props`：

```xml
<Version>1.5.1</Version>
<AssemblyVersion>1.5.0.0</AssemblyVersion>
<FileVersion>1.5.1.0</FileVersion>
```

| 欄位 | 規則 |
| --- | --- |
| `Version` | 主.次.修；每次功能或文件釋出遞增 |
| `AssemblyVersion` | 主次版變更時才調整 |
| `FileVersion` | 對應 `Version` |

同步更新：

- `CHANGELOG.md`
- `RELEASE_NOTES.md`
- `docs/DEV_LOG.md`

## 10. Release 檢查清單

1. 更新版本與文件。
2. 執行：

   ```powershell
   dotnet build
   dotnet test
   dotnet format --verify-no-changes
   ```

3. 以 Release 組態發佈 x64 與 ARM64。
4. 在乾淨環境或虛擬機測試解壓後可啟動（需安裝 Runtime）。
5. 建立 GitHub Release：

   ```powershell
   gh release create v1.5.1 `
     artifacts\SaveClipboard-1.5.1-win-x64.zip `
     artifacts\SaveClipboard-1.5.1-win-arm64.zip `
     --title "SaveClipboard 1.5.1" `
     --notes-file RELEASE_NOTES.md
   ```

6. 確認 Release 資產名稱、大小與 `RELEASE_NOTES.md` 內容。
7. 確認 `LICENSE` 被 GitHub 識別為 MIT。

## 11. 常見建置問題

### 找不到 Windows App SDK 資源

- 確認 `Directory.Packages.props` 的 `Microsoft.WindowsAppSDK` 版本已還原。
- 清除 `bin/`、`obj/` 後重新 `dotnet restore`。

### XAML 編譯錯誤（WMC0001／WMC0909）

- 多為 C# 編譯失敗導致的連鎖錯誤，先修正 C# 錯誤。
- 確認 `x:Bind` 的型別與轉換器可用。

### ARM64 發佈失敗

- 確認有覆寫 `-p:Platform=arm64 -p:PlatformTarget=arm64`。
- 確認已安裝對應的 .NET SDK 與 Windows App SDK 套件。

### 執行時提示缺少 Runtime

- 安裝 .NET 10 Desktop Runtime（x64 或 ARM64）。
- 安裝 Windows App SDK 1.8 Runtime（x64 或 ARM64）。
- 本專案 Release zip 不包含上述 Runtime。

### 系統匣圖示未出現

- 檢查 Windows 系統匣溢出區。
- 重新啟動 Explorer 後圖示應自動恢復。
- 查看 `%LOCALAPPDATA%\SaveClipboard\Logs\app.log`。

## 12. 不要提交的檔案

`.gitignore` 已排除：

- `bin/`、`obj/`
- `artifacts/`、`publish/`
- `.vs/`、`.vscode/`
- `TestResults/`、覆蓋率檔
- `*.user`、`*.suo`
- MSIX／憑證檔
- 本機設定與機密檔

Release 資產（zip）不進版控，只上傳到 GitHub Release。
