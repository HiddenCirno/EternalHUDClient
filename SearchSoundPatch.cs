using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using EFT.UI;
using HarmonyLib;

namespace EternalHUD
{
    /// <summary>
    /// 4.1 适配：原 GClass3517.PlayDiscoverSound → SinglePlayerSearchContentOperation.PlayDiscoverSound。
    /// 按物品价值等级播放不同的发现音效。
    /// </summary>
    [HarmonyPatch(typeof(SinglePlayerSearchContentOperation), "PlayDiscoverSound")]
    public static class SearchSoundPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Item item)
        {
            if (!MyMod.ChangeSearchSound.Value)
            {
                return true;
            }

            PlaySound(item);
            return false;
        }

        public static EUISoundType GetSoundType(int level)
        {
            return level switch
            {
                >= 6 => EUISoundType.AchievementCompleted,
                >= 5 => EUISoundType.InsuranceInsured,
                >= 4 => EUISoundType.MenuInspectorWindowClose,
                >= 2 => EUISoundType.ButtonClick,
                _ => EUISoundType.ButtonOver
            };
        }

        public static void PlaySound(Item item)
        {
            if (item == null) return;
            var itemRef = MyMod.ItemDict.TryGetValue(item.TemplateId, out var r) ? r : null;
            if (itemRef != null && itemRef.ChangeSearchTime)
            {
                Singleton<GUISounds>.Instance.PlayUISound(GetSoundType(itemRef.Level));
            }
            else
            {
                Singleton<GUISounds>.Instance.PlayItemSound(item.ItemSound, EInventorySoundType.drop);
            }
        }
    }
}
