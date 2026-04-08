import { defineConfig } from '@playwright/test';

/**
 * VeggieAlly E2E Playwright 設定
 *
 * 測試對象：API 端點（不需要 browser，使用 request fixture）
 * 環境：docker-compose.test.yml 啟動的本地容器
 * 服務 URL：http://localhost:5000（veggie-ally 容器對外 port）
 */
export default defineConfig({
  testDir: './tests',
  /* 每個測試最長執行時間 */
  timeout: 30_000,
  /* 每個 expect() 最長等待時間 */
  expect: { timeout: 10_000 },
  /* 並行執行（API 測試無 browser 狀態衝突，但 DB 有共享狀態，改用循序） */
  workers: 1,
  /* 失敗時不重試（E2E 測試應明確失敗，不掩蓋問題） */
  retries: 0,
  /* 測試報告：保留 HTML report 與 list output */
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['json', { outputFile: 'playwright-report/results.json' }],
  ],
  /* Screenshot / Trace：測試失敗時自動擷取 */
  use: {
    baseURL: process.env['API_BASE_URL'] ?? 'http://localhost:5010',
    /* 失敗時自動截圖（API 測試無 browser，此設定對 APIRequestContext 無作用；
       若未來加入 browser 測試則自動生效） */
    screenshot: 'only-on-failure',
    /* Trace：失敗時保留 trace.zip */
    trace: 'retain-on-failure',
  },
});
