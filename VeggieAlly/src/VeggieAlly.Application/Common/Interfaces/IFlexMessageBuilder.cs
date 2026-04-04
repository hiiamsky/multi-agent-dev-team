using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Common.Interfaces;

public interface IFlexMessageBuilder
{
    /// <summary>
    /// 根據驗證後的品項清單組裝 LINE Flex Message BubbleContainer。
    /// 回傳可被 JSON 序列化的 Flex Message contents 物件。
    /// </summary>
    object BuildBubble(IReadOnlyList<ValidatedVegetableItem> items);
}
