﻿using Dalamud.Game.ClientState.Objects.Types;
using DelvUI.Config;
using DelvUI.Enums;
using DelvUI.Helpers;
using DelvUI.Interface.Bars;
using DelvUI.Interface.GeneralElements;
using DelvUI.Interface.StatusEffects;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DelvUI.Interface.EnemyList
{
    public class EnemyListHud : DraggableHudElement, IHudElementWithMouseOver, IHudElementWithPreview
    {
        private EnemyListConfig Config => (EnemyListConfig)_config;
        private EnemyListConfigs Configs;

        private EnemyListHelper _helper = new EnemyListHelper();

        private List<SmoothHPHelper> _smoothHPHelpers = new List<SmoothHPHelper>();

        private const int MaxEnemyCount = 8;
        private List<float> _previewValues = new List<float>(MaxEnemyCount);

        private bool _wasHovering = false;

        private LabelHud _nameLabelHud;
        private LabelHud _healthLabelHud;
        private LabelHud _orderLabelHud;
        private CastbarHud _castbarHud;
        private StatusEffectsListHud _buffsListHud;
        private StatusEffectsListHud _debuffsListHud;

        public EnemyListHud(EnemyListConfig config, string displayName) : base(config, displayName)
        {
            Configs = EnemyListConfigs.GetConfigs();

            config.ValueChangeEvent += OnConfigPropertyChanged;

            _nameLabelHud = new LabelHud(Configs.HealthBar.NameLabel);
            _healthLabelHud = new LabelHud(Configs.HealthBar.HealthLabel);
            _orderLabelHud = new LabelHud(Configs.HealthBar.OrderLetterLabel);

            _castbarHud = new CastbarHud(Configs.CastBar);
            _buffsListHud = new StatusEffectsListHud(Configs.Buffs);
            _debuffsListHud = new StatusEffectsListHud(Configs.Debuffs);

            for (int i = 0; i < MaxEnemyCount; i++)
            {
                _smoothHPHelpers.Add(new SmoothHPHelper());
            }
        }

        protected override void InternalDispose()
        {
            _config.ValueChangeEvent -= OnConfigPropertyChanged;
        }

        private void OnConfigPropertyChanged(object sender, OnChangeBaseArgs args)
        {
            if (args.PropertyName == "Preview")
            {
                _previewValues.Clear();

                if (Config.Preview)
                {
                    Random RNG = new Random((int)ImGui.GetTime());

                    for (int i = 0; i < MaxEnemyCount; i++)
                    {
                        _previewValues.Add(RNG.Next(0, 101) / 100f);
                    }
                }
            }
        }

        protected override (List<Vector2>, List<Vector2>) ChildrenPositionsAndSizes()
        {
            Vector2 size = new Vector2(Configs.HealthBar.Size.X, MaxEnemyCount * Configs.HealthBar.Size.Y + (MaxEnemyCount - 1) * Config.VerticalPadding);
            return (new List<Vector2>() { Config.Position + size / 2f }, new List<Vector2>() { size });
        }

        public void StopPreview()
        {
            Config.Preview = false;
            _castbarHud.StopPreview();
            _buffsListHud.StopPreview();
            _debuffsListHud.StopPreview();
        }

        public void StopMouseover()
        {
            if (_wasHovering)
            {
                InputsHelper.Instance.ClearTarget();
                _wasHovering = false;
            }
        }

        public override void DrawChildren(Vector2 origin)
        {
            if (!Config.Enabled) { return; }

            _helper.Update();

            int count = Math.Min(MaxEnemyCount, Config.Preview ? MaxEnemyCount : _helper.EnemyCount);
            uint fakeMaxHp = 100000;

            Character? mouseoverTarget = null;
            bool hovered = false;

            for (int i = 0; i < count; i++)
            {
                // hp bar
                Character? character = Config.Preview ? null : Plugin.ObjectTable.SearchById((uint)_helper.EnemiesData.ElementAt(i).ObjectId) as Character;

                uint currentHp = Config.Preview ? (uint)(_previewValues[i] * fakeMaxHp) : character?.CurrentHp ?? fakeMaxHp;
                uint maxHp = Config.Preview ? fakeMaxHp : character?.MaxHp ?? fakeMaxHp;
                int enmityLevel = Config.Preview ? Math.Max(4, i + 1) : _helper.EnemiesData.ElementAt(i).EnmityLevel;

                if (Configs.HealthBar.SmoothHealthConfig.Enabled)
                {
                    currentHp = _smoothHPHelpers[i].GetNextHp((int)currentHp, (int)maxHp, Configs.HealthBar.SmoothHealthConfig.Velocity);
                }

                Vector2 pos = new Vector2(Config.Position.X, Config.Position.Y + i * Configs.HealthBar.Size.Y + i * Config.VerticalPadding);
                Rect background = new Rect(pos, Configs.HealthBar.Size, Configs.HealthBar.BackgroundColor);

                PluginConfigColor fillColor = GetColor(character, currentHp, maxHp);
                PluginConfigColor borderColor = GetBorderColor(character, enmityLevel);
                Rect healthFill = BarUtilities.GetFillRect(pos, Configs.HealthBar.Size, Configs.HealthBar.FillDirection, fillColor, currentHp, maxHp);

                BarHud bar = new BarHud(
                    Configs.HealthBar.ID + $"_{i}",
                    Configs.HealthBar.DrawBorder,
                    borderColor,
                    Configs.HealthBar.BorderThickness,
                    DrawAnchor.TopLeft,
                    current: currentHp,
                    max: maxHp
                );

                bar.SetBackground(background);
                bar.AddForegrounds(healthFill);

                // highlight
                bool isHovering = ImGui.IsMouseHoveringRect(origin + pos, origin + pos + Configs.HealthBar.Size);
                if (isHovering && Configs.HealthBar.Colors.ShowHighlight)
                {
                    Rect highlight = new Rect(pos, Configs.HealthBar.Size, Configs.HealthBar.Colors.HighlightColor);
                    bar.AddForegrounds(highlight);

                    mouseoverTarget = character;
                    hovered = true;
                }

                bar.Draw(origin);

                // labels
                string? name = Config.Preview ? "Fake Name" : null;
                _nameLabelHud.Draw(origin + pos, Configs.HealthBar.Size, character, name, currentHp, maxHp);
                _healthLabelHud.Draw(origin + pos, Configs.HealthBar.Size, character, name, currentHp, maxHp);

                string letter = Config.Preview || _helper.EnemiesData.ElementAt(i).Letter == null ? ((char)(i + 65)).ToString() : _helper.EnemiesData.ElementAt(i).Letter!;
                Configs.HealthBar.OrderLetterLabel.SetText($"[{letter}]");
                _orderLabelHud.Draw(origin + pos, Configs.HealthBar.Size);

                // buffs / debuffs
                var buffsPos = Utils.GetAnchoredPosition(origin + pos, -Configs.HealthBar.Size, Configs.Buffs.HealthBarAnchor);
                _buffsListHud.Actor = character;
                _buffsListHud.Draw(buffsPos);

                var debuffsPos = Utils.GetAnchoredPosition(origin + pos, -Configs.HealthBar.Size, Configs.Debuffs.HealthBarAnchor);
                _debuffsListHud.Actor = character;
                _debuffsListHud.Draw(debuffsPos);

                // castbar
                var castbarPos = Utils.GetAnchoredPosition(origin + pos, -Configs.HealthBar.Size, Configs.CastBar.HealthBarAnchor);
                _castbarHud.Actor = character;
                _castbarHud.Draw(castbarPos);
            }

            // mouseover
            if (hovered && mouseoverTarget != null)
            {
                InputsHelper.Instance.SetTarget(mouseoverTarget);
                _wasHovering = true;

                // left click
                if (InputsHelper.Instance.LeftButtonClicked)
                {
                    Plugin.TargetManager.SetTarget(mouseoverTarget);
                }
            }
            else if (_wasHovering)
            {
                InputsHelper.Instance.ClearTarget();
                _wasHovering = false;
            }
        }

        private PluginConfigColor GetColor(Character? character, uint currentHp = 0, uint maxHp = 0)
        {
            if (Configs.HealthBar.Colors.ColorByHealth.Enabled && character != null)
            {
                var scale = (float)currentHp / Math.Max(1, maxHp);
                return Utils.GetColorByScale(scale, Configs.HealthBar.Colors.ColorByHealth);
            }

            return Configs.HealthBar.FillColor;
        }

        private PluginConfigColor GetBorderColor(Character? character, int enmityLevel)
        {
            GameObject? target = Plugin.TargetManager.Target ?? Plugin.TargetManager.SoftTarget;
            if (character != null && character == target)
            {
                return Configs.HealthBar.Colors.TargetBordercolor;
            }

            return enmityLevel switch
            {
                >= 3 => Configs.HealthBar.Colors.EnmityLeaderBorderColor,
                >= 1 => Configs.HealthBar.Colors.EnmitySecondBorderColor,
                _ => Configs.HealthBar.BorderColor
            };
        }
    }

    #region utils
    public struct EnemyListConfigs
    {
        public EnemyListHealthBarConfig HealthBar;
        public EnemyListCastbarConfig CastBar;
        public EnemyListBuffsConfig Buffs;
        public EnemyListDebuffsConfig Debuffs;

        public EnemyListConfigs(
            EnemyListHealthBarConfig healthBar,
            EnemyListCastbarConfig castBar,
            EnemyListBuffsConfig buffs,
            EnemyListDebuffsConfig debuffs)
        {
            HealthBar = healthBar;
            CastBar = castBar;
            Buffs = buffs;
            Debuffs = debuffs;
        }

        public static EnemyListConfigs GetConfigs()
        {
            return new EnemyListConfigs(
                ConfigurationManager.Instance.GetConfigObject<EnemyListHealthBarConfig>(),
                ConfigurationManager.Instance.GetConfigObject<EnemyListCastbarConfig>(),
                ConfigurationManager.Instance.GetConfigObject<EnemyListBuffsConfig>(),
                ConfigurationManager.Instance.GetConfigObject<EnemyListDebuffsConfig>()
            );
        }
    }
    #endregion
}
