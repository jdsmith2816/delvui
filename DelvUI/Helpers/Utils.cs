using DelvUI.Interface.GeneralElements;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using DelvUI.Config;
using DelvUI.Enums;
using Colourful;
using System.Text.RegularExpressions;

namespace DelvUI.Helpers
{
    internal static class Utils
    {
        public static GameObject? GetBattleChocobo(GameObject? player)
        {
            if (player == null)
            {
                return null;
            }

            // only the first 200 elements in the array are relevant due to the order in which SE packs data into the array
            // we do a step of 2 because its always an actor followed by its companion
            for (var i = 0; i < 200; i += 2)
            {
                var gameObject = Plugin.ObjectTable[i];

                if (gameObject == null || gameObject is not BattleNpc battleNpc)
                {
                    continue;
                }

                if (battleNpc.BattleNpcKind == BattleNpcSubKind.Chocobo && battleNpc.OwnerId == player.ObjectId)
                {
                    return gameObject;
                }
            }

            return null;
        }

        public static unsafe bool IsHostileMemory(BattleNpc npc)
        {
            if (npc == null)
            {
                return false;
            }

            return (npc.BattleNpcKind == BattleNpcSubKind.Enemy || (int)npc.BattleNpcKind == 1)
                && *(byte*)(npc.Address + 0x1980) != 0
                && *(byte*)(npc.Address + 0x193C) != 1;
        }

        public static unsafe float ActorShieldValue(GameObject? actor)
        {
            if (actor == null)
            {
                return 0f;
            }

            return Math.Min(*(int*)(actor.Address + 0x1997), 100) / 100f;
        }

        public static string DurationToString(double duration)
        {
            if (duration == 0)
            {
                return "";
            }

            var t = TimeSpan.FromSeconds(duration);

            if (t.Hours > 1)
            {
                return t.Hours + "h";
            }

            if (t.Minutes >= 5)
            {
                return t.Minutes + "m";
            }

            if (t.Minutes >= 1)
            {
                return $"{t.Minutes}:{t.Seconds:00}";
            }

            return t.Seconds.ToString();
        }

        //The converter is intended to be built once, and then re-used for each conversion you need to do. create the converter once (e.g. store it in a field somewhere)
        private static readonly IColorConverter<RGBColor, LabColor> _rgbToLab = new ConverterBuilder().FromRGB().ToLab().Build();
        private static readonly IColorConverter<LabColor, RGBColor> _labToRgb = new ConverterBuilder().FromLab().ToRGB().Build();

        private static readonly IColorConverter<RGBColor, LChabColor> _rgbToLChab = new ConverterBuilder().FromRGB().ToLChab().Build();
        private static readonly IColorConverter<LChabColor, RGBColor> _lchabToRgb = new ConverterBuilder().FromLChab().ToRGB().Build();

        private static readonly IColorConverter<RGBColor, XYZColor> _rgbToXyz = new ConverterBuilder().FromRGB(RGBWorkingSpaces.sRGB).ToXYZ(Illuminants.D65).Build();
        private static readonly IColorConverter<XYZColor, RGBColor> _xyzToRgb = new ConverterBuilder().FromXYZ(Illuminants.D65).ToRGB(RGBWorkingSpaces.sRGB).Build();

        private static readonly IColorConverter<RGBColor, LChuvColor> _rgbToLChuv = new ConverterBuilder().FromRGB().ToLChuv().Build();
        private static readonly IColorConverter<LChuvColor, RGBColor> _lchuvToRgb = new ConverterBuilder().FromLChuv().ToRGB().Build();

        private static readonly IColorConverter<RGBColor, LuvColor> _rgbToLuv = new ConverterBuilder().FromRGB().ToLuv().Build();
        private static readonly IColorConverter<LuvColor, RGBColor> _luvToRgb = new ConverterBuilder().FromLuv().ToRGB().Build();

        private static readonly IColorConverter<RGBColor, JzazbzColor> _rgbToJzazbz = new ConverterBuilder().FromRGB().ToJzazbz().Build();
        private static readonly IColorConverter<JzazbzColor, RGBColor> _jzazbzToRgb = new ConverterBuilder().FromJzazbz().ToRGB().Build();

        private static readonly IColorConverter<RGBColor, JzCzhzColor> _rgbToJzCzhz = new ConverterBuilder().FromRGB().ToJzCzhz().Build();
        private static readonly IColorConverter<JzCzhzColor, RGBColor> _jzCzhzToRgb = new ConverterBuilder().FromJzCzhz().ToRGB().Build();

        public static PluginConfigColor ColorByHealthValue(float i, float min, float max, PluginConfigColor fullHealthColor, PluginConfigColor lowHealthColor, BlendMode blendMode)
        {
            float ratio = i;
            if (min > 0 || max < 1)
            {
                if (i < min)
                {
                    ratio = 0;
                }
                else if (i > max)
                {
                    ratio = 1;
                }
                else
                {
                    var range = max - min;
                    ratio = (i - min) / range;
                }
            }

            //convert our plugin colors to RGBColor
            var rgbFullHealthColor = new RGBColor(fullHealthColor.Vector.X, fullHealthColor.Vector.Y, fullHealthColor.Vector.Z);
            var rgbLowHealthColor = new RGBColor(lowHealthColor.Vector.X, lowHealthColor.Vector.Y, lowHealthColor.Vector.Z);
            float alpha = (fullHealthColor.Vector.W - lowHealthColor.Vector.W) * ratio + lowHealthColor.Vector.W;

            switch (blendMode)
            {
                case BlendMode.LAB:
                    {
                    //convert RGB to LAB
                    var rgbFullHealthLab = _rgbToLab.Convert(rgbFullHealthColor);
                    var rgbLowHealthLab = _rgbToLab.Convert(rgbLowHealthColor);

                    float LabresultL = (float)((rgbFullHealthLab.L - rgbLowHealthLab.L) * ratio + rgbLowHealthLab.L);
                    float Labresulta = (float)((rgbFullHealthLab.a - rgbLowHealthLab.a) * ratio + rgbLowHealthLab.a);
                    float Labresultb = (float)((rgbFullHealthLab.b - rgbLowHealthLab.b) * ratio + rgbLowHealthLab.b);

                    var newColorLab = new LabColor(LabresultL, Labresulta, Labresultb);
                    var newColorLab2RGB = _labToRgb.Convert(newColorLab);

                    newColorLab2RGB.NormalizeIntensity();

                    return new PluginConfigColor(new Vector4((float)newColorLab2RGB.R, (float)newColorLab2RGB.G, (float)newColorLab2RGB.B, alpha));
                    }

                case BlendMode.LChab:
                    {
                        //convert RGB to LChab
                        var rgbFullHealthLChab = _rgbToLChab.Convert(rgbFullHealthColor);
                        var rgbLowHealthLChab = _rgbToLChab.Convert(rgbLowHealthColor);

                        //LChab interpolation results
                        float LChabresultL = (float)((rgbFullHealthLChab.L - rgbLowHealthLChab.L) * ratio + rgbLowHealthLChab.L);
                        float LChabresultC = (float)((rgbFullHealthLChab.C - rgbLowHealthLChab.C) * ratio + rgbLowHealthLChab.C);
                        float LChabresulth = (float)((rgbFullHealthLChab.h - rgbLowHealthLChab.h) * ratio + rgbLowHealthLChab.h);

                        var newColorLChab = new LChabColor(LChabresultL, LChabresultC, LChabresulth);

                        var newColorLChab2RGB = _lchabToRgb.Convert(newColorLChab);

                        newColorLChab2RGB.NormalizeIntensity();

                        return new PluginConfigColor(new Vector4((float)newColorLChab2RGB.R, (float)newColorLChab2RGB.G, (float)newColorLChab2RGB.B, alpha));
                    }
                case BlendMode.XYZ:
                    {
                        //convert RGB to XYZ
                        var rgbFullHealthXyz = _rgbToXyz.Convert(rgbFullHealthColor);
                        var rgbLowHealthXyz = _rgbToXyz.Convert(rgbLowHealthColor);

                        //XYZ interpolation results
                        float XYZresultX = (float)((rgbFullHealthXyz.X - rgbLowHealthXyz.X) * ratio + rgbLowHealthXyz.X);
                        float XYZresultY = (float)((rgbFullHealthXyz.Y - rgbLowHealthXyz.Y) * ratio + rgbLowHealthXyz.Y);
                        float XYZresultZ = (float)((rgbFullHealthXyz.Z - rgbLowHealthXyz.Z) * ratio + rgbLowHealthXyz.Z);

                        var newColorXYZ = new XYZColor(XYZresultX, XYZresultY, XYZresultZ);

                        var newColorXYZ2RGB = _xyzToRgb.Convert(newColorXYZ);

                        newColorXYZ2RGB.NormalizeIntensity();

                        return new PluginConfigColor(new Vector4((float)newColorXYZ2RGB.R, (float)newColorXYZ2RGB.G, (float)newColorXYZ2RGB.B, alpha));
                    }
                case BlendMode.RGB:
                    {
                        //RGB interpolation results
                        float resultR = (float)((fullHealthColor.Vector.X - lowHealthColor.Vector.X) * ratio + lowHealthColor.Vector.X);
                        float resultG = (float)((fullHealthColor.Vector.Y - lowHealthColor.Vector.Y) * ratio + lowHealthColor.Vector.Y);
                        float resultB = (float)((fullHealthColor.Vector.Z - lowHealthColor.Vector.Z) * ratio + lowHealthColor.Vector.Z);

                        var newColorRGB = new RGBColor(resultR, resultG, resultB);

                        return new PluginConfigColor(new Vector4((float)newColorRGB.R, (float)newColorRGB.G, (float)newColorRGB.B, alpha));
                    }
                case BlendMode.LChuv:
                    {
                        //convert RGB to LChuv
                        var rgbFullHealthLChuv = _rgbToLChuv.Convert(rgbFullHealthColor);
                        var rgbLowHealthLChuv = _rgbToLChuv.Convert(rgbLowHealthColor);

                        //LChuv interpolation results
                        float LChuvresultL = (float)((rgbFullHealthLChuv.L - rgbLowHealthLChuv.L) * ratio + rgbLowHealthLChuv.L);
                        float LChuvresultC = (float)((rgbFullHealthLChuv.C - rgbLowHealthLChuv.C) * ratio + rgbLowHealthLChuv.C);
                        float LChuvresulth = (float)((rgbFullHealthLChuv.h - rgbLowHealthLChuv.h) * ratio + rgbLowHealthLChuv.h);

                        var newColorLChuv = new LChuvColor(LChuvresultL, LChuvresultC, LChuvresulth);

                        var newColorLChuv2RGB = _lchuvToRgb.Convert(newColorLChuv);

                        newColorLChuv2RGB.NormalizeIntensity();

                        return new PluginConfigColor(new Vector4((float)newColorLChuv2RGB.R, (float)newColorLChuv2RGB.G, (float)newColorLChuv2RGB.B, alpha));
                    }

                case BlendMode.Luv:
                    {
                        //convert RGB to Luv
                        var rgbFullHealthLuv = _rgbToLuv.Convert(rgbFullHealthColor);
                        var rgbLowHealthLuv = _rgbToLuv.Convert(rgbLowHealthColor);

                        //Luv interpolation results
                        float LuvresultL = (float)((rgbFullHealthLuv.L - rgbLowHealthLuv.L) * ratio + rgbLowHealthLuv.L);
                        float Luvresultu = (float)((rgbFullHealthLuv.u - rgbLowHealthLuv.u) * ratio + rgbLowHealthLuv.u);
                        float Luvresultv = (float)((rgbFullHealthLuv.v - rgbLowHealthLuv.v) * ratio + rgbLowHealthLuv.v);

                        var newColorLuv = new LuvColor(LuvresultL, Luvresultu, Luvresultv);

                        var newColorLuv2RGB = _luvToRgb.Convert(newColorLuv);

                        newColorLuv2RGB.NormalizeIntensity();

                        return new PluginConfigColor(new Vector4((float)newColorLuv2RGB.R, (float)newColorLuv2RGB.G, (float)newColorLuv2RGB.B, alpha));
                    }
                case BlendMode.Jzazbz:
                    {
                        //convert RGB to Jzazbz
                        var rgbFullHealthJzazbz = _rgbToJzazbz.Convert(rgbFullHealthColor);
                        var rgbLowHealthJzazbz = _rgbToJzazbz.Convert(rgbLowHealthColor);

                        //Jzazbz interpolation results
                        float JzazbzresultJz = (float)((rgbFullHealthJzazbz.Jz - rgbLowHealthJzazbz.Jz) * ratio + rgbLowHealthJzazbz.Jz);
                        float Jzazbzresultaz = (float)((rgbFullHealthJzazbz.az - rgbLowHealthJzazbz.az) * ratio + rgbLowHealthJzazbz.az);
                        float Jzazbzresultbz = (float)((rgbFullHealthJzazbz.bz - rgbLowHealthJzazbz.bz) * ratio + rgbLowHealthJzazbz.bz);

                        var newColorJzazbz = new JzazbzColor(JzazbzresultJz, Jzazbzresultaz, Jzazbzresultbz);

                        var newColorJzazbz2RGB = _jzazbzToRgb.Convert(newColorJzazbz);

                        newColorJzazbz2RGB.NormalizeIntensity();

                        return new PluginConfigColor(new Vector4((float)newColorJzazbz2RGB.R, (float)newColorJzazbz2RGB.G, (float)newColorJzazbz2RGB.B, alpha));
                    }
                case BlendMode.JzCzhz:
                    {
                        //convert RGB to JzCzhz
                        var rgbFullHealthJzCzhz = _rgbToJzCzhz.Convert(rgbFullHealthColor);
                        var rgbLowHealthJzCzhz = _rgbToJzCzhz.Convert(rgbLowHealthColor);

                        //Jzazbz interpolation results
                        float JzCzhzresultJz = (float)((rgbFullHealthJzCzhz.Jz - rgbLowHealthJzCzhz.Jz) * ratio + rgbLowHealthJzCzhz.Jz);
                        float JzCzhzresultCz = (float)((rgbFullHealthJzCzhz.Cz - rgbLowHealthJzCzhz.Cz) * ratio + rgbLowHealthJzCzhz.Cz);
                        float JzCzhzresulthz = (float)((rgbFullHealthJzCzhz.hz - rgbLowHealthJzCzhz.hz) * ratio + rgbLowHealthJzCzhz.hz);

                        var newColorJzCzhz = new JzCzhzColor(JzCzhzresultJz, JzCzhzresultCz, JzCzhzresulthz);

                        var newColorJzCzhz2RGB = _jzCzhzToRgb.Convert(newColorJzCzhz);

                        newColorJzCzhz2RGB.NormalizeIntensity();

                        return new PluginConfigColor(new Vector4((float)newColorJzCzhz2RGB.R, (float)newColorJzCzhz2RGB.G, (float)newColorJzCzhz2RGB.B, alpha));
                    }

                default: throw new ArgumentOutOfRangeException();
            }
        }


        public static PluginConfigColor ColorForActor(GameObject? actor)
        {
            if (actor == null || actor is not Character character)
            {
                return GlobalColors.Instance.NPCNeutralColor;
            }

            switch (character.ObjectKind)
            {
                // Still need to figure out the "orange" state; aggroed but not yet attacked.
                case ObjectKind.Player:
                    return GlobalColors.Instance.SafeColorForJobId(character.ClassJob.Id);

                case ObjectKind.BattleNpc when (character.StatusFlags & StatusFlags.InCombat) == StatusFlags.InCombat:
                    return GlobalColors.Instance.NPCHostileColor;

                case ObjectKind.BattleNpc:
                    if (!IsHostileMemory((BattleNpc)character))
                    {
                        return GlobalColors.Instance.NPCFriendlyColor;
                    }
                    break;
            }

            return GlobalColors.Instance.NPCNeutralColor;
        }

        public static bool HasTankInvulnerability(BattleChara actor)
        {
            var tankInvulnBuff = actor.StatusList.Where(o => o.StatusId is 810 or 1302 or 409 or 1836);
            return tankInvulnBuff.Any();
        }

        public static GameObject? FindTargetOfTarget(GameObject? target, GameObject? player, ObjectTable actors)
        {
            if (target == null)
            {
                return null;
            }

            if (target.TargetObjectId == 0 && player != null && player.TargetObjectId == 0)
            {
                return player;
            }

            // only the first 200 elements in the array are relevant due to the order in which SE packs data into the array
            // we do a step of 2 because its always an actor followed by its companion
            for (var i = 0; i < 200; i += 2)
            {
                var actor = actors[i];
                if (actor?.ObjectId == target.TargetObjectId)
                {
                    return actor;
                }
            }

            return null;
        }

        public static Vector2 GetAnchoredPosition(Vector2 position, Vector2 size, DrawAnchor anchor)
        {
            switch (anchor)
            {
                case DrawAnchor.Center: return position - size / 2f;
                case DrawAnchor.Left: return position + new Vector2(0, -size.Y / 2f);
                case DrawAnchor.Right: return position + new Vector2(-size.X, -size.Y / 2f);
                case DrawAnchor.Top: return position + new Vector2(-size.X / 2f, 0);
                case DrawAnchor.TopLeft: return position;
                case DrawAnchor.TopRight: return position + new Vector2(-size.X, 0);
                case DrawAnchor.Bottom: return position + new Vector2(-size.X / 2f, -size.Y);
                case DrawAnchor.BottomLeft: return position + new Vector2(0, -size.Y);
                case DrawAnchor.BottomRight: return position + new Vector2(-size.X, -size.Y);
            }

            return position;
        }

        public static string UserFriendlyConfigName(string configTypeName)
        {
            return UserFriendlyString(configTypeName, "Config");
        }

        public static string UserFriendlyString(string str, string? remove)
        {
            var s = remove != null ? str.Replace(remove, "") : str;

            var regex = new Regex(@"
                    (?<=[A-Z])(?=[A-Z][a-z]) |
                    (?<=[^A-Z])(?=[A-Z]) |
                    (?<=[A-Za-z])(?=[^A-Za-z])",
                RegexOptions.IgnorePatternWhitespace);

            return regex.Replace(s, " ");
        }
    }
}
