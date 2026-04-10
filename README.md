# multi-agent-dev-team

> 以多智能體團隊驅動企業級軟體開發的實驗性框架

本 Repo 示範如何將 AI Agent 組成**分工明確的開發團隊**，從需求淨化到 PR 交付全程自動協作，並以 [VeggieAlly（菜商神隊友）](#專案清單) 作為真實產品驗證場景。

---

## Agent 團隊

| Agent | 職責 | 能寫碼 |
|-------|------|--------|
| **Orchestrator** | 需求淨化、任務路由、狀態掌控、PR 協調 | ✗ |
| **SA/SD** | 需求解構、架構設計、BDD User Stories、規格藍圖 | ✗ |
| **後端 PG** | Controller、CQRS Handler、Domain、Dapper | ✓ |
| **前端 PG** | Vue 3 元件、路由、API Client | ✓ |
| **DBA** | Schema、Migration、索引策略 | ✓ |
| **QA/QC** | API 整合驗證、安全審查、批判迴圈 | ✗ |
| **E2E 測試** | Playwright 端到端核心流程驗證 | ✓ |

## 工作流程

```
人類需求
  └─► Orchestrator（需求淨化 + Issue 建立）
        └─► SA/SD（BDD Stories + 架構藍圖）
              ├─► 前端 PG  ┐
              ├─► 後端 PG  ├─ 並行施工（git worktree）+ 跨域檢視
              └─► DBA      ┘
                    └─► QA/QC（整合驗證 + 安全審查）
                          └─► E2E 測試
                                └─► Orchestrator（PR 建立）
                                      └─► 人類批准 merge
```

詳細協作規範、路由規則、退回機制請參閱 [`.github/AGENTS.md`](.github/AGENTS.md)。

---

## 專案清單

| 專案 | 說明 | 技術棧 |
|------|------|--------|
| [VeggieAlly](./VeggieAlly/) | 蔬菜批發商 LINE 智能助理，含 AI 菜單建立、庫存管理、LIFF 銷售端 | .NET 10 / PostgreSQL / Redis / Vue 3 |

---

## Repo 結構

```
mulAgent/
├── VeggieAlly/          # 產品專案（含完整 src、tests、db、liff-app）
├── docs/
│   ├── specs/           # SA/SD 規格藍圖
│   └── reviews/         # QA/QC 驗證報告
└── .github/
    ├── AGENTS.md        # Agent 協作規範（完整版）
    └── agents/          # Agent 定義
```

---

## 授權

MIT
