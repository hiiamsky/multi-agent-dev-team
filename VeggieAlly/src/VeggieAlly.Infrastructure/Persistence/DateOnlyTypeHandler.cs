using System.Data;
using Dapper;

namespace VeggieAlly.Infrastructure.Persistence;

/// <summary>
/// Dapper TypeHandler for System.DateOnly
///
/// 解決 Dapper 預設不支援 DateOnly 型別的問題。
/// 將 DateOnly 轉換為 DbType.Date（DateTime with time=00:00:00），
/// 並在從 DB 讀取時正確解析回 DateOnly。
///
/// 在 DependencyInjection.cs 中呼叫 SqlMapper.AddTypeHandler 一次性全域註冊。
/// </summary>
internal sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value)
    {
        return value switch
        {
            DateOnly d    => d,
            DateTime dt   => DateOnly.FromDateTime(dt),
            string s      => DateOnly.Parse(s),
            _             => DateOnly.FromDateTime(Convert.ToDateTime(value))
        };
    }

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value  = value.ToDateTime(TimeOnly.MinValue);
    }
}
