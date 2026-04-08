/**
 * helpers/auth.ts
 *
 * LIFF Auth Mock 輔助工具
 *
 * 在 ASPNETCORE_ENVIRONMENT=Testing 環境下，LiffAuthFilter 接受
 * X-Test-Auth: true header 跳過真實 LINE token 驗證，直接注入
 * HttpContext.Items["TenantId"] 等欄位。
 *
 * 安全說明：
 *   - 此 mock 路徑嚴格限定在後端 Testing 環境，Production 完全無效。
 *   - 測試中的 tenant/user ID 皆為假資料，不包含任何真實使用者 PII。
 */

/** E2E 測試用的固定租戶 ID（對應 DB seed 資料） */
export const E2E_TENANT_ID = 'e2e-test-tenant';

/** E2E 測試用的租戶（無菜單，用於 404 邊界案例） */
export const E2E_NO_MENU_TENANT_ID = 'e2e-no-menu-tenant';

/** E2E 測試用的假 LINE User ID */
export const E2E_LINE_USER_ID = 'Ue2e000000000000000000000000test';

/** E2E 測試用的假顯示名稱 */
export const E2E_DISPLAY_NAME = 'E2E Test User';

/**
 * 回傳 Testing 環境 mock auth headers。
 *
 * @param tenantId  租戶 ID（預設 E2E_TENANT_ID）
 * @param userId    LINE User ID（預設 E2E_LINE_USER_ID）
 * @param displayName 顯示名稱（預設 E2E_DISPLAY_NAME）
 */
export function testAuthHeaders(
  tenantId: string = E2E_TENANT_ID,
  userId: string = E2E_LINE_USER_ID,
  displayName: string = E2E_DISPLAY_NAME,
): Record<string, string> {
  return {
    'X-Test-Auth': 'true',
    'X-Test-TenantId': tenantId,
    'X-Test-LineUserId': userId,
    'X-Test-DisplayName': displayName,
    'Content-Type': 'application/json',
  };
}
