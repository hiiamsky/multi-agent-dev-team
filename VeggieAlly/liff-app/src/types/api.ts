// === Draft 修正 ===
export interface CorrectPriceRequest {
  buy_price?: number
  sell_price?: number
}

export interface CorrectPriceResponse {
  id: string
  name: string
  is_new: boolean
  buy_price: number
  sell_price: number
  quantity: number
  unit: string
  historical_avg_price: number | null
  validation: {
    status: 'passed' | 'failed' | 'warning'
    message: string | null
  }
}

// === 今日菜單 ===
export interface TodayMenuResponse {
  id: string
  tenant_id: string
  date: string
  published_at: string
  items: MenuItemResponse[]
}

export interface MenuItemResponse {
  id: string
  name: string
  is_new: boolean
  sell_price: number
  original_qty: number
  remaining_qty: number
  unit: string
}

// === 庫存扣除 ===
export interface DeductInventoryRequest {
  item_id: string
  amount: number
}

export interface DeductInventoryResponse {
  id: string
  name: string
  remaining_qty: number
  unit: string
}

// === 通用錯誤 ===
export interface ApiError {
  error: {
    code: string
    message: string
  }
}

// === 前端專用 ===
export interface OrderItem {
  id: string
  name: string
  is_new?: boolean
  sell_price: number
  original_qty?: number
  remaining_qty: number
  unit: string
  orderQty: number
}