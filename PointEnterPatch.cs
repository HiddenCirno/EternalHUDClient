using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;

namespace EternalHUD
{
    // ===== 悬停进入：记录当前物品实例（4.1 适配） =====

    [HarmonyPatch(typeof(ItemView), "OnPointerEnter")]
    internal static class ItemView_PointEnterPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ItemView __instance)
        {
            if (__instance.Item != null) HoverState.CurrentItem = __instance.Item;
        }
    }

    [HarmonyPatch(typeof(GridItemView), "OnPointerEnter")]
    internal static class GridItemView_PointEnterPatch
    {
        [HarmonyPrefix]
        private static void Prefix(GridItemView __instance)
        {
            if (__instance.Item != null) HoverState.CurrentItem = __instance.Item;
        }
    }

    [HarmonyPatch(typeof(HideoutItemView), "OnPointerEnter")]
    internal static class HideoutItemView_PointEnterPatch
    {
        [HarmonyPrefix]
        private static void Prefix(HideoutItemView __instance)
        {
            if (__instance.Item != null) HoverState.CurrentItem = __instance.Item;
        }
    }

    // 4.1：TradingRequisitePanel 的 method_1 → CG_Awake，字段 itemContextAbstractClass → _itemContext
    [HarmonyPatch(typeof(TradingRequisitePanel), "CG_Awake")]
    internal static class TradingRequisitePanel_HoverPatch
    {
        [HarmonyPrefix]
        private static void Prefix(TradingRequisitePanel __instance)
        {
            var ctx = Traverse.Create(__instance).Field("_itemContext").GetValue();
            var item = ctx != null ? Traverse.Create(ctx).Property("Item").GetValue<Item>() : null;
            if (item != null) HoverState.CurrentItem = item;
        }
    }

    // 4.1：手册 EntityIcon 悬停进入（旧版 method_1 → CG_Awake，字段 item_0 → _item）
    [HarmonyPatch(typeof(EFT.HandBook.EntityIcon), "CG_Awake")]
    internal static class EntityIcon_HoverPatch
    {
        [HarmonyPrefix]
        private static void Prefix(EFT.HandBook.EntityIcon __instance)
        {
            var item = Traverse.Create(__instance).Field("_item").GetValue<Item>();
            if (item != null) HoverState.CurrentItem = item;
        }
    }
}
