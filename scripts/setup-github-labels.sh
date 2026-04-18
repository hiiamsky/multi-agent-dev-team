#!/usr/bin/env bash
# GitHub Labels 建立腳本 — multi-agent 工作流
# Usage: ./scripts/setup-github-labels.sh
# Idempotent: 重複執行會覆寫既有 label 的顏色與描述
set -euo pipefail

echo "建立 Capability Labels..."
gh label create "cap:sa-sd"       --color "1D76DB" --description "Capability: 系統分析與架構設計" --force
gh label create "cap:dba"         --color "0E8A16" --description "Capability: 資料庫設計" --force
gh label create "cap:backend-pg"  --color "5319E7" --description "Capability: 後端開發" --force
gh label create "cap:frontend-pg" --color "FBCA04" --description "Capability: 前端開發" --force
gh label create "cap:qa-qc"       --color "B60205" --description "Capability: 品質驗證" --force
gh label create "cap:e2e-test"    --color "D93F0B" --description "Capability: E2E 自動化測試" --force
gh label create "cap:human"       --color "000000" --description "Capability: 需要人類或外部協作（嚴格使用）" --force

echo "建立 Status Labels..."
gh label create "status:ready"    --color "0E8A16" --description "Status: 可被認領" --force
gh label create "status:claimed"  --color "FBCA04" --description "Status: 已被認領" --force
gh label create "status:blocked"  --color "B60205" --description "Status: 被依賴或外部因素阻擋" --force
gh label create "status:review"   --color "5319E7" --description "Status: 等待 QA/QC 驗證" --force

echo "---所有 labels 建立完成---"
