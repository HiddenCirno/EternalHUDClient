using System.Collections.Generic;
using Newtonsoft.Json;
using SPT.Common.Http;

namespace EternalHUD
{
    /// <summary>
    /// 客户端唯一的数据请求：启动时拉取一次全量物品情报。
    /// 已移除 getPMCData / getRagfairPrice / pullPriceMap（仓库数量本地读取，价格并入 ItemRef）。
    /// </summary>
    internal static class HudRequest
    {
        public static List<ItemRef> FetchItemData()
        {
            try
            {
                var json = RequestHandler.PostJson("/EternalHUD/getNameData", "{}");
                return JsonConvert.DeserializeObject<List<ItemRef>>(json) ?? new List<ItemRef>();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[永恒HUD] 拉取物品数据失败: {ex.Message}");
                return new List<ItemRef>();
            }
        }
    }
}
