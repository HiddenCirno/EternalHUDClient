using System.Collections.Generic;

namespace EternalHUD
{
    // ===== 服务端 → 客户端 的物品情报（纯数据，与服务端 HudModels.cs 完全对齐） =====
    public class ItemRef
    {
        public string Id { get; set; } = "";
        public int Level { get; set; }
        public bool CanSell { get; set; }
        public int MarketPrice { get; set; }
        public int HandbookPrice { get; set; }
        public bool ChangeSearchTime { get; set; }
        public AmmoInfo? Ammo { get; set; }
        public ArmorInfo? Armor { get; set; }
        public List<QuestRequirement> QuestReqs { get; set; } = new();
        public List<RewardRef> RewardFrom { get; set; } = new();
        public List<AreaRequirement> AreaReqs { get; set; } = new();
        public List<RecipeRef> Recipes { get; set; } = new();
        public List<RecipeRef> RecipeUses { get; set; } = new();
        public List<TradeRef> Trades { get; set; } = new();
        public List<TradeRef> TradeUses { get; set; } = new();
    }

    public class AmmoInfo
    {
        public int Pent { get; set; }
        public int Damage { get; set; }
        public int ArmorDamage { get; set; }
        public int BulletCount { get; set; }
    }

    public class ArmorInfo
    {
        public int Level { get; set; }
        public string Blunt { get; set; } = "";
        public int MaxDurability { get; set; }
        public string Material { get; set; } = "";
        public string Weight { get; set; } = "";
    }

    public class QuestRequirement
    {
        public string QuestId { get; set; } = "";
        public string TraderId { get; set; } = "";
        public bool FindInRaid { get; set; }
        public int Count { get; set; }
        public string Type { get; set; } = "";
    }

    public class RewardRef
    {
        public string QuestId { get; set; } = "";
        public int Count { get; set; }
        public int Stage { get; set; }
    }

    public class AreaRequirement
    {
        public int AreaType { get; set; }
        public int AreaLevel { get; set; }
        public int Count { get; set; }
    }

    public class RecipeRef
    {
        public string ResultId { get; set; } = "";
        public int Count { get; set; }
        public int Time { get; set; }
        public bool Locked { get; set; }
        public string QuestId { get; set; } = "";
        public int AreaType { get; set; }
        public int AreaLevel { get; set; }
        public Dictionary<string, int> Items { get; set; } = new();
        public List<string> Tools { get; set; } = new();
    }

    public class TradeRef
    {
        public string TraderId { get; set; } = "";
        public int LoyalLevel { get; set; }
        public bool IsLocked { get; set; }
        public string QuestId { get; set; } = "";
        public int Stage { get; set; }
        public Dictionary<string, double> Barter { get; set; } = new();
        public string ResultId { get; set; } = "";
    }

    /// <summary>当前悬停的物品实例（由 PointEnter/PointOuter patch 维护）</summary>
    public static class HoverState
    {
        public static EFT.InventoryLogic.Item? CurrentItem;
    }
}
