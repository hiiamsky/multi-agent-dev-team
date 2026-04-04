using VeggieAlly.Domain.ValueObjects;
using VeggieAlly.Domain.Models.Draft;

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
    /// </summary>
    object BuildDraftBubble(DraftMenuSession session, string? liffBaseUrl);
}
