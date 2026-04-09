using System.Text.Json.Serialization;

namespace VeggieAlly.Domain.ValueObjects;

/// <summary>
/// 驗證狀態列舉
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationStatus
{
    Ok,         // 準備發布區
    Anomaly,    // 異常待處理區
    Error       // 結構錯誤或無效資料
}