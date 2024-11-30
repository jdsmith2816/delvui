using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
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
    public class MonkHud : JobHud
    {
        private new MonkConfig Config => (MonkConfig)_config;

        private string[] _chunkTexts = new string[] { "I", "II", "III" };

        public MonkHud(MonkConfig config, string? displayName = null) : base(config, displayName)
        {
        }

        protected override (List<Vector2>, List<Vector2>) ChildrenPositionsAndSizes()
        {
            List<Vector2> positions = new List<Vector2>();
            List<Vector2> sizes = new List<Vector2>();

            if (Config.ChakraBar.Enabled)
            {
                positions.Add(Config.Position + Config.ChakraBar.Position);
                sizes.Add(Config.ChakraBar.Size);
            }

            if (Config.BeastChakraStacksBar.Enabled)
            {
                positions.Add(Config.Position + Config.BeastChakraStacksBar.Position);
                sizes.Add(Config.BeastChakraStacksBar.Size);
            }

            if (Config.MastersGauge.Enabled)
            {
                positions.Add(Config.Position + Config.MastersGauge.Position);
                sizes.Add(Config.MastersGauge.Size);
            }

            if (Config.StancesBar.Enabled)
            {
                positions.Add((Config.Position + Config.StancesBar.Position));
                sizes.Add(Config.StancesBar.Size);
            }

            if (Config.PerfectBalanceBar.Enabled)
            {
                positions.Add(Config.Position + Config.PerfectBalanceBar.Position);
                sizes.Add(Config.PerfectBalanceBar.Size);
            }

            return (positions, sizes);
        }

        public override void DrawJobHud(Vector2 origin, IPlayerCharacter player)
        {
            var position = origin + Config.Position;

            if (Config.ChakraBar.Enabled)
            {
                DrawChakraGauge(position, player);
            }

            if (Config.BeastChakraStacksBar.Enabled)
            {
                DrawBeastChakraStacksBar(position, player);
            }

            if (Config.MastersGauge.Enabled)
            {
                DrawMastersGauge(position, player);
            }

            if (Config.StancesBar.Enabled)
            {
                DrawFormsBar(position, player);
            }

            if (Config.PerfectBalanceBar.Enabled)
            {
                DrawPerfectBalanceBar(position, player);
            }
        }

        private void DrawChakraGauge(Vector2 origin, IPlayerCharacter player)
        {
            MNKGauge gauge = Plugin.JobGauges.Get<MNKGauge>();
            if (Config.ChakraBar.HideWhenInactive && gauge.Chakra == 0)
            {
                return;
            }

            LabelConfig[]? labels = null;
            Status? brotherhood = Utils.StatusListForBattleChara(player).Where(o => o.StatusId is 1185 && o.RemainingTime > 0f).FirstOrDefault();
            float brotherhoodTimer = brotherhood?.RemainingTime ?? 0f;
            bool brotherhoodActive = brotherhoodTimer > 0f;

            if (brotherhoodActive && Config.ChakraBar.BrotherhoodDurationLabel.Enabled)
            {
                Config.ChakraBar.BrotherhoodDurationLabel.SetValue(brotherhoodTimer);
                labels = new LabelConfig[5];
                labels[2] = Config.ChakraBar.BrotherhoodDurationLabel;
            }

            int maxChakra = brotherhoodActive ? 10 : 5;
            int currentChakra = gauge.Chakra;
            int currentBrotherhoodChakra = brotherhoodActive && (currentChakra > 5) ? (currentChakra - 5) : currentChakra;
            bool isFull = currentChakra == maxChakra;

            PluginConfigColor color = brotherhoodActive && (currentChakra > 5) ? Config.ChakraBar.BrotherhoodColor : Config.ChakraBar.FillColor;
            BarGlowConfig? glow = isFull && Config.ChakraBar.GlowConfig.Enabled ? Config.ChakraBar.GlowConfig : null;

            BarHud[] bars = BarUtilities.GetChunkedBars(Config.ChakraBar, 5, brotherhoodActive && (currentChakra > 5) ? currentBrotherhoodChakra : currentChakra, 5,
                labels: labels, fillColor: color, glowConfig: glow);
            foreach (BarHud bar in bars)
            {
                AddDrawActions(bar.GetDrawActions(origin, Config.ChakraBar.StrataLevel));
            }
        }

        private unsafe void DrawBeastChakraStacksBar(Vector2 origin, IPlayerCharacter player)
        {
            MonkBeastChakraStacksBar config = Config.BeastChakraStacksBar;
            MNKGauge gauge = Plugin.JobGauges.Get<MNKGauge>();
            int stacks = gauge.OpoOpoFury + gauge.RaptorFury + gauge.CoeurlFury;

            if (config.HideWhenInactive && stacks == 0)
            {
                return;
            }

            PluginConfigColor empty = PluginConfigColor.Empty;
            Tuple<PluginConfigColor, float, LabelConfig?>[] chunks =
            [
                new(gauge.OpoOpoFury > 0 ? config.OpoopoColor : empty, 1, null),
                new(gauge.RaptorFury > 0 ? config.RaptorColor : empty, 1, null),
                new(gauge.CoeurlFury > 0 ? config.CoeurlColor : empty, 1, null),
                new(gauge.CoeurlFury > 1 ? config.CoeurlColor : empty, 1, null),
            ];

            BarHud[] bars = BarUtilities.GetChunkedBars(config, chunks, player);
            foreach (BarHud bar in bars)
            {
                AddDrawActions(bar.GetDrawActions(origin, config.StrataLevel));
            }
        }

        private unsafe void DrawMastersGauge(Vector2 origin, IPlayerCharacter player)
        {
            MNKGauge gauge = Plugin.JobGauges.Get<MNKGauge>();

            if (Config.MastersGauge.HideWhenInactive &&
                gauge.Nadi == Nadi.NONE &&
                gauge.BeastChakra[0] == BeastChakra.NONE &&
                gauge.BeastChakra[1] == BeastChakra.NONE &&
                gauge.BeastChakra[2] == BeastChakra.NONE)
            {
                return;
            }

            int[] order = Config.MastersGauge.ChakraOrder;
            int[] hasChakra =
            [
                gauge.Nadi.HasFlag(Nadi.LUNAR) ? 1 : 0,
                gauge.BeastChakra[0] != BeastChakra.NONE ? 1 : 0,
                gauge.BeastChakra[0] != BeastChakra.NONE ? 1 : 0,
                gauge.BeastChakra[0] != BeastChakra.NONE ? 1 : 0,
                gauge.Nadi.HasFlag(Nadi.SOLAR) ? 1 : 0,
            ];

            PluginConfigColor[] colors = new[]
            {
                Config.MastersGauge.LunarNadiColor,
                GetChakraColor(gauge.BeastChakra[0]),
                GetChakraColor(gauge.BeastChakra[1]),
                GetChakraColor(gauge.BeastChakra[2]),
                Config.MastersGauge.SolarNadiColor
            };

            var chunks = new Tuple<PluginConfigColor, float, LabelConfig?>[5];
            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i] = new(colors[order[i]], hasChakra[order[i]], i == 2 ? Config.MastersGauge.BlitzTimerLabel : null);
            }

            Config.MastersGauge.BlitzTimerLabel.SetValue(gauge.BlitzTimeRemaining / 1000);

            BarHud[] bars = BarUtilities.GetChunkedBars(Config.MastersGauge, chunks, player);
            foreach (BarHud bar in bars)
            {
                AddDrawActions(bar.GetDrawActions(origin, Config.MastersGauge.StrataLevel));
            }
        }

        private void DrawFormsBar(Vector2 origin, IPlayerCharacter player)
        {
            // formless fist
            Status? formlessFist = Utils.StatusListForBattleChara(player).FirstOrDefault(o => o.StatusId == 2513);
            if (formlessFist != null)
            {
                float remaining = Math.Abs(formlessFist.RemainingTime);

                BarHud bar = BarUtilities.GetProgressBar(
                    Config.StancesBar,
                    null,
                    new LabelConfig[] { Config.StancesBar.FormlessFistLabel },
                    remaining,
                    30f,
                    0,
                    player,
                    Config.StancesBar.FormlessFistColor
                );

                Config.StancesBar.FormlessFistLabel.SetValue(remaining);

                AddDrawActions(bar.GetDrawActions(origin, Config.StancesBar.StrataLevel));
                return;
            }

            // forms
            Status? form = Utils.StatusListForBattleChara(player).FirstOrDefault(o => o.StatusId is 107 or 108 or 109);
            if (Config.StancesBar.HideWhenInactive && form is null)
            {
                return;
            }

            int activeFormIndex = form != null ? (int)form.StatusId - 107 : -1;
            PluginConfigColor[] chunkColors = new PluginConfigColor[]
            {
                Config.StancesBar.OpoOpoColor,
                Config.StancesBar.RaptorColor,
                Config.StancesBar.CoeurlColor
            };

            LabelConfig[] chunkLabels = new LabelConfig[]
            {
                Config.StancesBar.FormLabel.Clone(0),
                Config.StancesBar.FormLabel.Clone(1),
                Config.StancesBar.FormLabel.Clone(2)
            };

            var chunks = new Tuple<PluginConfigColor, float, LabelConfig?>[3];
            for (int i = 0; i < chunks.Length; i++)
            {
                LabelConfig label = chunkLabels[i];
                label.SetText(_chunkTexts[i]);

                chunks[i] = new(chunkColors[i], activeFormIndex == i ? 1 : 0, label);
            }

            BarHud[] bars = BarUtilities.GetChunkedBars(Config.StancesBar, chunks, player);
            foreach (BarHud bar in bars)
            {
                AddDrawActions(bar.GetDrawActions(origin, Config.StancesBar.StrataLevel));
            }
        }

        private void DrawPerfectBalanceBar(Vector2 origin, IPlayerCharacter player)
        {
            Status? perfectBalance = Utils.StatusListForBattleChara(player).Where(o => o.StatusId is 110 && o.RemainingTime > 0f).FirstOrDefault();
            float duration = perfectBalance?.RemainingTime ?? 0f;
            float stacks = perfectBalance?.StackCount ?? 0f;

            if (Config.PerfectBalanceBar.HideWhenInactive && duration <= 0)
            {
                return;
            }

            Tuple<PluginConfigColor, float, LabelConfig?>[] chunks = new Tuple<PluginConfigColor, float, LabelConfig?>[3];
            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i] = new(
                    Config.PerfectBalanceBar.FillColor,
                    i < stacks ? 1f : 0,
                    i == 1 ? Config.PerfectBalanceBar.PerfectBalanceLabel : null
                );
            }

            Config.PerfectBalanceBar.PerfectBalanceLabel.SetValue(duration);

            BarHud[] bars = BarUtilities.GetChunkedBars(Config.PerfectBalanceBar, chunks, player);
            foreach (BarHud bar in bars)
            {
                AddDrawActions(bar.GetDrawActions(origin, Config.PerfectBalanceBar.StrataLevel));
            }
        }

        private PluginConfigColor GetChakraColor(BeastChakra chakra) => chakra switch
        {
            BeastChakra.OPOOPO => Config.MastersGauge.OpoopoChakraColor,
            BeastChakra.RAPTOR => Config.MastersGauge.RaptorChakraColor,
            BeastChakra.COEURL => Config.MastersGauge.CoeurlChakraColor,
            _ => new PluginConfigColor(new(0, 0, 0, 0))
        };
    }

    [Section("Job Specific Bars")]
    [SubSection("Melee", 0)]
    [SubSection("Monk", 1)]
    public class MonkConfig : JobConfig
    {
        [JsonIgnore] public override uint JobId => JobIDs.MNK;

        public new static MonkConfig DefaultConfig()
        {
            var config = new MonkConfig();

            config.StancesBar.Enabled = false;
            config.MastersGauge.BlitzTimerLabel.HideIfZero = true;
            config.ChakraBar.BrotherhoodDurationLabel.HideIfZero = true;
            config.PerfectBalanceBar.PerfectBalanceLabel.HideIfZero = true;

            return config;
        }

        [NestedConfig("Chakra Bar", 35)]
        public ChakraBarConfig ChakraBar = new ChakraBarConfig(
            new(0, -32),
            new(254, 20),
            new(new Vector4(204f / 255f, 115f / 255f, 0f, 100f / 100f))
        );

        [NestedConfig("Fury Stacks Bar", 40)]
        public MonkBeastChakraStacksBar BeastChakraStacksBar = new MonkBeastChakraStacksBar(
            new(0, -32),
            new(254, 20)
        );

        [NestedConfig("Masterful Blitz Bar", 45)]
        public MastersGauge MastersGauge = new MastersGauge(
            new(0, -54),
            new(254, 20)
        );

        [NestedConfig("Forms Bar", 50)]
        public MonkStancesBarConfig StancesBar = new MonkStancesBarConfig(
            new(0, -98),
            new(254, 20)
        );

        [NestedConfig("Perfect Balance Bar", 55)]
        public PerfectBalanceBar PerfectBalanceBar = new PerfectBalanceBar(
            new(0, -76),
            new(254, 20),
            new(new Vector4(150f / 255f, 255f / 255f, 255f / 255f, 100f / 100f))
        );
    }

    [Exportable(false)]
    public class ChakraBarConfig : ChunkedBarConfig
    {
        [ColorEdit4("Brotherhood Fill Color")]
        [Order(65)]
        public PluginConfigColor BrotherhoodColor;

        [NestedConfig("Show Glow" + "##ChakraBar", 60, separator = false, spacing = true)]
        [Order(66)]
        public BarGlowConfig GlowConfig = new();

        [NestedConfig("Brotherhood Duration Text", 67)]
        public NumericLabelConfig BrotherhoodDurationLabel;


        public ChakraBarConfig(Vector2 position, Vector2 size, PluginConfigColor fillColor)
                 : base(position, size, fillColor)
        {
            GlowConfig.Color = new PluginConfigColor(new(255f / 255f, 255f / 255f, 255f / 255f, 100f / 100f));
            BrotherhoodColor = new PluginConfigColor(new(204 / 255f, 3f / 255f, 3f / 255f, 100f / 100f));
            BrotherhoodDurationLabel = new NumericLabelConfig(Vector2.Zero, "", DrawAnchor.Center, DrawAnchor.Center);
        }
    }

    public class PerfectBalanceBar : ChunkedBarConfig
    {
        [NestedConfig("Perfect Balance Duration Text", 50, spacing = true)]
        public NumericLabelConfig PerfectBalanceLabel;

        public PerfectBalanceBar(Vector2 position, Vector2 size, PluginConfigColor fillColor, int padding = 2) : base(position, size, fillColor, padding)
        {
            PerfectBalanceLabel = new NumericLabelConfig(Vector2.Zero, "", DrawAnchor.Center, DrawAnchor.Center);
        }
    }

    [DisableParentSettings("FillColor", "FillDirection")]
    public class MonkBeastChakraStacksBar : ChunkedBarConfig
    {
        [ColorEdit4("Opo-opo Color")]
        [Order(19)]
        public PluginConfigColor OpoopoColor = PluginConfigColor.FromHex(0xFFFFB3D3);

        [ColorEdit4("Raptor Color")]
        [Order(20)]
        public PluginConfigColor RaptorColor = PluginConfigColor.FromHex(0xFFBF89E5);

        [ColorEdit4("Coeurl Color")]
        [Order(21)]
        public PluginConfigColor CoeurlColor = PluginConfigColor.FromHex(0xFF9AE7C0);

        public MonkBeastChakraStacksBar(Vector2 position, Vector2 size)
            : base(position, size, PluginConfigColor.Empty)
        {
        }
    }

    [DisableParentSettings("FillColor", "FillDirection")]
    public class MastersGauge : ChunkedBarConfig
    {
        [ColorEdit4("Lunar Nadi Color")]
        [Order(19)]
        public PluginConfigColor LunarNadiColor = PluginConfigColor.FromHex(0xFFDA87FF);

        [ColorEdit4("Solar Nadi Color")]
        [Order(20)]
        public PluginConfigColor SolarNadiColor = PluginConfigColor.FromHex(0xFFFFFFCA);

        [ColorEdit4("Opo-opo Color")]
        [Order(21)]
        public PluginConfigColor OpoopoChakraColor = PluginConfigColor.FromHex(0xFFC1527E);

        [ColorEdit4("Raptor Color")]
        [Order(22)]
        public PluginConfigColor RaptorChakraColor = PluginConfigColor.FromHex(0xFF8C67BA);

        [ColorEdit4("Coeurl Color")]
        [Order(23)]
        public PluginConfigColor CoeurlChakraColor = PluginConfigColor.FromHex(0xFF326D5A);

        [DragDropHorizontal("Chakra Order", "Lunar Nadi", "Chakra 1", "Chakra 2", "Chakra 3", "Solar Nadi")]
        [Order(24)]
        public int[] ChakraOrder = new int[] { 0, 1, 2, 3, 4 };

        [NestedConfig("Blitz Timer Text", 50, spacing = true)]
        public NumericLabelConfig BlitzTimerLabel;

        public MastersGauge(Vector2 position, Vector2 size)
            : base(position, size, PluginConfigColor.Empty)
        {
            BlitzTimerLabel = new NumericLabelConfig(Vector2.Zero, "", DrawAnchor.Center, DrawAnchor.Center);
        }
    }

    [DisableParentSettings("FillColor")]
    public class MonkStancesBarConfig : ChunkedBarConfig
    {
        [ColorEdit4("Opo-opo Color")]
        [Order(19)]
        public PluginConfigColor OpoOpoColor = PluginConfigColor.FromHex(0xFFFFB3D3);

        [ColorEdit4("Raptor Color")]
        [Order(20)]
        public PluginConfigColor RaptorColor = PluginConfigColor.FromHex(0xFFBF89E5);

        [ColorEdit4("Coeurl Color")]
        [Order(21)]
        public PluginConfigColor CoeurlColor = PluginConfigColor.FromHex(0xFF9AE7C0);

        [ColorEdit4("Formless Fist Color")]
        [Order(22)]
        public PluginConfigColor FormlessFistColor = PluginConfigColor.FromHex(0xFF514793);

        [NestedConfig("Form Number Text", 500, spacing = true)]
        public LabelConfig FormLabel;

        [NestedConfig("Formless Fist Duration Text", 1000, separator = false, spacing = true)]
        public NumericLabelConfig FormlessFistLabel;

        public MonkStancesBarConfig(Vector2 position, Vector2 size)
            : base(position, size, PluginConfigColor.Empty)
        {
            FormLabel = new LabelConfig(Vector2.Zero, "", DrawAnchor.Center, DrawAnchor.Center);

            FormlessFistLabel = new NumericLabelConfig(Vector2.Zero, "", DrawAnchor.Center, DrawAnchor.Center);
            FormlessFistLabel.Enabled = false;
        }
    }
}
