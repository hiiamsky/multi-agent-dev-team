# 台灣個資法合規規範

**權威來源**：個人資料保護法（2023 年修正）、個人資料保護法施行細則

**適用情境**：系統儲存、處理、傳輸中華民國境內任何個人資料時。

---

## 法規基本定義

依《個資法》第 2 條：

| 類型 | 定義 | 範例 |
|------|------|------|
| **一般個資** | 可識別自然人之資料 | 姓名、地址、電話、Email、身分證字號、出生年月日、IP 位址（能連結到個人時）|
| **特種個資** | 病歷、醫療、基因、性生活、健康檢查、犯罪前科 | 病歷號、健保卡號、基因檢測結果 |

**處理特種個資原則上禁止**，除非符合《個資法》第 6 條例外情形（法律明文規定、當事人書面同意、為增進公共利益、學術研究等）。

---

## SA/SD 階段規範：個資盤點與設計

### 1. 資料流盤點

在 SA/SD 藍圖中必須產出**個資流向表**：

| 資料欄位 | 類別 | 蒐集來源 | 儲存位置 | 處理目的 | 保存期限 | 第三方傳輸 |
|---------|------|---------|---------|---------|---------|-----------|
| email | 一般 | 註冊表單 | users.email | 帳號識別 | 帳號存續期 | 無 |
| 身分證字號 | 一般（高敏） | KYC 流程 | users.id_number_encrypted | 實名驗證 | 法遵要求 5 年 | 無 |
| 病歷資料 | **特種** | 醫療機構 API | medical_records | 掛號業務 | 7 年 | 無 |

### 2. 告知義務設計（個資法第 8 條）

系統蒐集個資前必須告知：
- 蒐集機關名稱
- 蒐集目的
- 個資類別
- 利用期間、地區、對象、方式
- 當事人得行使之權利（查閱、複製、補充更正、停止處理、刪除）

**設計要求**：
- 註冊流程必須有獨立的隱私權政策頁面（非僅 checkbox）
- 同意紀錄必須儲存（時間戳、IP、同意版本）

### 3. 當事人權利機制設計

系統必須提供當事人行使以下權利的功能（個資法第 3 條）：
- 查閱、複製
- 補充、更正
- 停止蒐集、處理、利用
- **刪除**

**設計要求**：
- 使用者中心必須有「資料下載」與「帳號刪除」功能
- 刪除必須是實質刪除或不可逆的匿名化，非僅標記 `is_deleted = true`

---

## 後端 PG 實作規範：個資處理

### 1. 敏感欄位處理對照

| 欄位 | 儲存方式 | API 回傳規則 | 查詢規則 |
|------|---------|-------------|---------|
| 身分證字號 | AES-256-GCM 加密 | 預設遮蔽為 `A12****789` | 需權限控制 |
| 手機號碼 | 明文可（視業務） | 預設遮蔽為 `09**-***-678` | 完整號碼需權限 |
| Email | 明文可 | 預設遮蔽為 `u***@example.com` | 完整需權限 |
| 信用卡號 | **絕不自存**，使用 PCI DSS 合規的 tokenization 服務 | 僅回傳末四碼 | - |
| 密碼 | bcrypt / Argon2 | **絕不回傳** | - |
| 病歷資料 | AES-256-GCM 加密 + 存取稽核 | 完整遮蔽 | 需醫療人員角色 |
| 健康檢查資料 | AES-256-GCM 加密 | 完整遮蔽 | 需本人或醫療人員 |

### 2. 遮蔽邏輯實作

遮蔽必須在**後端完成**，而非依賴前端：

```csharp
public static class PiiMaskingExtensions
{
    public static string MaskIdNumber(this string idNumber)
        => idNumber == null ? null : $"{idNumber[..1]}{new string('*', 7)}{idNumber[^2..]}";

    public static string MaskPhone(this string phone)
        => phone == null ? null : $"{phone[..2]}**-***-{phone[^3..]}";

    public static string MaskEmail(this string email)
    {
        if (string.IsNullOrEmpty(email)) return email;
        var at = email.IndexOf('@');
        if (at < 2) return email;
        return $"{email[..1]}***{email[at..]}";
    }
}
```

**DTO 層強制使用遮蔽版本**：
```csharp
public class UserResponseDto
{
    public string Email { get; init; }       // 已遮蔽
    public string Phone { get; init; }       // 已遮蔽
    public string IdNumber { get; init; }    // 已遮蔽
}
```

### 3. 存取稽核（個資法第 18 條）

處理個資時必須有完整的稽核軌跡：

| 事件 | 記錄內容 |
|------|---------|
| 個資查詢 | 查詢者 ID、時間、被查詢對象、查詢欄位、查詢目的 |
| 個資修改 | 修改者 ID、時間、變更前後內容（敏感欄位以 hash 記錄）|
| 個資刪除 | 刪除者 ID、時間、被刪除對象、刪除原因 |
| 個資匯出 | 匯出者 ID、時間、匯出範圍、匯出格式 |

**實作要求**：
- 稽核紀錄必須獨立於業務資料庫（避免應用程式帳號可竄改）
- 稽核紀錄保存至少 5 年
- 稽核紀錄本身不得含有完整明文個資

### 4. 資料最小化原則

- API 設計只回傳業務必要的欄位
- 查詢語句使用 `SELECT specific_columns` 而非 `SELECT *`
- Log 中禁止記錄完整個資

---

## 前端 PG 實作規範：個資遮蔽

### 1. 預設顯示遮蔽版本

- 列表、表格、搜尋結果**預設顯示遮蔽後的值**
- 完整值需使用者主動點擊「顯示完整」按鈕，且觸發稽核紀錄

### 2. 前端禁止事項

- ❌ 禁止在 `localStorage` / `sessionStorage` 儲存身分證字號、手機號碼、Email
- ❌ 禁止在 URL query string 傳遞個資（會落在瀏覽器歷史、伺服器 log）
- ❌ 禁止在 `console.log` 輸出個資（即使開發階段）
- ❌ 禁止將個資傳至第三方分析工具（GA、Mixpanel）未經去識別化

### 3. 表單輸入處理

- 身分證字號、信用卡號輸入欄位使用 `autocomplete="off"`（敏感欄位不讓瀏覽器記憶）
- 密碼欄位使用 `<input type="password">`，切換明文後必須還原
- 防截圖/防錄影需求：敏感畫面可考慮 `-webkit-user-select: none` + `pointer-events: none` 對內容層

---

## DBA 實作規範：個資儲存

### 1. Schema 標註規範

個資欄位在 Schema 註解中必須明確標註：

```sql
CREATE TABLE users (
    id              UNIQUEIDENTIFIER PRIMARY KEY,
    email           NVARCHAR(256) NOT NULL,        -- PII: 一般個資
    phone           NVARCHAR(20),                  -- PII: 一般個資
    id_number       VARBINARY(256),                -- PII: 一般個資（高敏），AES-256-GCM 加密
    password_hash   VARCHAR(72) NOT NULL,          -- bcrypt，cost=12
    created_at      DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    -- ... 稽核欄位
);
```

### 2. 資料庫加密層級

| 資料類型 | 加密層級 |
|---------|---------|
| 一般個資（email、phone） | 可明文儲存（DB 檔案層級加密即可） |
| 一般個資（身分證字號、護照號碼） | **欄位層級加密（AES-256-GCM）** |
| 特種個資（病歷、健康資料） | **欄位層級加密 + 獨立資料庫實例 + 存取控制** |
| 信用卡號 | **絕不自存**，使用 PCI DSS 合規的 tokenization 服務 |

### 3. 備份與還原

- 備份檔必須加密（SQL Server TDE 或 OS 層級加密）
- 備份檔的存取權限獨立管控
- 還原測試環境時，個資必須先脫敏或使用合成資料

### 4. 退休與刪除

- 帳號刪除時，關聯個資必須一併處理：
  - **硬刪除**：直接 DELETE（有業務價值的關聯資料例外處理）
  - **匿名化**：將個資欄位覆寫為 `<deleted>` 或 hash 值，保留非個資欄位供統計
- 不可僅靠 `is_deleted = true` 標記（資料仍可被查詢、備份中仍存在）

---

## E2E 測試規範：測試資料

**禁止使用真實個資於測試**：

- 測試腳本中絕不使用真實姓名、身分證字號、手機號碼、Email
- 使用 faker 函式庫產生合成資料：
  ```typescript
  import { faker } from '@faker-js/faker/locale/zh_TW';

  const testUser = {
    name: faker.person.fullName(),
    email: faker.internet.email(),
    phone: faker.phone.number(),
    idNumber: generateFakeTwIdNumber(),  // 自製符合格式但明顯假的
  };
  ```
- 截圖、Trace、錄影產物可能外洩至 CI log，必須確保內含個資皆為合成資料

---

## QA/QC 驗證檢查清單

### 設計層
- [ ] SA/SD 藍圖有個資流向表
- [ ] SA/SD 藍圖有標註特種個資處理方式
- [ ] 隱私權政策頁面與同意機制設計完備

### 實作層
- [ ] 所有個資欄位有標註（DTO、Schema、Entity）
- [ ] 敏感個資欄位有加密實作
- [ ] API 預設回傳遮蔽版本
- [ ] 前端未在 localStorage/sessionStorage 儲存個資
- [ ] 前端 console.log 無個資洩漏
- [ ] URL query string 無個資
- [ ] 密碼欄位使用 bcrypt/Argon2

### 稽核層
- [ ] 個資存取有稽核紀錄
- [ ] 稽核紀錄獨立儲存
- [ ] 稽核紀錄保存期限符合法遵要求

### 當事人權利
- [ ] 使用者可自行查閱/下載自己的資料
- [ ] 使用者可自行申請帳號刪除
- [ ] 刪除機制為實質刪除或匿名化

### 測試層
- [ ] E2E 測試資料為合成資料
- [ ] CI log 無真實個資

---

## 特殊議題：跨境傳輸

**個資法第 21 條**：中央目的事業主管機關認有限制必要時，得限制跨境傳輸。

若系統涉及跨境個資傳輸（如使用海外雲端服務、海外第三方 API）：
- SA/SD 藍圖必須標註跨境傳輸的資料類型與目的
- 告知義務告訴當事人跨境傳輸事實
- 評估接收地的個資保護水準是否相當
- 簽訂 DPA（Data Processing Agreement）

---

## 特殊議題：AI/LLM 處理個資

當 multi-agent 系統或 AI 功能處理個資時，需額外注意：

- 發送給 LLM 的 prompt 中不得含真實個資（先脫敏）
- LLM 輸出的內容必須經過 PII 偵測（見 LLM02 Sensitive Information Disclosure）
- 使用海外 LLM 服務（OpenAI、Anthropic API）涉及跨境傳輸議題
- LLM 供應商的資料保留政策必須告知當事人
- Fine-tuning 絕不使用真實個資
