# 安全缺陷分級矩陣

**權威來源**：OWASP CVSS 規範、NIST SP 800-30 風險評估框架

**適用對象**：QA/QC 執行安全驗證時的分級依據、Orchestrator 在「階段五：安全缺陷回應協調」的決策依據。

---

## 分級定義

| 等級 | CVSS 分數對照 | 定義 | 處理方式 |
|------|--------------|------|---------|
| **Critical** | 9.0 – 10.0 | 可直接導致資料洩漏、未授權存取、系統完全淪陷，**無需特殊條件即可利用** | 🚫 阻擋合併，立即退回對應 agent，不得以任何理由放行 |
| **High** | 7.0 – 8.9 | 安全機制實作有缺陷但非直接可利用，或需特定條件觸發 | 🚫 阻擋合併，優先修正 |
| **Medium** | 4.0 – 6.9 | 安全最佳實踐未遵循但風險有限 | ⚠️ 標記於 PR 描述，人類決定是否阻擋 |
| **Low** | 0.1 – 3.9 | 改善建議 | 📝 記錄於 Issue 追蹤，不阻擋當次交付 |

---

## OWASP 類別 × 分級速查表

### Web Application（OWASP Top 10:2025）

| OWASP | Critical 觸發條件 | High 觸發條件 | Medium 觸發條件 |
|-------|-----------------|---------------|----------------|
| **A01 Broken Access Control** | 端點缺授權檢查、IDOR 可重現、SSRF 可到內網 | 前後端授權不一致、授權邏輯有繞過路徑 | 前端路由守衛缺失（後端已防護）|
| **A02 Security Misconfiguration** | 生產環境暴露 debug 端點、預設帳密、Cloud Bucket 公開 | 安全 HTTP Header 缺失、Swagger 未受保護 | 錯誤頁面格式不一致 |
| **A03 Software Supply Chain** | 使用已知高危 CVE 套件 | 無 lockfile、無 SCA 掃描 | 無 SBOM |
| **A04 Cryptographic Failures** | 硬編碼金鑰、明文儲存密碼、使用 MD5/SHA1 雜湊密碼 | 使用 AES-ECB 而非 GCM、金鑰管理機制薄弱 | 使用較弱的 bcrypt cost（< 12）|
| **A05 Injection** | SQL Injection 可重現、XSS 可執行任意 JS | 字串串接 SQL（即使當前 input 無注入風險）、使用 `innerHTML` | CSP 設定過寬 |
| **A06 Insecure Design** | 缺乏威脅建模且有實際高風險缺陷 | 業務邏輯濫用防護缺失 | 威脅建模流於形式 |
| **A07 Authentication Failures** | 登入失敗訊息洩漏帳號存在性、Token 可重放、無 Session 過期 | JWT Lifetime 未驗證、Refresh Token 無 rotation | MFA 可選但預設不啟用 |
| **A08 Software and Data Integrity** | 反序列化接受任意型別、CI/CD 無產物簽章 | CDN 資源無 SRI、更新機制無驗簽 | 內部工具產物無簽章 |
| **A09 Security Logging** | 無任何安全事件日誌 | 日誌中有密碼/Token 洩漏、無警報機制 | 認證失敗未記錄 |
| **A10 Mishandling of Exceptional Conditions** | 錯誤回應洩漏 Stack Trace / SQL / 連線字串、Fail-Open | 無全域 Exception Filter、部分錯誤路徑未處理 | 錯誤碼不一致 |

### API Security（OWASP API Top 10:2023）

| OWASP | Critical | High | Medium |
|-------|----------|------|--------|
| **API1 BOLA** | 可存取他人資源（重現） | 授權邏輯有 corner case 漏洞 | UUID 使用自增 ID（已有授權檢查）|
| **API2 Broken Authentication** | 敏感操作無需密碼確認 | JWT 驗證配置不完整 | 密碼重設 Token 過期時間過長 |
| **API3 BOPLA** | 直接 serialize Domain Entity 含密碼 | DTO 有多餘敏感欄位 | DTO 命名不一致 |
| **API4 Unrestricted Resource Consumption** | 無 Rate Limiting 於登入端點 | 檔案上傳無大小限制 | 分頁 pageSize 無上限 |
| **API5 BFLA** | 一般使用者可呼叫 admin 端點 | Role 檢查實作不完整 | Role 命名不一致 |
| **API6 Sensitive Business Flows** | 關鍵業務流程無濫用防護 | CAPTCHA 可繞過 | 僅依賴帳號限流 |
| **API7 SSRF** | 可到 cloud metadata endpoint | URL 驗證不完整 | 僅驗證 scheme |
| **API8 Security Misconfiguration** | CORS `*` 配 credentials | 不必要 HTTP 方法開啟 | 過時 API 版本未標註 |
| **API9 Improper Inventory** | 生產環境有 dev 端點 | 舊版 API 無 deprecation 標註 | API 文件不完整 |
| **API10 Unsafe Consumption** | 第三方 API 回傳直接信任無驗證 | 無 timeout / 重試策略 | 無熔斷機制 |

### LLM Applications（OWASP LLM Top 10:2025）

| OWASP | Critical | High | Medium |
|-------|----------|------|--------|
| **LLM01 Prompt Injection** | Prompt Injection 可觸發工具濫用或洩漏系統 prompt | RAG 內容未分隔標註、無 output 驗證 | Prompt 內容過度信任 |
| **LLM02 Sensitive Information Disclosure** | LLM 輸出洩漏他人個資 | Output 無 PII 過濾 | Log 未脫敏 |
| **LLM03 Supply Chain** | 從不信任來源載入 skill 或 MCP server | 模型下載無 checksum | 第三方 skill 無審查流程 |
| **LLM04 Data and Model Poisoning** | RAG 資料庫可被匿名寫入 | RAG 寫入無授權管控 | 無相關性閾值 |
| **LLM05 Improper Output Handling** | LLM 產出程式碼直接 eval、LLM 產出 SQL 直接執行 | LLM 輸出未經驗證即渲染 HTML | URL 驗證不完整 |
| **LLM06 Excessive Agency** | Agent 擁有超範圍工具且可執行不可逆操作、無 human-in-the-loop 於生產 merge | `tools` 欄位過於寬鬆、MCP server 白名單缺失 | Agent 可跨職責存取資料 |
| **LLM07 System Prompt Leakage** | System prompt 含硬編碼 API Key、密碼 | Prompt 描述內部 URL、生產主機名 | Prompt 洩漏無關緊要的內部邏輯 |
| **LLM08 Vector and Embedding Weaknesses** | Vector DB 無租戶隔離 | Embedding 無存取控制 | 相似度閾值過低 |
| **LLM09 Misinformation** | LLM 幻覺被直接採納於生產決策、未經跨域檢視 | SA/SD 技術選型理由無法驗證 | Commit 訊息宣稱無依據 |
| **LLM10 Unbounded Consumption** | Agent 無 maxTurns 導致無限遞迴 | 無 token 上限、無 timeout | 單使用者無 quota |

### 個資法合規

| 項目 | Critical | High | Medium |
|------|----------|------|--------|
| **特種個資處理** | 特種個資未依法取得同意、未加密儲存 | 特種個資無獨立存取控制 | 特種個資稽核不完整 |
| **一般個資高敏欄位** | 身分證字號明文儲存、信用卡號自存 | 加密強度不足 | 加密金鑰管理薄弱 |
| **告知義務** | 無隱私權政策、無同意機制 | 同意紀錄缺失 | 政策版本追蹤不完整 |
| **當事人權利** | 無法行使刪除權 | 刪除僅為標記非實質刪除 | 下載功能 UX 差 |
| **稽核軌跡** | 個資存取無任何日誌 | 稽核紀錄與業務庫同 DB | 稽核保存期限不足 |
| **測試資料** | 測試環境使用真實個資 | CI log 含真實個資 | 測試腳本含範例 email 不夠假 |
| **跨境傳輸** | 未告知使用者海外傳輸事實 | DPA 缺失 | 合規文件過期 |

---

## 特殊情境加權規則

### 情境 1：缺陷組合放大

**當多個 Medium 缺陷可組合成 Critical 攻擊鏈時，整體視為 Critical**：
- 例：Medium 的 CSRF + Medium 的 SSRF → 組合可從外部觸發 SSRF → Critical
- 例：Medium 的資訊洩漏 + Medium 的弱密碼政策 → 組合可暴力破解 → High

### 情境 2：多租戶 / 多使用者場景

**影響範圍越大，嚴重度越高**：
- 單一使用者資料洩漏 → 依 OWASP 分級
- 可跨租戶/跨使用者洩漏 → **升一級**
- 可洩漏全體使用者資料 → **Critical 不得降級**

### 情境 3：管理員層級操作

**管理員功能的缺陷嚴重度高於一般使用者**：
- 一般使用者可做越權 → 依 OWASP 分級
- 攻擊者可取得管理員權限 → **Critical 不得降級**

### 情境 4：不可逆操作

**涉及不可逆操作的缺陷嚴重度加權**：
- 刪除資料、支付、庫存扣減的缺陷 → **升一級**
- 例：Medium 的驗證不足 + 不可逆扣款 → High

---

## QA/QC 的分級判斷流程

當發現疑似安全缺陷時，依以下順序判斷：

```
1. 對照 OWASP 類別 → 取得基線嚴重度
   │
2. 套用特殊情境加權規則 → 調整嚴重度
   │
3. 確認是否為 Critical/High → 決定是否阻擋合併
   │
4. 產出退回報告（含溯源、建議修正、嚴重度說明）
   │
5. 若為 Medium → 寫入 PR 描述，交人類決定
   │
6. 若為 Low → 建立 Issue 追蹤，不阻擋
```

---

## Orchestrator 的回應流程（對應 orchestrator.agent.md 階段五）

```
QA/QC 回報安全缺陷
   │
   ├─ Critical / High → 🚫 阻擋 PR 合併，退回對應 agent 優先修正
   │      │
   │      └─ 修正完成 → 重新 QA/QC 安全驗證 → 通過才能合併
   │
   ├─ Medium → ⚠️ 寫入 PR 描述，附修正建議與影響評估
   │      │
   │      └─ 由人類決定：立即修正 / 建 follow-up issue / 接受風險
   │
   └─ Low → 📝 建立 Issue 追蹤，不阻擋當次交付
```

---

## 豁免機制

**少數情境下，Critical/High 缺陷可例外放行**，但必須滿足所有條件：

1. 有明確的業務必要性（且文件化）
2. 有對應的**補償控制（Compensating Control）**，經 SA/SD 評估有效
3. 有明確的修正期限（不超過 90 天）
4. 人類（非 Orchestrator）批准簽核
5. 建立追蹤 Issue 與 ADR 記錄

豁免的缺陷仍須於下次迭代中修正，且每週狀態追蹤。

**禁止豁免的情境**：
- 涉及真實個資洩漏風險
- 涉及支付、金融相關缺陷
- 涉及特種個資處理
- 法遵要求的硬性規範

---

## 分級品質保證

QA/QC 的分級判斷本身也需要品質保證：

- **過度嚴苛** → 開發流程被不必要阻塞，降低團隊生產力
- **過度寬鬆** → 缺陷進入生產，違反本系統存在意義

**定期校準**：
- 每季檢視過去的分級決策
- 比對實際事件或第三方 Pentest 結果
- 必要時更新本矩陣並透過 ADR 記錄
