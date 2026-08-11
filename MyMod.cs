using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Newtonsoft.Json;
using SPT.Common.Http;
using TMPro;
using UnityEngine;

namespace EternalHUD
{
    [BepInPlugin("eft.hiddenhiragi.eternalhud", "EternalHUD", "2.0.2")]
    public class MyMod : BaseUnityPlugin
    {
        // ===== 数据缓存 =====
        public static Dictionary<string, ItemRef> ItemDict { get; set; } = new();

        // ===== 实例捕获 =====
        /// <summary>战局内 Player（GameStart 时捕获）</summary>
        public static Player CorrectPlayer { get; set; }
        /// <summary>仓库 InventoryController（InventoryScreen.Show 时捕获；仅主菜单有效）</summary>
        public static InventoryController StashController { get; set; }
        /// <summary>仓库物品快照（每次 OnGameStart 从服务端拉取一次；templateId → (count, fir)）</summary>
        public static Dictionary<string, (int count, int fir)> StashSnapshot { get; } = new();

        // ===== Tooltip 折叠缓存（保留原机制） =====
        public static TextMeshProUGUI _cache_label;
        public static SimpleTooltip _cacheTooltip;
        public static string _cacheText;
        public static string _ammoText;
        public static string _priceText;
        public static string _armorText;
        public static string _questText;
        public static string _questHandoverText;
        public static string _questLeaveText;
        public static string _hideoutText;
        public static string _productText;
        public static string _productUseText;
        public static string _tradeText;
        public static string _tradeUseText;
        public static string _rewardText;
        public static string _itemID;
        public static string _itemName;
        public static bool _useeng = false;
        private KeyboardShortcut CopyItemIDKey = new KeyboardShortcut(KeyCode.C, KeyCode.LeftAlt, KeyCode.LeftControl);
        private bool _copyKeyLastFrame = false;

        // ===== 配置字段 =====
        internal static ConfigEntry<float> TooltipDelay;
        internal static ConfigEntry<float> TooltipWidth;
        internal static ConfigEntry<Color> CommonColor, RedsColor, GreenColor, HandOverRaidColor, HandOverColor, LeaveColor;
        internal static ConfigEntry<Color> HideoutColor, TradeColor, TradeUseColor, ProductColor, ProductUseColor, RewardColor, PriceColor;
        internal static ConfigEntry<Color> AmmoColor1, AmmoColor2, AmmoColor3, AmmoColor4, AmmoColor5, AmmoColor6;
        internal static ConfigEntry<Color> TagColor0, TagColor1, TagColor2, TagColor3, TagColor4, TagColor5, TagColor6, TagColor7;
        internal static ConfigEntry<bool> ShowEName, ShowID, ShowTag, ShowStashCount, ShowRagfairData, ShowPrice, StaticPrice;
        internal static ConfigEntry<bool> ShowAmmoData, ShowArmorData, ShowReward, ShowQuestData, ShowHideoutData, ShowTradeData, ShowTradeUseData, ShowProductUseData, ShowProductData;
        internal static ConfigEntry<KeyCode> PriceKeyCode, AmmoKeyCode, QuestKeyCode, QuestHandoverKeyCode, QuestLeaveKeyCode, HideoutKeyCode, ProductKeyCode, ProductUseKeyCode, TradeKeyCode, TradeUseKeyCode, RewardKeyCode;
        internal static ConfigEntry<bool> FoldPriceData, FoldAmmoData, FoldQuestData, FoldQuestHandoverData, FoldQuestLeaveData, FoldHideoutData, FoldProductData, FoldProductUseData, FoldTradeData, FoldTradeUseData, FoldRewardData;
        internal static ConfigEntry<bool> ChangeSearchTime, ChangeSearchSound;
        internal static ConfigEntry<float> SearchTimeMutiper;

        public void Awake()
        {
            var harmony = new Harmony("eft.hiddenhiragi.eternalhud");
            harmony.PatchAll();


            // ===== 配置绑定（保留原 key，避免用户配置失效） =====
            TooltipDelay = Config.Bind("通用设置", "战局内显示延迟", 0f, new ConfigDescription("战局内悬停显示延迟", new AcceptableValueRange<float>(0f, 5f)));
            TooltipWidth = Config.Bind("通用设置", "Tooltip宽度", 300f, new ConfigDescription("Tooltip宽度", new AcceptableValueRange<float>(300f, 900f)));
            CommonColor = Config.Bind("色彩设置", "标签颜色", new Color(1f, 1f, 1f, 1f));
            RedsColor = Config.Bind("色彩设置", "红字颜色", new Color(1f, 0.258f, 0.258f, 1f));
            GreenColor = Config.Bind("色彩设置", "绿字颜色", new Color(0.258f, 1f, 0.258f, 1f));
            HideoutColor = Config.Bind("色彩设置", "藏身处需求颜色", new Color(1f, 0.772f, 0f, 1f));
            TradeColor = Config.Bind("色彩设置", "交易途径颜色", new Color(0.482f, 0.407f, 0.933f, 1f));
            TradeUseColor = Config.Bind("色彩设置", "交易用途颜色", new Color(0.945f, 0.717f, 1f, 1f));
            ProductColor = Config.Bind("色彩设置", "制作配方颜色", new Color(0.4f, 0.627f, 0.933f, 1f));
            ProductUseColor = Config.Bind("色彩设置", "制作用途颜色", new Color(0.658f, 0.984f, 0.29f, 1f));
            HandOverRaidColor = Config.Bind("色彩设置", "上交物品颜色", new Color(0.964f, 0.376f, 0.67f, 1f));
            RewardColor = Config.Bind("色彩设置", "任务奖励颜色", new Color(0.933f, 0.933f, 0f, 1f));
            HandOverColor = Config.Bind("色彩设置", "交付物品颜色", new Color(0.501f, 0.878f, 1f, 1f));
            LeaveColor = Config.Bind("色彩设置", "安放物品颜色", new Color(0f, 0.98f, 0.603f, 1f));
            PriceColor = Config.Bind("色彩设置", "价格颜色", new Color(1f, 1f, 0.501f, 1f));
            AmmoColor6 = Config.Bind("甲弹等级颜色配置", "甲弹等级6颜色", new Color(0.8f, 0f, 0f, 1f));
            AmmoColor5 = Config.Bind("甲弹等级颜色配置", "甲弹等级5颜色", new Color(1f, 0.878f, 0.4f, 1f));
            AmmoColor4 = Config.Bind("甲弹等级颜色配置", "甲弹等级4颜色", new Color(0.666f, 0f, 0.8f, 1f));
            AmmoColor3 = Config.Bind("甲弹等级颜色配置", "甲弹等级3颜色", new Color(0f, 0.4f, 0.8f, 1f));
            AmmoColor2 = Config.Bind("甲弹等级颜色配置", "甲弹等级2颜色", new Color(0f, 0.8f, 0f, 1f));
            AmmoColor1 = Config.Bind("甲弹等级颜色配置", "甲弹等级1颜色", new Color(0.8f, 0.8f, 0.8f, 1f));

            TagColor0 = Config.Bind("品质标签颜色", "等级0颜色", new Color(0.502f, 0.502f, 0.502f, 1f));   // #808080
            TagColor1 = Config.Bind("品质标签颜色", "等级1颜色", new Color(1f, 1f, 1f, 1f));               // #FFFFFF
            TagColor2 = Config.Bind("品质标签颜色", "等级2颜色", new Color(0f, 0.667f, 0f, 1f));           // #00AA00
            TagColor3 = Config.Bind("品质标签颜色", "等级3颜色", new Color(0f, 0.627f, 1f, 1f));           // #00A0FF
            TagColor4 = Config.Bind("品质标签颜色", "等级4颜色", new Color(0.667f, 0f, 0.667f, 1f));       // #AA00AA
            TagColor5 = Config.Bind("品质标签颜色", "等级5颜色", new Color(1f, 0.667f, 0f, 1f));           // #FFAA00
            TagColor6 = Config.Bind("品质标签颜色", "等级6颜色", new Color(0.667f, 0f, 0f, 1f));           // #AA0000
            TagColor7 = Config.Bind("品质标签颜色", "等级7颜色", new Color(1f, 0.333f, 1f, 1f));           // #FF55FF

            ShowEName = Config.Bind("功能设置", "显示英文名", true);
            ShowID = Config.Bind("功能设置", "显示物品ID", true);
            ShowTag = Config.Bind("功能设置", "显示品质", false);
            ShowStashCount = Config.Bind("功能设置", "显示仓库数量", true);
            ShowRagfairData = Config.Bind("功能设置", "显示跳蚤市场信息", true);
            ShowPrice = Config.Bind("功能设置", "显示物品价格", true);
            StaticPrice = Config.Bind("功能设置", "启用静态价格模式", true);
            ShowAmmoData = Config.Bind("功能设置", "显示弹药数据", true);
            ShowArmorData = Config.Bind("功能设置", "显示护甲数据", true);
            ShowReward = Config.Bind("功能设置", "显示任务奖励数据", true);
            ShowQuestData = Config.Bind("功能设置", "显示任务需求信息", true);
            ShowHideoutData = Config.Bind("功能设置", "显示藏身处需求信息", true);
            ShowTradeData = Config.Bind("功能设置", "显示获取途径", true);
            ShowTradeUseData = Config.Bind("功能设置", "显示交易用途", true);
            ShowProductUseData = Config.Bind("功能设置", "显示制作用途", true);
            ShowProductData = Config.Bind("功能设置", "显示制作配方", true);

            PriceKeyCode = Config.Bind("按键设置", "显示物品价值快捷键", KeyCode.LeftShift);
            AmmoKeyCode = Config.Bind("按键设置", "显示弹药数据快捷键", KeyCode.LeftControl);
            QuestKeyCode = Config.Bind("按键设置", "显示交付物品数据快捷键", KeyCode.Q);
            QuestHandoverKeyCode = Config.Bind("按键设置", "显示上交物品数据快捷键", KeyCode.H);
            QuestLeaveKeyCode = Config.Bind("按键设置", "显示安放物品数据快捷键", KeyCode.L);
            HideoutKeyCode = Config.Bind("按键设置", "显示藏身处需求数据快捷键", KeyCode.A);
            ProductKeyCode = Config.Bind("按键设置", "显示制作配方数据快捷键", KeyCode.P);
            ProductUseKeyCode = Config.Bind("按键设置", "显示制作用途数据快捷键", KeyCode.U);
            TradeKeyCode = Config.Bind("按键设置", "显示交易来源数据快捷键", KeyCode.T);
            TradeUseKeyCode = Config.Bind("按键设置", "显示交易用途数据快捷键", KeyCode.W);
            RewardKeyCode = Config.Bind("按键设置", "显示获取途径数据快捷键", KeyCode.R);

            FoldPriceData = Config.Bind("模式设置", "为物品价格启用折叠模式", false);
            FoldAmmoData = Config.Bind("模式设置", "为弹药数据启用折叠模式", false);
            FoldQuestData = Config.Bind("模式设置", "为交付物品数据启用折叠模式", true);
            FoldQuestHandoverData = Config.Bind("模式设置", "为上交物品数据启用折叠模式", true);
            FoldQuestLeaveData = Config.Bind("模式设置", "为安放物品数据启用折叠模式", true);
            FoldHideoutData = Config.Bind("模式设置", "为藏身处需求数据启用折叠模式", true);
            FoldProductData = Config.Bind("模式设置", "为制作配方数据启用折叠模式", true);
            FoldProductUseData = Config.Bind("模式设置", "为制作用途数据启用折叠模式", true);
            FoldTradeData = Config.Bind("模式设置", "为交易来源数据启用折叠模式", true);
            FoldTradeUseData = Config.Bind("模式设置", "为交易用途数据启用折叠模式", true);
            FoldRewardData = Config.Bind("模式设置", "为获取途径数据启用折叠模式", true);

            ChangeSearchTime = Config.Bind("搜索设置", "更改搜索时长", true);
            ChangeSearchSound = Config.Bind("搜索设置", "更改搜索音效", true);
            SearchTimeMutiper = Config.Bind("搜索设置", "搜索时间倍率", 0.75f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 1f)));

            // ===== 拉取服务端数据 =====
            var list = HudRequest.FetchItemData();
            ItemDict = list.ToDictionary(x => x.Id, x => x);
            Logger.LogInfo($"[永恒HUD] 已加载 {ItemDict.Count} 个物品情报");

            _ammoText = _priceText = _questText = _questHandoverText = _questLeaveText =
                _hideoutText = _productText = _productUseText = _tradeText = _tradeUseText =
                _rewardText = _itemID = _itemName = string.Empty;
        }

        public void Update()
        {
            // ===== 折叠刷新机制：保留原逻辑，逐帧按按键状态从 _cacheText 重建 =====
            if (_cacheTooltip == null || _cache_label == null) return;

            string newText = _cacheText;
            bool priceKey = Input.GetKey(PriceKeyCode.Value);
            bool ammoKey = Input.GetKey(AmmoKeyCode.Value);
            bool questKey = Input.GetKey(QuestKeyCode.Value);
            bool questHandoverKey = Input.GetKey(QuestHandoverKeyCode.Value);
            bool questLeaveKey = Input.GetKey(QuestLeaveKeyCode.Value);
            bool hideoutKey = Input.GetKey(HideoutKeyCode.Value);
            bool productKey = Input.GetKey(ProductKeyCode.Value);
            bool productUseKey = Input.GetKey(ProductUseKeyCode.Value);
            bool tradeKey = Input.GetKey(TradeKeyCode.Value);
            bool tradeUseKey = Input.GetKey(TradeUseKeyCode.Value);
            bool rewardKey = Input.GetKey(RewardKeyCode.Value);

            string questString = $"<i>按住{QuestKeyCode.Value}显示交付物品数据</i>\n";
            string questHandoverString = $"<i>按住{QuestHandoverKeyCode.Value}显示上交物品数据</i>\n";
            string questLeaveString = $"<i>按住{QuestLeaveKeyCode.Value}显示安放物品数据</i>\n";
            string hideoutString = $"<i>按住{HideoutKeyCode.Value}显示藏身处需求数据</i>\n";
            string procutString = $"<i>按住{ProductKeyCode.Value}显示制作配方数据</i>\n";
            string procutUseString = $"<i>按住{ProductUseKeyCode.Value}显示制作用途数据</i>\n";
            string tradeString = $"<i>按住{TradeKeyCode.Value}显示交易来源数据</i>\n";
            string tradeUseString = $"<i>按住{TradeUseKeyCode.Value}显示交易用途数据</i>\n";
            string rewardString = $"<i>按住{RewardKeyCode.Value}显示获取途径数据</i>\n";

            if (!priceKey && _priceText != string.Empty && FoldPriceData.Value)
                newText = newText.Replace(_priceText, $"<i>按住{PriceKeyCode.Value}显示参考价格</i>\n");
            if (!ammoKey && _ammoText != string.Empty && FoldAmmoData.Value)
                newText = newText.Replace(_ammoText, $"<i>按住{AmmoKeyCode.Value}显示子弹数据</i>\n");
            if (!questKey && _questText != string.Empty && FoldQuestData.Value)
                newText = newText.Replace(_questText, questString);
            if (!questHandoverKey && _questHandoverText != string.Empty && FoldQuestHandoverData.Value)
                newText = newText.Replace(_questHandoverText, questHandoverString);
            if (!questLeaveKey && _questLeaveText != string.Empty && FoldQuestLeaveData.Value)
                newText = newText.Replace(_questLeaveText, questLeaveString);
            if (!hideoutKey && _hideoutText != string.Empty && FoldHideoutData.Value)
                newText = newText.Replace(_hideoutText, hideoutString);
            if (!productKey && _productText != string.Empty && FoldProductData.Value)
                newText = newText.Replace(_productText, procutString);
            if (!productUseKey && _productUseText != string.Empty && FoldProductUseData.Value)
                newText = newText.Replace(_productUseText, procutUseString);
            if (!tradeKey && _tradeText != string.Empty && FoldTradeData.Value)
                newText = newText.Replace(_tradeText, tradeString);
            if (!tradeUseKey && _tradeUseText != string.Empty && FoldTradeUseData.Value)
                newText = newText.Replace(_tradeUseText, tradeUseString);
            if (!rewardKey && _rewardText != string.Empty && FoldRewardData.Value)
                newText = newText.Replace(_rewardText, rewardString);

            if (CopyItemIDKey.IsPressed() && !_copyKeyLastFrame)
            {
                GUIUtility.systemCopyBuffer = _itemID;
                NotificationManager.DisplayMessageNotification(
                    $"物品{_itemName}的ID已复制到剪贴板!",
                    ENotificationDurationType.Default,
                    ENotificationIconType.Default,
                    null);
                _copyKeyLastFrame = true;
            }
            if (!CopyItemIDKey.IsPressed()) _copyKeyLastFrame = false;

            _cacheTooltip.SetText(newText);
        }

        // ===== 战局开始：捕获 Player 实例 + 拉取仓库物品快照（每次战局请求一次） =====
        [HarmonyPatch(typeof(GameWorld), "OnGameStarted")]
        public class GameStartPatch
        {
            [HarmonyPostfix]
            public static void Postfix(GameWorld __instance)
            {
                CorrectPlayer = __instance.MainPlayer;
                RefreshStashSnapshotFromServer();
            }

            private static void RefreshStashSnapshotFromServer()
            {
                try
                {
                    var json = RequestHandler.PostJson("/EternalHUD/getStashSnapshot", "{}");
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, int[]>>(json);
                    if (dict == null) return;
                    StashSnapshot.Clear();
                    foreach (var (k, v) in dict)
                    {
                        StashSnapshot[k] = (v.Length > 0 ? v[0] : 0, v.Length > 1 ? v[1] : 0);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[永恒HUD] 拉取仓库快照失败: {ex.Message}");
                }
            }
        }

        // ===== 仓库：捕获 InventoryController 实例（仅主菜单显示"已拥有"用） =====
        // InventoryScreen.Show 有多个重载，必须显式指定参数类型，否则 Harmony 抛 AmbiguousMatchException
        [HarmonyPatch(typeof(InventoryScreen), nameof(InventoryScreen.Show), new Type[]
        {
            typeof(EFT.HealthSystem.IHealthController),
            typeof(InventoryController),
            typeof(EFT.Quests.QuestController),
            typeof(EFT.Achievements.AchievementsController),
            typeof(EFT.Prestige.PrestigeController),
            typeof(CompoundItem),
            typeof(EFT.UI.EInventoryTab),
            typeof(EFT.IEftSession),
            typeof(EFT.InventoryLogic.ItemContext),
            typeof(bool)
        })]
        public class InventoryScreenShowPatch
        {
            [HarmonyPostfix]
            public static void Postfix(InventoryController controller)
            {
                StashController = controller;
            }
        }
    }
}
