using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using HarmonyLib;

namespace EternalHUD
{
    /// <summary>
    /// 4.1 适配：原 GClass3515.method_6 → ActiveSearchContentOperation.SearchContent（勿 patch ExecuteAsync，
    /// 否则会替换整个搜索总流程并跳过 Open，导致无法搜索容器）。
    /// 按物品价值等级改变单次发现的搜索时长。
    /// </summary>
    [HarmonyPatch(typeof(ActiveSearchContentOperation), "SearchContent")]
    public static class SearchTimePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref Task __result, ActiveSearchContentOperation __instance)
        {
            if (!MyMod.ChangeSearchTime.Value)
            {
                return true;
            }

            // 无未知物品直接完成（对齐旧版 ContainsUnknownItems 判断）
            if (!__instance._searchController.ContainsUnknownItems(__instance.Item))
            {
                __result = Task.CompletedTask;
                return false;
            }

            __result = ExecuteSearchLogicAsync(__instance);
            return false;
        }

        private static async Task ExecuteSearchLogicAsync(ActiveSearchContentOperation op)
        {
            var skillsInfo = op._profile.SkillsInfo;
            float speed = 1f + skillsInfo.AttentionLootSpeedValue + skillsInfo.SearchBuffSpeedValue;

            while (op.TryFindUnknownItem(out var unknownItem))
            {
                var itemRef = unknownItem != null && MyMod.ItemDict.TryGetValue(unknownItem.TemplateId, out var r) ? r : null;
                float time = 1f;
                if (itemRef != null && itemRef.ChangeSearchTime)
                {
                    time = itemRef.Level switch
                    {
                        >= 6 => 3f,
                        >= 5 => 2.5f,
                        >= 4 => 1.5f,
                        >= 2 => 1.5f,
                        _ => 1f
                    };
                }
                time *= MyMod.SearchTimeMutiper.Value;

                await Task.Delay((int)(time / speed * 1000f), op._cancellationToken.Token);
                if (op.Terminated) return;

                if (!op.TryFindUnknownItem(out var next)) break;
                op.DiscoverItem(next);
            }

            op._searchController.OnItemFullySearched();
        }
    }
}
