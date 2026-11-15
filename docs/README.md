# SaveClipboard 文件索引

本目錄收錄 SaveClipboard 的使用、架構、設計、建置與隱私文件。若您第一次接觸本專案，建議依下列順序閱讀。

| 文件 | 對象 | 內容 |
| --- | --- | --- |
| [使用指南](USAGE.md) | 一般使用者 | 安裝、啟動、面板操作、鍵盤快捷鍵、設定與常見問題 |
| [系統架構與技術文件](ARCHITECTURE.md) | 開發者 | 技術堆疊、專案結構、服務職責、執行緒模型、資料流、P/Invoke 與生命週期 |
| [系統設計文件](DESIGN.md) | 開發者／維護者 | 需求、使用案例、UI 設計、資料模型、狀態機、錯誤處理、設計決策與限制 |
| [建置與發佈指南](BUILD.md) | 開發者 | 環境需求、建置、測試、格式化、x64／ARM64 發佈與 Release 流程 |
| [隱私與資料處理](PRIVACY.md) | 所有人 | 資料存放位置、加密方式、日誌內容、隱私標記與清除方式 |
| [開發紀錄](DEV_LOG.md) | 維護者 | 各版本目標、變更、問題與解法、待辦事項 |

## 其他文件

- 專案首頁：[README.md](../README.md)
- 版本歷程：[CHANGELOG.md](../CHANGELOG.md)
- 使用者版本說明：[RELEASE_NOTES.md](../RELEASE_NOTES.md)
- 貢獻指南：[CONTRIBUTING.md](../CONTRIBUTING.md)
- 安全性政策：[SECURITY.md](../SECURITY.md)
- 授權：[LICENSE](../LICENSE)

## 文件維護原則

- 文件以繁體中文撰寫，使用者可見名詞與介面文字保持一致。
- 功能或行為變更時，同步更新對應文件、`CHANGELOG.md` 與 `docs/DEV_LOG.md`。
- 文件中的路徑、版本與指令需與實際專案一致。
- 不要在本目錄記錄 API Key、密碼、個人資料或真實剪貼簿內容。
