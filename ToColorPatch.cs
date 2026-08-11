using System;
using HarmonyLib;
using JsonType;
using UnityEngine;

namespace EternalHUD
{
    /// <summary>
    /// 4.1 适配：原 GClass1409.ToColor → JsonType.TaxonomyColorExtension.ToColor。
    /// 支持扩展 TaxonomyColor 枚举之外的自定义 24 位色值。
    /// </summary>
    [HarmonyPatch(typeof(TaxonomyColorExtension), "ToColor")]
    public static class ToColorPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref Color __result, TaxonomyColor taxonomyColor)
        {
            if (Enum.IsDefined(typeof(TaxonomyColor), taxonomyColor))
            {
                return true;
            }

            int colorCodeAsInt = (int)taxonomyColor - Enum.GetValues(typeof(TaxonomyColor)).Length;
            string colorCode = colorCodeAsInt.ToString("X6");

            if (colorCode.Length == 6)
            {
                __result = HexToColor(colorCode);
            }
            else if (colorCode.Length == 8)
            {
                __result = HexToColorAlpha(colorCode);
            }
            else
            {
                __result = Color.white;
            }

            return false;
        }

        private static Color HexToColor(string hex)
        {
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color32(r, g, b, 255);
        }

        private static Color HexToColorAlpha(string hex)
        {
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            byte a = Convert.ToByte(hex.Substring(6, 2), 16);
            return new Color32(r, g, b, a);
        }

        public static string ColorToHex(Color color)
        {
            int r = Mathf.RoundToInt(color.r * 255f);
            int g = Mathf.RoundToInt(color.g * 255f);
            int b = Mathf.RoundToInt(color.b * 255f);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
