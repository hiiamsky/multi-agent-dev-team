using VeggieAlly.Domain.ValueObjects;
using VeggieAlly.Domain.Models.Draft;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Common.Interfaces;

public interface IFlexMessageBuilder
{
    /// <summary>
    /// 根據驗證後的品項清單組裝 LINE Flex Message BubbleContainer。
    /// 回傳可被 JSON 序列化的 Flex Message contents 物件。
    /// </summary>
    object BuildBubble(IReadOnlyList<ValidatedVegetableItem> items);

    /// <summary>
    /// 根據草稿 Session 組裝 LINE Flex Message BubbleContainer（含 LIFF 修正按鈕）。
    /// 異常品項會包含修正按鈕，正常品項無按鈕。
    /// 全部品項 Status == Ok 時，Footer 顯示 🚀 一鍵發布 Postback 按鈕。
    /// </summary>
    object BuildDraftBubble(DraftMenuSession session, string? liffBaseUrl);

    /// <summary>
    /// 根據已發布菜單組裝 LINE Flex Message BubbleContainer（發布確認卡片）。
    /// 顯示 ✅ 菜單已發布確認訊息。
    /// </summary>
    object BuildPublishedBubble(PublishedMenu menu);
}
