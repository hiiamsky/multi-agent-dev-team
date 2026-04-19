---
name: security-baseline
description: 企業級 SSDLC 安全基線，涵蓋 OWASP Web App Top 10:2025、OWASP API Security Top 10:2023、OWASP LLM Top 10:2025 三套權威規則，外加軟體供應鏈具體工具鏈、台灣個資法合規要求、安全缺陷分級矩陣。當 Agent 進入實作、跨域檢視、或 QA/QC 安全驗證階段時載入。
when_to_use: 任何 agent 涉及程式碼產出、資料存取、API 設計、權限判斷、AI/LLM 功能設計、處理個資、或需要進行安全審查時
---

# 安全基線（跨 Agent 共用）

本 skill 是本 multi-agent 團隊的安全真理來源。所有 agent 的安全職責描述應引用本 skill，而非各自重複定義 OWASP 規則。

## 核心原則（全域強制，所有 Agent 適用）

1. **機敏資訊零硬編碼**：程式碼、設定檔、commit history、測試腳本中不得出現密碼、Token、連線字串、加密金鑰、API Key、個資明文。
2. **預設拒絕（Deny by Default）**：未明確標註為公開的端點一律要求認證；未明確標註為安全的輸入一律視為不可信。
3. **最小權限原則（Least Privilege）**：每個角色、帳號、元件、agent 只擁有完成職責所需的最小權限。
4. **安全左移（Shift-Left Security）**：缺陷越早發現修正成本越低；SA/SD 階段必須產出安全設計章節，而非等到 QA/QC 才補。
5. **Fail-Safe，不 Fail-Open**：授權失敗時拒絕存取；加密失敗時拒絕儲存；外部服務失敗時拒絕繼續——絕不以「可用性優先」為由開後門。

## 適用對象對照表

| Agent | 必讀章節 | 選讀章節 |
|-------|---------|---------|
| Orchestrator | severity-matrix.md（缺陷分級決策用） | — |
| SA/SD | owasp-web-top10.md §威脅建模、owasp-api-top10.md、owasp-llm-top10.md、pdpa-compliance.md | supply-chain-tooling.md |
| 後端 PG | owasp-web-top10.md、owasp-api-top10.md、pdpa-compliance.md §個資處理 | supply-chain-tooling.md |
| 前端 PG | owasp-web-top10.md §A01/A05/A07、pdpa-compliance.md §前端個資遮蔽 | — |
| DBA | owasp-web-top10.md §A01/A04/A09、pdpa-compliance.md §個資儲存 | — |
| QA/QC | 全部六份檔案 | — |
| E2E 測試 | owasp-web-top10.md §A01/A07、pdpa-compliance.md §測試資料 | — |

## 三層 OWASP 規則的呼叫時機

- **需求淨化階段（Orchestrator）** → 依安全標籤判斷是否觸發下游安全設計：
  - 涉及認證/授權 → SA/SD 載入 owasp-web-top10.md §A01/A07、owasp-api-top10.md §API1/2/5
  - 涉及敏感資料處理 → SA/SD 載入 owasp-web-top10.md §A04、pdpa-compliance.md
  - 涉及外部輸入 → SA/SD 載入 owasp-web-top10.md §A05、owasp-api-top10.md §API10
  - 涉及 AI/LLM 能力 → SA/SD 載入 owasp-llm-top10.md
  - 涉及第三方依賴變更 → SA/SD 載入 supply-chain-tooling.md

- **架構設計階段（SA/SD）** → 產出安全設計章節，引用本 skill 對應條目

- **實作階段（PG/DBA）** → 依角色對照表載入必讀章節

- **驗證階段（QA/QC）** → 全部章節 + severity-matrix.md

## 缺陷分級與阻擋規則

詳見 severity-matrix.md。摘要：

- **Critical / High** → 🚫 阻擋合併，立即退回對應 agent
- **Medium** → ⚠️ 標記於 PR 描述，由人類決定
- **Low** → 📝 記錄於 Issue 追蹤，不阻擋當次交付

## 子檔案清單

| 檔案 | 內容 | 來源權威性 |
|------|------|----------|
| owasp-web-top10.md | OWASP Web Application Top 10:2025（10 項） | https://owasp.org/Top10/2025/ |
| owasp-api-top10.md | OWASP API Security Top 10:2023（10 項） | https://owasp.org/API-Security/editions/2023/ |
| owasp-llm-top10.md | OWASP Top 10 for LLM Applications:2025（10 項） | https://genai.owasp.org/llm-top-10/ |
| supply-chain-tooling.md | 軟體供應鏈具體工具鏈（SCA、SBOM、lockfile） | NIST SSDF SP 800-218 + OWASP A03:2025 |
| pdpa-compliance.md | 台灣個資法合規要求與特別類個資處理 | 個人資料保護法（2023 年修正）|
| severity-matrix.md | 安全缺陷 Critical/High/Medium/Low 分級矩陣 | OWASP CVSS 規範 + 本系統設計 |

## 斷鏈防護規則

若下游 agent 的產出未落實本 skill 中對應的安全規範，QA/QC 必須視為缺陷退回：

| 斷鏈情境 | 退回對象 | 嚴重度 |
|---------|---------|--------|
| Orchestrator 安全標籤被勾選，但 SA/SD 藍圖缺少對應安全設計章節 | SA/SD | Critical |
| SA/SD 定義加密欄位，但 DBA Schema 未使用對應型別 | DBA | High |
| SA/SD 定義端點權限矩陣，但後端程式碼缺少授權檢查 | 後端 PG | Critical |
| SA/SD 定義敏感欄位遮蔽規則，但前端 Response 處理未落實 | 前端 PG | High |
| 個資欄位未依 pdpa-compliance.md 加密或遮蔽 | 後端 PG / DBA | Critical |
| Agent tools 欄位超出任務所需（Excessive Agency） | 本 skill / agent 定義修改者 | High |

## 更新機制

本 skill 引用的外部規範（OWASP、NIST SSDF、個資法）更新時，必須透過 ADR 記錄變更：

1. 建立 `ADR-XXX-security-baseline-update.md`
2. 標註被更新的章節與新舊規則差異
3. Commit 訊息加入 `⚠️ MUST-READ` 旗標，讓所有 agent 下次啟動時感知
