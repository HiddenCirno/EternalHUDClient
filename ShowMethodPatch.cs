using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EternalHUD
{
    /// <summary>
    /// 完全重写 SimpleTooltip.Show：数据驱动 + 本地化 + Item 树价格 + 本地仓库数量。
    /// 文本结构与旧版一致：名称→双语→ID→品质→弹药→护甲→任务→藏身处→交易→制作→奖励→已拥有→跳蚤→价格→复制提示。
    /// 折叠机制依赖的 _xxxText 片段仍在此填充，MyMod.Update 的折叠刷新逻辑不变。
    /// </summary>
    public static class ShowMethodPatch
    {
        private static readonly string[] LevelTags = { "[废品]", "[普通]", "[精良]", "[稀有]", "[史诗]", "[传奇]", "[神话]", "[超越]" };

        // 旧版货币判定列表
        private static readonly string[] MoneyIds =
        {
            "5449016a4bdc2d6f028b456f", // 卢布
            "5696686a4bdc2da3298b456a", // 美元
            "569668774bdc2da2298b4568", // 欧元
            "5d235b4d86f7742e017bc88a", // GP币
            "6656560053eaaa7a23349c86"  // Lega徽章
        };

        [HarmonyPatch(typeof(SimpleTooltip), "Show")]
        public class SimpleTooltip_Show_Patch
        {
            public static bool Prefix(SimpleTooltip __instance, string text, ref CancellationToken __result, Vector2? offset = null, float delay = 0f, float? maxWidth = null)
            {
                MyMod._cacheText = text;
                MyMod._cacheTooltip = __instance;
                var label = Traverse.Create(__instance).Field("_label").GetValue<TextMeshProUGUI>();
                MyMod._cache_label = label;

                var item = HoverState.CurrentItem;
                if (item == null || !MyMod.ItemDict.TryGetValue(item.TemplateId, out var itemRef))
                {
                    // 服务端未生成该物品索引：放行原始 SimpleTooltip.Show，显示游戏原始 tooltip
                    MyMod._cacheText = text;
                    return true;
                }

                string result = BuildTooltip(item, itemRef, text);
                __instance.SetText(result);
                MyMod._cacheText = result;
                MyMod._itemID = itemRef.Id;
                MyMod._itemName = text;

                Vector2 actualOffset = offset ?? (Vector2)AccessTools.Field(typeof(SimpleTooltip), "_defaultOffset").GetValue(__instance);
                bool inRaid = Singleton<AbstractGame>.Instance?.InRaid ?? false;
                __result = __instance.Show(actualOffset, inRaid ? MyMod.TooltipDelay.Value : delay);
                if (label != null) label.color = new Color(label.color.r, label.color.g, label.color.b, 1f);
                return false;
            }

            private static string BuildTooltip(Item item, ItemRef itemRef, string originalText)
            {
                string C(Color c) => ToColorPatch.ColorToHex(c);
                var sb = new StringBuilder();

                // 名称
                sb.Append($"<color={C(MyMod.CommonColor.Value)}>名称: </color>{originalText}\n");

                // 双语（英文名，客户端本地化）
                if (MyMod.ShowEName.Value)
                {
                    string enName = LocalizationManager.Instance.LocalizedValue($"{itemRef.Id} Name", "en");
                    if (!string.IsNullOrEmpty(enName) && enName != $"{itemRef.Id} Name")
                    {
                        sb.Append($"<color={C(MyMod.CommonColor.Value)}>双语: {enName}</color>\n");
                    }
                }

                // ID
                if (MyMod.ShowID.Value)
                {
                    sb.Append($"<color={C(MyMod.CommonColor.Value)}>ID: {itemRef.Id}</color>\n");
                }

                // 品质（单独一行，颜色来自 TagColorX 配置）
                if (MyMod.ShowTag.Value && itemRef.Level >= 0 && itemRef.Level < LevelTags.Length)
                {
                    sb.Append($"<color={C(MyMod.CommonColor.Value)}>品质: </color><color={C(GetTagColor(itemRef.Level))}>{LevelTags[itemRef.Level]}</color>\n");
                }

                // 弹药
                MyMod._ammoText = string.Empty;
                if (MyMod.ShowAmmoData.Value && itemRef.Ammo != null)
                {
                    var ammoColor = AmmoColorHex(itemRef.Ammo.Pent);
                    MyMod._ammoText =
                        $"<color={C(MyMod.CommonColor.Value)}>伤害: </color><color={C(MyMod.RedsColor.Value)}>{itemRef.Ammo.Damage}</color>\n" +
                        $"<color={C(MyMod.CommonColor.Value)}>穿透力度: </color><color={ammoColor}>{itemRef.Ammo.Pent}</color>\n" +
                        $"<color={C(MyMod.CommonColor.Value)}>弹丸数量: {itemRef.Ammo.BulletCount}</color>\n";
                    sb.Append(MyMod._ammoText);
                }

                // 护甲
                MyMod._armorText = string.Empty;
                if (MyMod.ShowArmorData.Value && itemRef.Armor != null)
                {
                    var armorColor = ArmorColorHex(itemRef.Armor.Level);
                    MyMod._armorText =
                        $"<color={C(MyMod.CommonColor.Value)}>材质: {itemRef.Armor.Material}</color>\n" +
                        $"<color={C(MyMod.CommonColor.Value)}>重量: {itemRef.Armor.Weight}</color>\n" +
                        $"<color={C(MyMod.CommonColor.Value)}>钝伤指数: </color><color={C(MyMod.RedsColor.Value)}>{itemRef.Armor.Blunt}</color>\n" +
                        $"<color={C(MyMod.CommonColor.Value)}>防护等级: </color><color={armorColor}>{itemRef.Armor.Level}</color>\n" +
                        $"<color={C(MyMod.CommonColor.Value)}>最大耐久: </color><color={C(MyMod.GreenColor.Value)}>{itemRef.Armor.MaxDurability}</color>\n";
                    sb.Append(MyMod._armorText);
                }

                // 任务：上交(FIR) → 交付 → 安放
                MyMod._questHandoverText = string.Empty;
                MyMod._questText = string.Empty;
                MyMod._questLeaveText = string.Empty;
                if (MyMod.ShowQuestData.Value && itemRef.QuestReqs.Count > 0)
                {
                    var handoverFir = itemRef.QuestReqs.Where(q => q.Type == "HandoverItem" && q.FindInRaid).ToList();
                    var handover = itemRef.QuestReqs.Where(q => q.Type == "HandoverItem" && !q.FindInRaid).ToList();
                    var leave = itemRef.QuestReqs.Where(q => q.Type == "LeaveItemAtLocation").ToList();
                    if (handoverFir.Count > 0)
                        MyMod._questHandoverText = BuildQuestLine(handoverFir, "上交物品需求", C(MyMod.HandOverRaidColor.Value));
                    if (handover.Count > 0)
                        MyMod._questText = BuildQuestLine(handover, "交付物品需求", C(MyMod.HandOverColor.Value));
                    if (leave.Count > 0)
                        MyMod._questLeaveText = BuildQuestLine(leave, "安放物品需求", C(MyMod.LeaveColor.Value));
                    sb.Append(MyMod._questHandoverText);
                    sb.Append(MyMod._questText);
                    sb.Append(MyMod._questLeaveText);
                }

                // 藏身处
                MyMod._hideoutText = string.Empty;
                if (MyMod.ShowHideoutData.Value && itemRef.AreaReqs.Count > 0)
                {
                    MyMod._hideoutText = BuildAreaLine(itemRef.AreaReqs, C(MyMod.HideoutColor.Value));
                    sb.Append(MyMod._hideoutText);
                }

                // 交易来源
                MyMod._tradeText = string.Empty;
                if (MyMod.ShowTradeData.Value && itemRef.Trades.Count > 0)
                {
                    MyMod._tradeText = BuildTradeLine(itemRef.Trades, C(MyMod.TradeColor.Value));
                    sb.Append(MyMod._tradeText);
                }

                // 交易用途
                MyMod._tradeUseText = string.Empty;
                if (MyMod.ShowTradeUseData.Value && itemRef.TradeUses.Count > 0)
                {
                    MyMod._tradeUseText = BuildTradeUseLine(itemRef.TradeUses, C(MyMod.TradeUseColor.Value));
                    sb.Append(MyMod._tradeUseText);
                }

                // 制作配方
                MyMod._productText = string.Empty;
                if (MyMod.ShowProductData.Value && itemRef.Recipes.Count > 0)
                {
                    MyMod._productText = BuildRecipeLine(itemRef.Recipes, C(MyMod.ProductColor.Value));
                    sb.Append(MyMod._productText);
                }

                // 制作用途
                MyMod._productUseText = string.Empty;
                if (MyMod.ShowProductUseData.Value && itemRef.RecipeUses.Count > 0)
                {
                    MyMod._productUseText = BuildRecipeUseLine(itemRef.RecipeUses, C(MyMod.ProductUseColor.Value));
                    sb.Append(MyMod._productUseText);
                }

                // 获取途径（任务奖励）
                MyMod._rewardText = string.Empty;
                if (MyMod.ShowReward.Value && itemRef.RewardFrom.Count > 0)
                {
                    MyMod._rewardText = BuildRewardLine(itemRef.RewardFrom, C(MyMod.RewardColor.Value));
                    sb.Append(MyMod._rewardText);
                }

                // 已拥有（优先 StashController 客户端全量 Profile；无则用战局开始拉取的服务端快照；都没有则不显示）
                if (MyMod.ShowStashCount.Value)
                {
                    var (hasSource, count, fir) = GetStashCount(itemRef.Id);
                    if (hasSource)
                    {
                        string firStr = fir > 0 ? $"<color={C(MyMod.GreenColor.Value)}>{fir}</color>" : $"<color={C(MyMod.RedsColor.Value)}>0</color>";
                        sb.Append($"<color={C(MyMod.CommonColor.Value)}>已拥有: </color>{firStr}<color={C(MyMod.CommonColor.Value)}>/{count}</color>\n");
                    }
                }

                // 跳蚤市场
                if (MyMod.ShowRagfairData.Value)
                {
                    sb.Append(itemRef.CanSell
                        ? $"<color={C(MyMod.CommonColor.Value)}>跳蚤市场: </color><color={C(MyMod.GreenColor.Value)}>可交易</color>\n"
                        : $"<color={C(MyMod.CommonColor.Value)}>跳蚤市场: </color><color={C(MyMod.RedsColor.Value)}>不可交易</color>\n");
                }

                // 参考价格
                MyMod._priceText = string.Empty;
                if (MyMod.ShowPrice.Value)
                {
                    int value = GetItemValue(item, itemRef);
                    if (value > 0)
                        MyMod._priceText = $"<color={C(MyMod.CommonColor.Value)}>参考价格: </color><color={C(MyMod.PriceColor.Value)}>{value}</color>\n";
                    sb.Append(MyMod._priceText);
                }

                sb.Append("<i>按下Ctrl+Alt+C复制物品ID</i>\n");
                return sb.ToString();
            }

            // ===== 品质标签颜色 =====
            private static Color GetTagColor(int level)
            {
                return level switch
                {
                    0 => MyMod.TagColor0.Value,
                    1 => MyMod.TagColor1.Value,
                    2 => MyMod.TagColor2.Value,
                    3 => MyMod.TagColor3.Value,
                    4 => MyMod.TagColor4.Value,
                    5 => MyMod.TagColor5.Value,
                    6 => MyMod.TagColor6.Value,
                    _ => MyMod.TagColor7.Value
                };
            }

            // ===== 价格：可交易→市场价；不可交易→Item 树累加 =====
            private static int GetItemValue(Item item, ItemRef itemRef)
            {
                if (itemRef.CanSell)
                    return itemRef.MarketPrice > 0 ? itemRef.MarketPrice : (itemRef.HandbookPrice > 0 ? itemRef.HandbookPrice : 1);
                int total = 0;
                foreach (var sub in item.GetAllItems())
                {
                    if (!MyMod.ItemDict.TryGetValue(sub.TemplateId, out var subRef)) continue;
                    int p = subRef.MarketPrice > 0 ? subRef.MarketPrice : (subRef.HandbookPrice > 0 ? subRef.HandbookPrice : 1);
                    total += p;// * (sub.StackObjectsCount > 1 ? sub.StackObjectsCount : 1);
                }
                return total;
            }

            // ===== 仓库数量：先检查 StashController，再检查 Stash 是否有效 =====
            // StashController 存在且 Stash 有效（主菜单）→ 用客户端全量 Profile
            // StashController 存在但 Stash 为 null（战局内被清）→ 走服务端快照
            // StashController 为 null → 走服务端快照
            // 快照有数据源但物品不在 → 显示 0/0（与 StashController 分支语义一致）
            // 完全无数据源 → 不显示
            private static (bool hasSource, int count, int fir) GetStashCount(string templateId)
            {
                // ① StashController 存在且其 Stash 有效 → 客户端全量 Profile
                var inv = MyMod.StashController?.Profile?.InventoryInfo;
                if (inv != null && inv.Stash != null)
                {
                    int count = 0, fir = 0;
                    foreach (var i in inv.GetPlayerItems(EPlayerItems.All))
                    {
                        if (i.TemplateId != templateId) continue;
                        count += i.StackObjectsCount;
                        if (i.SpawnedInSession) fir += i.StackObjectsCount;
                    }
                    return (true, count, fir);
                }

                // ② Stash 为 null（战局内）或 StashController 为 null → 服务端快照
                if (MyMod.StashSnapshot.Count > 0)
                {
                    if (MyMod.StashSnapshot.TryGetValue(templateId, out var snap))
                        return (true, snap.count, snap.fir);
                    return (true, 0, 0);   // 快照有数据但该物品不在 → 0/0
                }

                // ③ 快照也为空 → 不显示
                return (false, 0, 0);
            }

            private static string AmmoColorHex(int pent)
            {
                if (pent >= 60) return ToColorPatch.ColorToHex(MyMod.AmmoColor6.Value);
                if (pent >= 50) return ToColorPatch.ColorToHex(MyMod.AmmoColor5.Value);
                if (pent >= 40) return ToColorPatch.ColorToHex(MyMod.AmmoColor4.Value);
                if (pent >= 30) return ToColorPatch.ColorToHex(MyMod.AmmoColor3.Value);
                if (pent >= 20) return ToColorPatch.ColorToHex(MyMod.AmmoColor2.Value);
                return ToColorPatch.ColorToHex(MyMod.AmmoColor1.Value);
            }

            private static string ArmorColorHex(int level)
            {
                if (level >= 6) return ToColorPatch.ColorToHex(MyMod.AmmoColor6.Value);
                if (level >= 5) return ToColorPatch.ColorToHex(MyMod.AmmoColor5.Value);
                if (level >= 4) return ToColorPatch.ColorToHex(MyMod.AmmoColor4.Value);
                if (level >= 3) return ToColorPatch.ColorToHex(MyMod.AmmoColor3.Value);
                if (level >= 2) return ToColorPatch.ColorToHex(MyMod.AmmoColor2.Value);
                return ToColorPatch.ColorToHex(MyMod.AmmoColor1.Value);
            }

            // ===== 本地化 =====
            private static string LocalizeItemName(string templateId)
            {
                var key = $"{templateId} Name";
                var localized = key.Localized();
                return localized == key ? templateId : localized;
            }

            private static string LocalizeQuestName(string questId)
                => ($"{questId} name").Localized();

            private static string LocalizeTraderName(string traderId)
                => ($"{traderId} Nickname").Localized();

            private static string LocalizeAreaName(int areaType)
                => ($"hideout_area_{areaType}_name").Localized();

            // ===== 任务行（上交/交付/安放） =====
            private static string BuildQuestLine(List<QuestRequirement> list, string title, string color)
            {
                var sb = new StringBuilder();
                sb.Append($"<color={ToColorPatch.ColorToHex(MyMod.CommonColor.Value)}>{title}: \n</color>");
                sb.Append($"<color={color}><b>");
                foreach (var q in list)
                {
                    sb.Append($"{LocalizeQuestName(q.QuestId)}({LocalizeTraderName(q.TraderId)})需求x{q.Count}\n");
                }
                sb.Append("</b></color>");
                return sb.ToString();
            }

            // ===== 藏身处 =====
            private static string BuildAreaLine(List<AreaRequirement> list, string color)
            {
                var sb = new StringBuilder();
                sb.Append($"<color={ToColorPatch.ColorToHex(MyMod.CommonColor.Value)}>藏身处需求: \n</color>");
                sb.Append($"<color={color}><b>");
                foreach (var a in list)
                {
                    sb.Append($"{LocalizeAreaName(a.AreaType)}{a.AreaLevel}级需求x{a.Count}\n");
                }
                sb.Append("</b></color>");
                return sb.ToString();
            }

            // ===== 交易来源 =====
            private static string BuildTradeLine(List<TradeRef> list, string color)
            {
                var sb = new StringBuilder();
                sb.Append($"<color={ToColorPatch.ColorToHex(MyMod.CommonColor.Value)}>交易来源: \n</color>");
                sb.Append($"<color={color}><b>");
                foreach (var t in list)
                {
                    sb.Append(BuildTradeEntry(t, false));
                    sb.Append("\n");
                }
                sb.Append("</b></color>");
                return sb.ToString();
            }

            // ===== 交易用途 =====
            private static string BuildTradeUseLine(List<TradeRef> list, string color)
            {
                var sb = new StringBuilder();
                sb.Append($"<color={ToColorPatch.ColorToHex(MyMod.CommonColor.Value)}>交易用途: \n</color>");
                sb.Append($"<color={color}><b>");
                foreach (var t in list)
                {
                    sb.Append(BuildTradeEntry(t, true));
                    sb.Append("\n");
                }
                sb.Append("</b></color>");
                return sb.ToString();
            }

            private static string BuildTradeEntry(TradeRef t, bool isUse)
            {
                var traderName = LocalizeTraderName(t.TraderId);
                var barterStr = string.Join("、", t.Barter.Select(kv => $"{LocalizeItemName(kv.Key)}x{kv.Value}"));

                if (isUse)
                {
                    return $"{traderName}{t.LoyalLevel}级兑换{LocalizeItemName(t.ResultId)}(完整配方: {barterStr})" +
                           BuildTradeLock(t);
                }

                var firstKey = t.Barter.Keys.FirstOrDefault();
                var firstValue = t.Barter.Values.FirstOrDefault();
                if (t.Barter.Count == 1 && firstKey != null && MoneyIds.Contains(firstKey))
                {
                    return $"{traderName}{t.LoyalLevel}级花费{firstValue}{LocalizeItemName(firstKey)}直接购买" +
                           BuildTradeLock(t);
                }

                return $"{traderName}{t.LoyalLevel}级兑换(完整配方: {barterStr})" + BuildTradeLock(t);
            }

            private static string BuildTradeLock(TradeRef t)
            {
                if (!t.IsLocked || string.IsNullOrEmpty(t.QuestId)) return "";
                return t.Stage == 0
                    ? $"(接取「{LocalizeQuestName(t.QuestId)}」后解锁)"
                    : $"(完成「{LocalizeQuestName(t.QuestId)}」后解锁)";
            }

            // ===== 制作配方 =====
            private static string BuildRecipeLine(List<RecipeRef> list, string color)
            {
                var sb = new StringBuilder();
                sb.Append($"<color={ToColorPatch.ColorToHex(MyMod.CommonColor.Value)}>制作配方: \n</color>");
                sb.Append($"<color={color}><b>");
                foreach (var r in list)
                {
                    var recipeText = BuildRecipeIngredients(r);
                    sb.Append($"{LocalizeAreaName(r.AreaType)}{r.AreaLevel}级一次产出{r.Count}个({FormatSecondTime(r.Time)}) ");
                    sb.Append($"({recipeText}) ");
                    sb.Append(BuildRecipeLock(r));
                    sb.Append("\n");
                }
                sb.Append("</b></color>");
                return sb.ToString();
            }

            // ===== 制作用途 =====
            private static string BuildRecipeUseLine(List<RecipeRef> list, string color)
            {
                var sb = new StringBuilder();
                sb.Append($"<color={ToColorPatch.ColorToHex(MyMod.CommonColor.Value)}>制作用途: \n</color>");
                sb.Append($"<color={color}><b>");
                foreach (var r in list)
                {
                    var recipeText = BuildRecipeIngredients(r);
                    sb.Append($"{LocalizeAreaName(r.AreaType)}{r.AreaLevel}级制作{LocalizeItemName(r.ResultId)}");
                    sb.Append($"(消耗{FormatSecondTime(r.Time)}，一次产出{r.Count}个)");
                    sb.Append($"(完整配方:{recipeText}) ");
                    sb.Append(BuildRecipeLock(r));
                    sb.Append("\n");
                }
                sb.Append("</b></color>");
                return sb.ToString();
            }

            private static string BuildRecipeIngredients(RecipeRef r)
            {
                var parts = r.Items.Select(kv => $"{LocalizeItemName(kv.Key)}x{kv.Value}").ToList();
                parts.AddRange(r.Tools.Select(t => $"{LocalizeItemName(t)}(不消耗)"));
                return string.Join("、", parts);
            }

            private static string BuildRecipeLock(RecipeRef r)
            {
                return r.Locked && !string.IsNullOrEmpty(r.QuestId)
                    ? $"(完成任务「{LocalizeQuestName(r.QuestId)}」后解锁)"
                    : "";
            }

            // ===== 获取途径（任务奖励） =====
            private static string BuildRewardLine(List<RewardRef> list, string color)
            {
                var sb = new StringBuilder();
                sb.Append($"<color={ToColorPatch.ColorToHex(MyMod.CommonColor.Value)}>获取途径: \n</color>");
                sb.Append($"<color={color}><b>");
                foreach (var r in list)
                {
                    var questText = r.Stage == 0
                        ? $"接取「{LocalizeQuestName(r.QuestId)}」后可领取{r.Count}个"
                        : $"完成「{LocalizeQuestName(r.QuestId)}」后可领取{r.Count}个";
                    sb.Append($"{questText}\n");
                }
                sb.Append("</b></color>");
                return sb.ToString();
            }

            // ===== 时间格式（秒 → X小时Y分Z秒，对齐旧版 FormatSecondTime） =====
            private static string FormatSecondTime(long seconds)
            {
                int hours = (int)(seconds / 3600);
                int minutes = (int)((seconds % 3600) / 60);
                int remainingSeconds = (int)(seconds % 60);
                string result = "";
                if (hours > 0) result += $"{hours}小时";
                if (minutes > 0)
                {
                    if (minutes >= 10) result += $"{minutes}分";
                    else result += (hours > 0 ? $"0{minutes}分" : $"{minutes}分");
                }
                if (remainingSeconds > 0)
                {
                    if (remainingSeconds >= 10) result += $"{remainingSeconds}秒";
                    else result += (minutes > 0 ? $"0{remainingSeconds}秒" : $"{remainingSeconds}秒");
                }
                return result.Trim();
            }
        }
    }
}
