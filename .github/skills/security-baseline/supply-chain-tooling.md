# 軟體供應鏈工具鏈規範

**對應規則**：OWASP Web Top 10:2025 A03 Software Supply Chain Failures、OWASP LLM Top 10:2025 LLM03 Supply Chain

**權威來源**：NIST SSDF SP 800-218、OWASP Dependency-Check、SLSA framework

**適用對象**：DevOps、後端 PG、前端 PG、DBA（Migration 腳本依賴）

---

## 為何需要具體工具鏈

OWASP A03:2025 將供應鏈風險從「使用過期套件」擴展到整個建置/散佈/更新生命週期。單講「要掃描依賴」不夠，必須落實到具體工具、具體門檻、具體回應流程。

---

## 第一層：依賴版本鎖定（Lockfile）

**規範：所有語言的依賴必須使用 lockfile，且 lockfile 納入版控**。

| 語言/平台 | Lockfile 檔名 | 啟用方式 |
|----------|-------------|---------|
| .NET | `packages.lock.json` | `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in `.csproj` |
| Node.js (npm) | `package-lock.json` | 預設啟用，禁止使用 `npm install --no-save` |
| Node.js (yarn) | `yarn.lock` | 預設啟用 |
| Node.js (pnpm) | `pnpm-lock.yaml` | 預設啟用 |
| Python | `requirements.txt` with `--hash` 或 `poetry.lock` / `Pipfile.lock` | 產出 hash-pinned requirements |
| Go | `go.sum` | 預設啟用 |
| Rust | `Cargo.lock` | 預設啟用 |

**CI 驗證**：
- 執行 `dotnet restore --locked-mode` / `npm ci` 而非 `npm install`
- 禁止 `npm audit fix --force`（可能升級到 breaking 版本而未經審查）

---

## 第二層：SCA（Software Composition Analysis）

**規範：CI/CD pipeline 必須執行 SCA 掃描，發現 High/Critical 漏洞必須阻擋合併**。

### 建議工具選項

| 工具 | 適用語言 | 特性 |
|------|---------|------|
| **GitHub Dependabot** | 多語言 | 免費、PR 自動產生、alert 整合 GitHub Security |
| **Snyk** | 多語言 | 商業，CVE 資料庫豐富、有 fix PR 功能 |
| **OWASP Dependency-Check** | 多語言 | 開源、命令列工具、整合 Jenkins/GitHub Actions |
| **Trivy** | 容器 + 多語言 | 開源，掃描速度快，支援 IaC |
| **npm audit** / **yarn audit** | Node.js | 官方內建，基本掃描 |
| **dotnet list package --vulnerable** | .NET | 官方 CLI，整合容易 |

### 建議的阻擋門檻

| 嚴重度 | 處理方式 |
|-------|---------|
| Critical | 🚫 阻擋 PR 合併，立即升級或換套件 |
| High | 🚫 阻擋 PR 合併，7 天內修正 |
| Medium | ⚠️ PR 描述標註，30 天內修正 |
| Low | 📝 記錄於 Issue，可接受的技術債 |

### GitHub Actions 範例

```yaml
name: SCA Scan
on: [push, pull_request]
jobs:
  sca:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run Trivy
        uses: aquasecurity/trivy-action@master
        with:
          scan-type: 'fs'
          severity: 'CRITICAL,HIGH'
          exit-code: '1'  # 發現 Critical/High 時失敗
```

---

## 第三層：SBOM（Software Bill of Materials）

**規範：每次 release 必須產出 SBOM，格式為 SPDX 或 CycloneDX**。

### 建議工具

| 工具 | 輸出格式 | 備註 |
|------|---------|------|
| **Syft** | SPDX, CycloneDX | 多語言通吃，與 Trivy 同生態 |
| **CycloneDX CLI** | CycloneDX | OWASP 官方工具 |
| **GitHub SBOM API** | SPDX | Repo 自動產生，免工具 |
| **dotnet CycloneDX** | CycloneDX | .NET 專用 |

### SBOM 使用情境

- 漏洞爆發時（如 Log4Shell）可快速定位受影響系統
- 稽核時證明依賴透明度
- 第三方合作時提供給客戶審查

### 產出與儲存規範

- 每次 release 產出 SBOM 並附加於 GitHub Release artifacts
- SBOM 格式建議 SPDX 2.3 以上或 CycloneDX 1.5 以上
- 內部儲存至少保留 2 年，便於事後稽核

---

## 第四層：Typo-Squatting 與惡意包防護

**威脅情境**：攻擊者發布近似名稱的惡意包（如 `reqeusts` vs `requests`、`cross-env` vs `cross-env.js`），開發者拼錯即中招。

**規範**：
- 新增依賴前必須人類審查：
  - 套件名稱拼寫
  - 作者/維護者是否為已知可信來源
  - 下載量、star 數、最後更新日期
  - 是否在官方 registry（非 fork 或 mirror）
- 禁止使用 GitHub 直接依賴未審查的分支：
  ```json
  // ❌ 禁止
  "somepackage": "github:random-user/somepackage#main"

  // ✅ 允許（已審查後鎖定）
  "somepackage": "github:trusted-org/somepackage#v1.2.3"
  ```
- 企業環境建議使用私有 registry（Artifactory、Nexus、GitHub Packages）代理官方 registry，可設定白名單

---

## 第五層：CI/CD Pipeline 安全

**規範：CI/CD pipeline 本身視為攻擊面**。

- **GitHub Actions 安全**：
  - 使用 commit SHA 而非 tag：`uses: actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11`（而非 `@v4`）
  - 設定 `GITHUB_TOKEN` 的最小權限：
    ```yaml
    permissions:
      contents: read
      pull-requests: write
    ```
  - 禁用 `pull_request_target` 於不信任 fork
- **Secret 管理**：
  - 使用 GitHub Secrets、Azure Key Vault、AWS Secrets Manager
  - 禁止 commit `.env`、`appsettings.Development.json` 含 secret
  - 加入 pre-commit hook 掃描 secret（如 `git-secrets`、`truffleHog`）
- **Build 環境隔離**：
  - 每次 build 使用乾淨容器
  - 禁止持久化的 build agent 處理敏感專案

---

## 第六層：Container 供應鏈（若使用容器）

**規範**：
- Base image 來源：官方映像（`mcr.microsoft.com/dotnet/*`、`node:lts-alpine`），禁止來路不明的 Docker Hub 映像
- Base image 版本鎖定到 digest：
  ```dockerfile
  FROM node@sha256:abcdef...  # 非 FROM node:lts
  ```
- 容器映像掃描（Trivy、Clair）作為 build 階段
- Distroless 或 minimal base image 減少攻擊面
- 不在容器中以 root 執行應用程式

---

## 第七層：Dependency Update 策略

**規範：主動更新，不被動受害**。

- **每月**執行一次 `dotnet outdated` / `npm outdated` 檢查
- **Security patch 立即更新**（Dependabot alert）
- **Major version 升級**必須經 SA/SD 評估是否影響架構
- **凍結依賴**的理由必須記錄於 ADR（為何不升級、何時重新評估）

---

## QA/QC 驗證檢查清單

- [ ] Lockfile 存在且納入版控
- [ ] CI pipeline 有 SCA 掃描，且 Critical/High 會阻擋
- [ ] SBOM 有產出並可取得
- [ ] `.gitignore` 排除 secret 檔案
- [ ] CI 使用的 GitHub Actions 版本已鎖定 SHA
- [ ] 容器 base image 使用官方來源且版本鎖定
- [ ] 最近 30 天內有執行依賴掃描的紀錄
