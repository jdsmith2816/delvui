﻿using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using DelvUI.Config;
using DelvUI.Config.Attributes;
using DelvUI.Enums;
using DelvUI.Helpers;
using DelvUI.Interface.Bars;
using DelvUI.Interface.GeneralElements;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DelvUI.Interface.Jobs
{
    public class NinjaHud : JobHud
    {
        private new NinjaConfig Config => (NinjaConfig)_config;

        public NinjaHud(string id, NinjaConfig config, string? displayName = null) : base(id, config, displayName) { }

        protected override (List<Vector2>, List<Vector2>) ChildrenPositionsAndSizes()
        {
            List<Vector2> positions = new List<Vector2>();
            List<Vector2> sizes = new List<Vector2>();

            if (Config.HutonBar.Enabled)
            {
                positions.Add(Config.Position + Config.HutonBar.Position);
                sizes.Add(Config.HutonBar.Size);
            }

            if (Config.NinkiBar.Enabled)
            {
                positions.Add(Config.Position + Config.NinkiBar.Position);
                sizes.Add(Config.NinkiBar.Size);
            }

            if (Config.TrickAttackBar.Enabled)
            {
                positions.Add(Config.Position + Config.TrickAttackBar.Position);
                sizes.Add(Config.TrickAttackBar.Size);
            }

            if (Config.SuitonBar.Enabled)
            {
                positions.Add(Config.Position + Config.SuitonBar.Position);
                sizes.Add(Config.SuitonBar.Size);
            }

            if (Config.MudraBar.Enabled)
            {
                positions.Add(Config.Position + Config.MudraBar.Position);
                sizes.Add(Config.MudraBar.Size);
            }

            return (positions, sizes);
        }

        public override void DrawJobHud(Vector2 origin, PlayerCharacter player)
        {
            var pos = origin + Config.Position;
            if (Config.MudraBar.Enabled)
            {
                DrawMudraBars(pos, player);
            }

            if (Config.HutonBar.Enabled)
            {
                DrawHutonGauge(pos, player);
            }

            if (Config.NinkiBar.Enabled)
            {
                DrawNinkiGauge(pos, player);
            }

            if (Config.TrickAttackBar.Enabled)
            {
                DrawTrickAttackBar(pos, player);
            }

            if (Config.SuitonBar.Enabled)
            {
                DrawSuitonBar(pos, player);
            }
        }

        public (bool, bool, bool) GetMudraBuffs(PlayerCharacter? player, out Status? ninjutsuBuff, out Status? kassatsuBuff, out Status? tcjBuff)
        {
            ninjutsuBuff = null;
            kassatsuBuff = null;
            tcjBuff = null;

            if (player is not null)
            {
                foreach (Status status in player.StatusList)
                {
                    if (status.StatusId == 496) { ninjutsuBuff = status; }
                    if (status.StatusId == 497) { kassatsuBuff = status; }
                    if (status.StatusId == 1186) { tcjBuff = status; }
                }
            }

            return (ninjutsuBuff is not null, kassatsuBuff is not null, tcjBuff is not null);
        }

        private void DrawMudraBars(Vector2 pos, PlayerCharacter player)
        {
            var (hasNinjutsuBuff, hasKassatsuBuff, hasTCJBuff) = GetMudraBuffs(player, out Status? ninjutsuBuff, out Status? kassatsuBuff, out Status? tcjBuff);

            int mudraStacks = SpellHelper.Instance.GetStackCount(2, 2259);
            float mudraCooldown = SpellHelper.Instance.GetSpellCooldown(2259);

            float current = 0f;
            float max = 0f;

            // For some reason, the mudras may be on cooldown before the "Mudra" buff is applied.
            // Mudra stack count is set to -2 when a mudra is in the middle of its re-cast timer, so we can check for that instead.
            bool inNinjutsu = mudraStacks == -2 || hasNinjutsuBuff;

            if (hasTCJBuff || hasKassatsuBuff || inNinjutsu)
            {
                if (hasTCJBuff)
                {
                    max = 6f;
                    current = tcjBuff is null || tcjBuff.RemainingTime < 0 ? max : tcjBuff.RemainingTime;
                    Config.MudraBar.Label.SetText(GenerateNinjutsuText(tcjBuff?.StackCount ?? 0, hasKassatsuBuff, hasTCJBuff));
                }
                else if (hasKassatsuBuff)
                {
                    max = 15f;
                    current = kassatsuBuff is null || kassatsuBuff.RemainingTime < 0 ? max : kassatsuBuff.RemainingTime;
                    Config.MudraBar.Label.SetText("KASSATSU");
                }

                if (inNinjutsu)
                {
                    max = 6f;
                    current = ninjutsuBuff is null || ninjutsuBuff.RemainingTime < 0 ? max : ninjutsuBuff.RemainingTime;
                    Config.MudraBar.Label.SetText(GenerateNinjutsuText(ninjutsuBuff?.StackCount ?? 0, hasKassatsuBuff, hasTCJBuff));
                }

                PluginConfigColor fillColor = hasTCJBuff ? Config.MudraBar.TCJBarColor : hasKassatsuBuff ? Config.MudraBar.KassatsuBarColor : Config.MudraBar.FillColor;
                Rect foreground = BarUtilities.GetFillRect(Config.MudraBar.Position, Config.MudraBar.Size, Config.MudraBar.FillDirection, fillColor, current, max);

                BarHud bar = new BarHud(Config.MudraBar, player).AddForegrounds(foreground).AddLabels(Config.MudraBar.Label);
                bar.Draw(pos);
            }
            else
            {
                max = 40f;
                current = max - mudraCooldown;

                if (!Config.MudraBar.HideWhenInactive || current < max)
                {
                    Config.MudraBar.Label.SetText(Math.Truncate((max - current) % 20).ToString());
                    BarUtilities.GetChunkedProgressBars(Config.MudraBar, 2, current, max, 0f, player, Config.MudraBar.Label).Draw(pos);
                }
            }
        }

        private void DrawHutonGauge(Vector2 pos, PlayerCharacter player)
        {
            NINGauge gauge = Plugin.JobGauges.Get<NINGauge>();
            float hutonDurationLeft = gauge.HutonTimer / 1000f;
            if (!Config.HutonBar.HideWhenInactive || hutonDurationLeft > 0)
            {
                Config.HutonBar.Label.SetText(Math.Truncate(hutonDurationLeft).ToString());
                BarUtilities.GetProgressBar(Config.HutonBar, hutonDurationLeft, 70f, 0f, player).Draw(pos);
            }
        }


        private void DrawNinkiGauge(Vector2 pos, PlayerCharacter player)
        {
            NINGauge gauge = Plugin.JobGauges.Get<NINGauge>();
            if (!Config.NinkiBar.HideWhenInactive || gauge.Ninki > 0)
            {
                Config.NinkiBar.Label.SetText(gauge.Ninki.ToString("N0"));
                BarUtilities.GetProgressBar(Config.NinkiBar, gauge.Ninki, 100f, 0f, player).Draw(pos);
            }
        }

        private void DrawTrickAttackBar(Vector2 pos, PlayerCharacter player)
        {
            GameObject? actor = Plugin.TargetManager.SoftTarget ?? Plugin.TargetManager.Target;
            float trickDuration = 0f;

            if (actor is BattleChara target)
            {
                trickDuration = target.StatusList.FirstOrDefault(o => o.StatusId is 638 && o.SourceID == player.ObjectId && o.RemainingTime > 0)?.RemainingTime ?? 0f;
            }

            if (!Config.TrickAttackBar.HideWhenInactive || trickDuration > 0)
            {
                Config.TrickAttackBar.Label.SetText(Math.Truncate(trickDuration).ToString());
                BarUtilities.GetProgressBar(Config.TrickAttackBar, trickDuration, 15f, 0f, player).Draw(pos);
            }
        }

        private void DrawSuitonBar(Vector2 pos, PlayerCharacter player)
        {
            float suitonDuration = player.StatusList.FirstOrDefault(o => o.StatusId == 507 && o.RemainingTime > 0)?.RemainingTime ?? 0f;

            if (!Config.SuitonBar.HideWhenInactive || suitonDuration > 0)
            {
                Config.SuitonBar.Label.SetText(Math.Truncate(suitonDuration).ToString("N0"));
                BarUtilities.GetProgressBar(Config.SuitonBar, suitonDuration, 20f, 0f, player).Draw(pos);
            }
        }

        private string GenerateNinjutsuText(byte param, bool haveKassatsuBuff, bool haveTCJBuff)
        {
            return param switch
            {
                1 or 2 or 3 => "FUMA SHURIKEN",
                6 or 7 => haveKassatsuBuff ? "GOKA MEKKYAKU" : "KATON",
                9 or 11 => "RAITON",
                13 or 14 => haveKassatsuBuff ? "HYOSHO RANRYU" : "HYOTON",
                27 or 30 => "HUTON",
                39 or 45 => "DOTON",
                54 or 57 => "SUITON",
                _ => haveTCJBuff ? "TEN CHI JIN" : "",
            };
        }
    }

    [Section("Job Specific Bars")]
    [SubSection("Melee", 0)]
    [SubSection("Ninja", 1)]
    public class NinjaConfig : JobConfig
    {
        [JsonIgnore] public override uint JobId => JobIDs.NIN;

        public NinjaConfig()
        {
            TrickAttackBar.Enabled = false;
            SuitonBar.Enabled = false;
            HutonBar.ThresholdConfig.Enabled = true;
        }

        public new static NinjaConfig DefaultConfig() { return new NinjaConfig(); }

        [NestedConfig("Mudra Bar", 30)]
        public MudraBarConfig MudraBar = new MudraBarConfig(
            new(0, -50),
            new(254, 10),
            new PluginConfigColor(new Vector4(211f / 255f, 166f / 255f, 75f / 242f, 100f / 100f))
        );

        [NestedConfig("Huton Bar", 35)]
        public ProgressBarConfig HutonBar = new ProgressBarConfig(
            new(0, -10),
            new(254, 20),
            new PluginConfigColor(new Vector4(110f / 255f, 197f / 255f, 207f / 255f, 100f / 100f)),
            BarDirection.Right,
            new PluginConfigColor(new Vector4(230f / 255f, 33f / 255f, 33f / 255f, 53f / 100f)),
            40f
        );

        [NestedConfig("Ninki Bar", 40)]
        public ProgressBarConfig NinkiBar = new ProgressBarConfig(
            new(0, -32),
            new(254, 20),
            new PluginConfigColor(new Vector4(137f / 255f, 82f / 255f, 236f / 255f, 100f / 100f))
        );

        [NestedConfig("Trick Attack Bar", 45)]
        public ProgressBarConfig TrickAttackBar = new ProgressBarConfig(
            new(0, -63),
            new(254, 10),
            new PluginConfigColor(new Vector4(191f / 255f, 40f / 255f, 0f / 255f, 100f / 100f))
        );

        [NestedConfig("Suiton Bar", 50)]
        public ProgressBarConfig SuitonBar = new ProgressBarConfig(
            new(0, -75),
            new(254, 10),
            new PluginConfigColor(new Vector4(202f / 255f, 228f / 255f, 246f / 242f, 100f / 100f))
        );
    }

    public class MudraBarConfig : ChunkedProgressBarConfig
    {
        [ColorEdit4("Kassatsu Color", spacing = true)]
        [Order(60)]
        public PluginConfigColor KassatsuBarColor = new(new Vector4(239 / 255f, 123 / 255f, 222 / 242f, 100f / 100f));

        [ColorEdit4("Ten Chi Jin Color")]
        [Order(65)]
        public PluginConfigColor TCJBarColor = new(new Vector4(181 / 255f, 33 / 255f, 41 / 242f, 100f / 100f));

        [NestedConfig("Bar Text", 1000, separator = false, spacing = true)]
        public LabelConfig Label;

        public MudraBarConfig(Vector2 position, Vector2 size, PluginConfigColor fillColor) : base(position, size, fillColor, 2)
        {
            Label = new LabelConfig(Vector2.Zero, "", DrawAnchor.Center, DrawAnchor.Center);
            UsePartialFillColor = true;
        }
    }
}
