#!/usr/bin/env bash
# GitHub Labels 建立腳本 — multi-agent 工作流
# Usage: ./scripts/setup-github-labels.sh
# Idempotent: 重複執行會覆寫既有 label 的顏色與描述
set -euo pipefail

if ! command -v gh >/dev/null 2>&1; then
  echo "錯誤：找不到 gh CLI，請先安裝 GitHub CLI。" >&2
  exit 1
fi

if ! gh auth status >/dev/null 2>&1; then
  echo "錯誤：gh 尚未登入，請先執行 gh auth login。" >&2
  exit 1
fi

if [[ -n "${GH_REPO:-}" ]]; then
  REPO="${GH_REPO}"
else
  if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "錯誤：目前不在 git repo 內，請切換到目標 repo 或設定 GH_REPO=owner/name。" >&2
    exit 1
  fi

  if ! REPO="$(gh repo view --json nameWithOwner -q .nameWithOwner)"; then
    echo "錯誤：gh 無法讀取目前 repo，請確認你有存取權限或設定 GH_REPO=owner/name。" >&2
    exit 1
  fi
fi

echo "目標 repo: ${REPO}"
echo "建立 Capability Labels..."
gh label create "cap:sa-sd"       --repo "${REPO}" --color "1D76DB" --description "Capability: 系統分析與架構設計" --force
gh label create "cap:dba"         --repo "${REPO}" --color "0E8A16" --description "Capability: 資料庫設計" --force
gh label create "cap:backend-pg"  --repo "${REPO}" --color "5319E7" --description "Capability: 後端開發" --force
gh label create "cap:frontend-pg" --repo "${REPO}" --color "FBCA04" --description "Capability: 前端開發" --force
gh label create "cap:qa-qc"       --repo "${REPO}" --color "B60205" --description "Capability: 品質驗證" --force
gh label create "cap:e2e-test"    --repo "${REPO}" --color "D93F0B" --description "Capability: E2E 自動化測試" --force
gh label create "cap:human"       --repo "${REPO}" --color "000000" --description "Capability: 需要人類或外部協作（嚴格使用）" --force

echo "建立 Status Labels..."
gh label create "status:ready"    --repo "${REPO}" --color "0E8A16" --description "Status: 可被認領" --force
gh label create "status:claimed"  --repo "${REPO}" --color "FBCA04" --description "Status: 已被認領" --force
gh label create "status:blocked"  --repo "${REPO}" --color "B60205" --description "Status: 被依賴或外部因素阻擋" --force
gh label create "status:review"   --repo "${REPO}" --color "5319E7" --description "Status: 等待 QA/QC 驗證" --force

echo "---所有 labels 建立完成---"
