using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;

namespace EternalHUD
{
    // ===== 悬停移出：清空当前物品实例（4.1 适配） =====

    [HarmonyPatch(typeof(ItemView), "OnPointerExit")]
    internal static class ItemView_PointOuterPatch
    {
        [HarmonyPrefix]
        private static void Prefix() => HoverState.CurrentItem = null;
    }

    [HarmonyPatch(typeof(GridItemView), "OnPointerExit")]
    internal static class GridItemView_PointOuterPatch
    {
        [HarmonyPrefix]
        private static void Prefix() => HoverState.CurrentItem = null;
    }

    // 4.1：TradingRequisitePanel 的 method_2 → CG_Awake1
    [HarmonyPatch(typeof(TradingRequisitePanel), "CG_Awake1")]
    internal static class TradingRequisitePanel_HoverOutPatch
    {
        [HarmonyPrefix]
        private static void Prefix() => HoverState.CurrentItem = null;
    }

    // 4.1：手册 EntityIcon 悬停移出（旧版 method_2 → CG_Awake1）
    [HarmonyPatch(typeof(EFT.HandBook.EntityIcon), "CG_Awake1")]
    internal static class EntityIcon_HoverOutPatch
    {
        [HarmonyPrefix]
        private static void Prefix() => HoverState.CurrentItem = null;
    }
}
