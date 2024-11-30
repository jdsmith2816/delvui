using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using DelvUI.Config;
using DelvUI.Enums;
using DelvUI.Helpers;
using DelvUI.Interface.Bars;
using DelvUI.Interface.GeneralElements;
using DelvUI.Interface.StatusEffects;
using ImGuiNET;
using System.Collections.Generic;
using System.Numerics;
using Action = System.Action;
using Character = Dalamud.Game.ClientState.Objects.Types.ICharacter;
using StructsCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace DelvUI.Interface.Nameplates
{
    public class Nameplate
    {
        protected NameplateConfig _config;

        protected LabelHud _nameLabelHud;
        protected LabelHud _titleLabelHud;

        public Nameplate(NameplateConfig config)
        {
            _config = config;

            _nameLabelHud = new LabelHud(config.NameLabelConfig);
            _titleLabelHud = new LabelHud(config.TitleLabelConfig);
        }

        protected bool IsVisible(IGameObject? actor)
        {
            if (!_config.Enabled ||
                actor == null ||
                !_config.VisibilityConfig.IsElementVisible(null) ||
                (_config.OnlyShowWhenTargeted && actor.Address != Plugin.TargetManager.Target?.Address))
            {
                return false;
            }

            return true;
        }

        public virtual List<(StrataLevel, Action)> GetElementsDrawActions(NameplateData data)
        {
            List<(StrataLevel, Action)> drawActions = new List<(StrataLevel, Action)>();
            if (!IsVisible(data.GameObject)) { return drawActions; }

            drawActions.AddRange(GetMainLabelDrawActions(data));

            return drawActions;
        }

        protected List<(StrataLevel, Action)> GetMainLabelDrawActions(NameplateData data, NameplateAnchor? barAnchor = null)
        {
            List<(StrataLevel, Action)> drawActions = new List<(StrataLevel, Action)>();
            Vector2 origin = _config.Position + (barAnchor?.Position ?? data.ScreenPosition);

            Vector2 swapOffset = Vector2.Zero;
            if (_config.SwapLabelsWhenNeeded && (data.IsTitlePrefix || data.Title.Length == 0))
            {
                swapOffset = _config.TitleLabelConfig.Position - _config.NameLabelConfig.Position;
            }

            // name
            float nameAlpha = _config.RangeConfig.AlphaForDistance(data.Distance, _config.NameLabelConfig.Color.Vector.W);
            var (nameText, namePos, nameSize, nameColor) = _nameLabelHud.PreCalculate(origin + swapOffset, barAnchor?.Size, data.GameObject, data.Name, isPlayerName: data.Kind == ObjectKind.Player);
            drawActions.Add((_config.NameLabelConfig.StrataLevel, () =>
            {
                _nameLabelHud.DrawLabel(nameText, namePos, nameSize, nameColor, nameAlpha);
            }
            ));

            // title others
            float titleAlpha = _config.RangeConfig.AlphaForDistance(data.Distance, _config.TitleLabelConfig.Color.Vector.W);
            var (titleText, titlePos, titleSize, titleColor) = _titleLabelHud.PreCalculate(origin - swapOffset, barAnchor?.Size, data.GameObject, title: data.Title);
            if (data.Title.Length > 0)
            {
                drawActions.Add((_config.TitleLabelConfig.StrataLevel, () =>
                {
                    if (!data.IsTitleOriginal)
                    {
                        Vector4 color = new Vector4(data.HonorificTitleColor.X, data.HonorificTitleColor.Y, data.HonorificTitleColor.Z, 1.0f);
                        Vector4 glow = new Vector4(data.HonorificTitleGlow.X, data.HonorificTitleGlow.Y, data.HonorificTitleGlow.Z, 1.0f);
                        titleColor = new PluginConfigColor(color);

                        _titleLabelHud.DrawTitle(titleText, titlePos, titleSize, titleColor, new PluginConfigColor(glow), titleAlpha);
                    }
                    else
                    {
                        _titleLabelHud.DrawLabel(titleText, titlePos, titleSize, titleColor, titleAlpha);
                    }
                }
                ));
            }

            return drawActions;
        }
    }

    public class NameplateWithBar : Nameplate
    {
        protected NameplateBarConfig BarConfig => ((NameplateWithBarConfig)_config).GetBarConfig();

        private LabelHud _leftLabelHud;
        private LabelHud _rightLabelHud;
        private LabelHud _optionalLabelHud;

        public NameplateWithBar(NameplateConfig config) : base(config)
        {
            _leftLabelHud = new LabelHud(BarConfig.LeftLabelConfig);
            _rightLabelHud = new LabelHud(BarConfig.RightLabelConfig);
            _optionalLabelHud = new LabelHud(BarConfig.OptionalLabelConfig);
        }

        public (bool, bool) GetMouseoverState(NameplateData data)
        {
            if (data.GameObject is not ICharacter character) { return (false, false); }
            if (!BarConfig.IsVisible(character.CurrentHp, character.MaxHp) || BarConfig.DisableInteraction)
            {
                return (false, false);
            }

            bool targeted = Plugin.TargetManager.Target?.Address == character.Address;
            Vector2 barSize = BarConfig.GetSize(targeted);

            Vector2 origin = _config.Position + data.ScreenPosition;
            Vector2 barPos = Utils.GetAnchoredPosition(origin, barSize, BarConfig.Anchor) + BarConfig.Position;
            var (areaStart, areaEnd) = BarConfig.MouseoverAreaConfig.GetArea(barPos, barSize);

            bool isHovering = ImGui.IsMouseHoveringRect(areaStart, areaEnd);
            bool ignoreMouseover = BarConfig.MouseoverAreaConfig.Enabled && BarConfig.MouseoverAreaConfig.Ignore;

            return (isHovering, ignoreMouseover);
        }

        public unsafe List<(StrataLevel, Action)> GetBarDrawActions(NameplateData data)
        {
            List<(StrataLevel, Action)> drawActions = new List<(StrataLevel, Action)>();
            if (!IsVisible(data.GameObject)) { return drawActions; }
            if (data.GameObject is not Character character) { return drawActions; }

            uint currentHp = character.CurrentHp;
            uint maxHp = character.MaxHp;

            if (!BarConfig.IsVisible(currentHp, maxHp)) { return drawActions; }

            // colors
            PluginConfigColor fillColor = GetFillColor(character, currentHp, maxHp);
            fillColor = fillColor.WithAlpha(_config.RangeConfig.AlphaForDistance(data.Distance, fillColor.Vector.W));

            PluginConfigColor bgColor = GetBackgroundColor(character);
            bgColor = bgColor.WithAlpha(_config.RangeConfig.AlphaForDistance(data.Distance, bgColor.Vector.W));

            bool targeted = character.Address == Plugin.TargetManager.Target?.Address;
            PluginConfigColor borderColor = targeted ? BarConfig.TargetedBorderColor : BarConfig.BorderColor;
            borderColor = borderColor.WithAlpha(
                _config.RangeConfig.AlphaForDistance(data.Distance, BarConfig.BorderColor.Vector.W)
            );

            // bar
            Vector2 barSize = BarConfig.GetSize(targeted);
            Rect background = new Rect(BarConfig.Position, barSize, bgColor);
            Rect healthFill = BarUtilities.GetFillRect(BarConfig.Position, barSize, BarConfig.FillDirection, fillColor, currentHp, maxHp);

            BarHud bar = new BarHud(
                BarConfig.ID,
                BarConfig.DrawBorder,
                borderColor,
                targeted ? BarConfig.TargetedBorderThickness : BarConfig.BorderThickness,
                BarConfig.Anchor,
                character,
                current: currentHp,
                max: maxHp,
                shadowConfig: BarConfig.ShadowConfig,
                barTextureName: BarConfig.BarTextureName,
                barTextureDrawMode: BarConfig.BarTextureDrawMode
            );

            bar.NeedsInputs = true;
            bar.SetBackground(background);
            bar.AddForegrounds(healthFill);

            // shield
            PluginConfigColor shieldColor = BarConfig.ShieldConfig.Color.WithAlpha(
                _config.RangeConfig.AlphaForDistance(data.Distance, BarConfig.ShieldConfig.Color.Vector.W)
            );
            BarUtilities.AddShield(bar, BarConfig, BarConfig.ShieldConfig, character, healthFill.Size, shieldColor);

            // draw bar
            Vector2 origin = _config.Position + data.ScreenPosition;
            drawActions.AddRange(bar.GetDrawActions(origin, _config.StrataLevel));

            // mouseover area
            BarHud? mouseoverAreaBar = BarConfig.MouseoverAreaConfig.GetBar(
                BarConfig.Position,
                barSize,
                BarConfig.ID + "_mouseoverArea",
                BarConfig.Anchor
            );

            if (mouseoverAreaBar != null)
            {
                drawActions.AddRange(mouseoverAreaBar.GetDrawActions(origin, StrataLevel.HIGHEST));
            }

            // labels
            Vector2 barPos = Utils.GetAnchoredPosition(origin, barSize, BarConfig.Anchor) + BarConfig.Position;
            LabelHud[] labels = GetLabels(maxHp);
            foreach (LabelHud label in labels)
            {
                LabelConfig labelConfig = (LabelConfig)label.GetConfig();
                float alpha = _config.RangeConfig.AlphaForDistance(data.Distance, labelConfig.Color.Vector.W);
                var (labelText, labelPos, labelSize, labelColor) = label.PreCalculate(barPos, barSize, data.GameObject, data.Name, currentHp, maxHp, data.Kind == ObjectKind.Player);

                drawActions.Add((labelConfig.StrataLevel, () =>
                {
                    label.DrawLabel(labelText, labelPos, labelSize, labelColor, alpha);
                }
                ));
            }

            return drawActions;
        }

        public override List<(StrataLevel, Action)> GetElementsDrawActions(NameplateData data)
        {
            List<(StrataLevel, Action)> drawActions = new List<(StrataLevel, Action)>();
            if (!IsVisible(data.GameObject)) { return drawActions; }

            NameplateAnchor? barAnchor = GetBarAnchor(data);
            drawActions.AddRange(GetMainLabelDrawActions(data, barAnchor));

            return drawActions;
        }

        protected virtual NameplateAnchor? GetBarAnchor(NameplateData data)
        {
            if (data.GameObject is Character chara &&
                BarConfig.IsVisible(chara.CurrentHp, chara.MaxHp))
            {
                bool targeted = Plugin.TargetManager.Target?.Address == data.GameObject.Address;
                Vector2 size = BarConfig.GetSize(targeted);
                Vector2 pos = Utils.GetAnchoredPosition(data.ScreenPosition + BarConfig.Position, size, BarConfig.Anchor);

                return new NameplateAnchor(pos, size);
            }

            return null;
        }

        private LabelHud[] GetLabels(uint maxHp)
        {
            List<LabelHud> labels = new List<LabelHud>();

            if (BarConfig.HideHealthIfPossible && maxHp <= 0)
            {
                if (!Utils.IsHealthLabel(BarConfig.LeftLabelConfig))
                {
                    labels.Add(_leftLabelHud);
                }

                if (!Utils.IsHealthLabel(BarConfig.RightLabelConfig))
                {
                    labels.Add(_rightLabelHud);
                }

                if (!Utils.IsHealthLabel(BarConfig.OptionalLabelConfig))
                {
                    labels.Add(_optionalLabelHud);
                }
            }
            else
            {
                labels.Add(_leftLabelHud);
                labels.Add(_rightLabelHud);
                labels.Add(_optionalLabelHud);
            }

            return labels.ToArray();
        }

        protected virtual PluginConfigColor GetFillColor(Character character, uint currentHp, uint maxHp)
        {
            return ColorUtils.ColorForCharacter(
                character,
                currentHp,
                maxHp,
                false,
                false,
                BarConfig.ColorByHealth
            ) ?? BarConfig.FillColor;
        }

        protected virtual PluginConfigColor GetBackgroundColor(Character character)
        {
            return BarConfig.BackgroundColor;
        }
    }

    public class NameplateWithBarAndExtras : NameplateWithBar
    {
        public NameplateWithBarAndExtras(NameplateConfig config) : base(config)
        {
        }

        public override List<(StrataLevel, Action)> GetElementsDrawActions(NameplateData data)
        {
            List<(StrataLevel, Action)> drawActions = new List<(StrataLevel, Action)>();
            if (!IsVisible(data.GameObject)) { return drawActions; }

            NameplateAnchor? barAnchor = GetBarAnchor(data);
            Vector2 origin = _config.Position + (barAnchor?.Position ?? data.ScreenPosition);

            Vector2 swapOffset = Vector2.Zero;
            if (_config.SwapLabelsWhenNeeded && (data.IsTitlePrefix || data.Title.Length == 0))
            {
                swapOffset = _config.TitleLabelConfig.Position - _config.NameLabelConfig.Position;
            }

            // name
            float nameAlpha = _config.RangeConfig.AlphaForDistance(data.Distance, _config.NameLabelConfig.Color.Vector.W);
            var (nameText, namePos, nameSize, nameColor) = _nameLabelHud.PreCalculate(origin + swapOffset, barAnchor?.Size, data.GameObject, data.Name, isPlayerName: data.Kind == ObjectKind.Player);
            drawActions.Add((_config.NameLabelConfig.StrataLevel, () =>
            {
                _nameLabelHud.DrawLabel(nameText, namePos, nameSize, nameColor, nameAlpha);
            }
            ));

            // title self
            float titleAlpha = _config.RangeConfig.AlphaForDistance(data.Distance, _config.TitleLabelConfig.Color.Vector.W);
            var (titleText, titlePos, titleSize, titleColor) = _titleLabelHud.PreCalculate(origin - swapOffset, barAnchor?.Size, data.GameObject, title: data.Title);
            if (data.Title.Length > 0)
            {
                drawActions.Add((_config.TitleLabelConfig.StrataLevel, () =>
                {
                    if (!data.IsTitleOriginal)
                    {
                        Vector4 color = new Vector4(data.HonorificTitleColor.X, data.HonorificTitleColor.Y, data.HonorificTitleColor.Z, 1.0f);
                        Vector4 glow = new Vector4(data.HonorificTitleGlow.X, data.HonorificTitleGlow.Y, data.HonorificTitleGlow.Z, 1.0f);
                        titleColor = new PluginConfigColor(color);

                        _titleLabelHud.DrawTitle(titleText, titlePos, titleSize, titleColor, new PluginConfigColor(glow), titleAlpha);
                    }
                    else
                    {
                        _titleLabelHud.DrawLabel(titleText, titlePos, titleSize, titleColor, titleAlpha);
                    }
                }
                ));
            }

            // extras anchor
            NameplateExtrasAnchors extrasAnchors = new NameplateExtrasAnchors(
                barAnchor,
                _config.NameLabelConfig.Enabled ? new NameplateAnchor(namePos, nameSize) : null,
                _config.TitleLabelConfig.Enabled && data.Title.Length > 0 ? new NameplateAnchor(titlePos, titleSize) : null
            );

            drawActions.AddRange(GetExtrasDrawActions(data, extrasAnchors));

            return drawActions;
        }

        protected virtual List<(StrataLevel, Action)> GetExtrasDrawActions(NameplateData data, NameplateExtrasAnchors anchors)
        {
            // override
            return new List<(StrataLevel, Action)>();
        }
    }

    public class NameplateWithPlayerBar : NameplateWithBarAndExtras
    {
        private NameplateWithPlayerBarConfig Config => (NameplateWithPlayerBarConfig)_config;

        public NameplateWithPlayerBar(NameplateWithPlayerBarConfig config) : base(config)
        {
        }

        protected override List<(StrataLevel, Action)> GetExtrasDrawActions(NameplateData data, NameplateExtrasAnchors anchors)
        {
            List<(StrataLevel, Action)> drawActions = new List<(StrataLevel, Action)>();
            if (data.GameObject is not IPlayerCharacter character) { return drawActions; }

            float alpha = _config.RangeConfig.AlphaForDistance(data.Distance);

            // role/job icon
            if (Config.RoleIconConfig.Enabled)
            {
                NameplateAnchor? anchor = anchors.GetAnchor(Config.RoleIconConfig.NameplateLabelAnchor, Config.RoleIconConfig.PrioritizeHealthBarAnchor);
                anchor = anchor ?? new NameplateAnchor(data.ScreenPosition, Vector2.Zero);

                uint jobId = character.ClassJob.RowId;
                uint iconId = Config.RoleIconConfig.UseRoleIcons ?
                        JobsHelper.RoleIconIDForJob(jobId, Config.RoleIconConfig.UseSpecificDPSRoleIcons) :
                        JobsHelper.IconIDForJob(jobId, (uint)Config.RoleIconConfig.Style);

                if (iconId > 0)
                {
                    var pos = Utils.GetAnchoredPosition(anchor.Value.Position, -anchor.Value.Size, Config.RoleIconConfig.FrameAnchor);
                    var iconPos = Utils.GetAnchoredPosition(pos + Config.RoleIconConfig.Position, Config.RoleIconConfig.Size, Config.RoleIconConfig.Anchor);

                    drawActions.Add((Config.RoleIconConfig.StrataLevel, () =>
                    {
                        DrawHelper.DrawInWindow(_config.ID + "_jobIcon", iconPos, Config.RoleIconConfig.Size, false, (drawList) =>
                        {
                            DrawHelper.DrawIcon(iconId, iconPos, Config.RoleIconConfig.Size, false, alpha, drawList);
                        });
                    }
                    ));
                }
            }

            // state icon
            if (Config.StateIconConfig.Enabled && 
                data.NamePlateIconId > 0 && 
                Config.StateIconConfig.ShouldDrawIcon(data.NamePlateIconId))
            {
                NameplateAnchor? anchor = anchors.GetAnchor(Config.StateIconConfig.NameplateLabelAnchor, Config.StateIconConfig.PrioritizeHealthBarAnchor);
                anchor = anchor ?? new NameplateAnchor(data.ScreenPosition, Vector2.Zero);

                var pos = Utils.GetAnchoredPosition(anchor.Value.Position, -anchor.Value.Size, Config.StateIconConfig.FrameAnchor);
                var iconPos = Utils.GetAnchoredPosition(pos + Config.StateIconConfig.Position, Config.StateIconConfig.Size, Config.StateIconConfig.Anchor);

                drawActions.Add((Config.StateIconConfig.StrataLevel, () =>
                {
                    DrawHelper.DrawInWindow(_config.ID + "_stateIcon", iconPos, Config.StateIconConfig.Size, false, (drawList) =>
                    {
                        DrawHelper.DrawIcon((uint)data.NamePlateIconId, iconPos, Config.StateIconConfig.Size, false, alpha, drawList);
                    });
                }
                ));
            }

            return drawActions;
        }

        protected override PluginConfigColor GetFillColor(Character character, uint currentHp, uint maxHp)
        {
            NameplatePlayerBarConfig config = (NameplatePlayerBarConfig)BarConfig;

            return ColorUtils.ColorForCharacter(
                character,
                currentHp,
                maxHp,
                config.UseJobColor,
                config.UseRoleColor,
                config.ColorByHealth
            ) ?? config.FillColor;
        }

        protected override PluginConfigColor GetBackgroundColor(Character character)
        {
            NameplatePlayerBarConfig config = (NameplatePlayerBarConfig)BarConfig;

            if (config.UseJobColorAsBackgroundColor)
            {
                return GlobalColors.Instance.SafeColorForJobId(character.ClassJob.RowId);
            }
            else if (config.UseRoleColorAsBackgroundColor)
            {
                return GlobalColors.Instance.SafeRoleColorForJobId(character.ClassJob.RowId);
            }

            return config.BackgroundColor;
        }
    }

    public class NameplateWithEnemyBar : NameplateWithBarAndExtras
    {
        private NameplateWithEnemyBarConfig Config => (NameplateWithEnemyBarConfig)_config;

        private LabelHud _orderLabelHud;
        private StatusEffectsListHud _debuffsHud;
        private NameplateCastbarHud _castbarHud;

        public NameplateWithEnemyBar(NameplateWithEnemyBarConfig config) : base(config)
        {
            _orderLabelHud = new LabelHud(config.BarConfig.OrderLabelConfig);
            _debuffsHud = new StatusEffectsListHud(config.DebuffsConfig);
            _castbarHud = new NameplateCastbarHud(config.CastbarConfig);
        }

        public void StopPreview()
        {
            _debuffsHud.StopPreview();
            _castbarHud.StopPreview();
        }

        protected override List<(StrataLevel, Action)> GetExtrasDrawActions(NameplateData data, NameplateExtrasAnchors anchors)
        {
            List<(StrataLevel, Action)> drawActions = new List<(StrataLevel, Action)>();
            if (data.GameObject is not Character character) { return drawActions; }

            NameplateEnemyBarConfig barConfig = Config.BarConfig;
            NameplateAnchor? anchor = barConfig.IsVisible(character.CurrentHp, character.MaxHp) ? anchors.BarAnchor : anchors.NameLabelAnchor;
            anchor = anchor ?? new NameplateAnchor(_config.Position + data.ScreenPosition, Vector2.Zero);

            // order label
            float alpha = _config.RangeConfig.AlphaForDistance(data.Distance, barConfig.OrderLabelConfig.Color.Vector.W);

            barConfig.OrderLabelConfig.SetText(data.Order);
            var (labelText, labelPos, labelSize, labelColor) = _orderLabelHud.PreCalculate(anchor.Value.Position, anchor.Value.Size, data.GameObject);
            drawActions.Add((barConfig.OrderLabelConfig.StrataLevel, () =>
            {
                _orderLabelHud.DrawLabel(labelText, labelPos, labelSize, labelColor, alpha);
            }
            ));

            // debuffs
            Vector2 buffsPos = Utils.GetAnchoredPosition(anchor.Value.Position, -anchor.Value.Size, Config.DebuffsConfig.HealthBarAnchor);
            drawActions.Add((Config.DebuffsConfig.StrataLevel, () =>
            {
                _debuffsHud.Actor = character;
                _debuffsHud.PrepareForDraw(buffsPos);
                _debuffsHud.Draw(buffsPos);
            }
            ));

            // castbar
            Vector2 castbarPos = Utils.GetAnchoredPosition(anchor.Value.Position, -anchor.Value.Size, Config.CastbarConfig.HealthBarAnchor);
            drawActions.Add((Config.CastbarConfig.StrataLevel, () =>
            {
                _castbarHud.ParentSize = anchor.Value.Size;
                _castbarHud.Actor = character;
                _castbarHud.PrepareForDraw(castbarPos);
                _castbarHud.Draw(castbarPos);
            }
            ));

            // icon
            if (Config.IconConfig.Enabled && data.NamePlateIconId > 0)
            {
                anchor = anchors.GetAnchor(Config.IconConfig.NameplateLabelAnchor, Config.IconConfig.PrioritizeHealthBarAnchor);
                anchor = anchor ?? new NameplateAnchor(data.ScreenPosition, Vector2.Zero);

                var pos = Utils.GetAnchoredPosition(_config.Position + anchor.Value.Position, -anchor.Value.Size, Config.IconConfig.FrameAnchor);
                var iconPos = Utils.GetAnchoredPosition(pos + Config.IconConfig.Position, Config.IconConfig.Size, Config.IconConfig.Anchor);

                drawActions.Add((Config.IconConfig.StrataLevel, () =>
                {
                    DrawHelper.DrawInWindow(_config.ID + "_enemyIcon", iconPos, Config.IconConfig.Size, false, (drawList) =>
                    {
                        DrawHelper.DrawIcon((uint)data.NamePlateIconId, iconPos, Config.IconConfig.Size, false, alpha, drawList);
                    });
                }
                ));

            }

            return drawActions;
        }

        protected override unsafe PluginConfigColor GetFillColor(Character character, uint currentHp, uint maxHp)
        {
            NameplateEnemyBarConfig config = (NameplateEnemyBarConfig)BarConfig;

            bool targetingPlayer = character.TargetObjectId == Plugin.ClientState.LocalPlayer?.GameObjectId;
            if (targetingPlayer && config.UseCustomColorWhenBeingTargeted)
            {
                return config.CustomColorWhenBeingTargeted;
            }

            if (config.UseStateColor)
            {
                StructsCharacter* chara = (StructsCharacter*)character.Address;

                bool inCombat = (character.StatusFlags & StatusFlags.InCombat) != 0;
                if (inCombat && !config.ColorByHealth.Enabled)
                {
                    return config.InCombatColor;
                }
                else if (!inCombat)
                {
                    return (character.StatusFlags & StatusFlags.Hostile) != 0 ? config.OutOfCombatHostileColor : config.OutOfCombatColor;
                }
            }

            return base.GetFillColor(character, currentHp, maxHp);
        }
    }

    #region utils
    public struct NameplateAnchor
    {
        public Vector2 Position;
        public Vector2 Size;

        internal NameplateAnchor(Vector2 position, Vector2 size)
        {
            Position = position;
            Size = size;
        }
    }

    public struct NameplateExtrasAnchors
    {
        public NameplateAnchor? BarAnchor;
        public NameplateAnchor? NameLabelAnchor;
        public NameplateAnchor? TitleLabelAnchor;
        public NameplateAnchor? HighestLabelAnchor;
        public NameplateAnchor? LowestLabelAnchor;
        private NameplateAnchor? DefaultLabelAnchor;

        internal NameplateExtrasAnchors(NameplateAnchor? barAnchor, NameplateAnchor? nameLabelAnchor, NameplateAnchor? titleLabelAnchor)
        {
            BarAnchor = barAnchor;
            NameLabelAnchor = nameLabelAnchor;
            TitleLabelAnchor = titleLabelAnchor;
            DefaultLabelAnchor = nameLabelAnchor;

            float nameY = -1;
            if (nameLabelAnchor.HasValue)
            {
                nameY = nameLabelAnchor.Value.Position.Y;
            }

            float titleY = -1;
            if (titleLabelAnchor.HasValue)
            {
                titleY = titleLabelAnchor.Value.Position.Y;
            }

            if (nameY == -1)
            {
                DefaultLabelAnchor = titleLabelAnchor;
            }
            else if (nameY < titleY)
            {
                HighestLabelAnchor = nameLabelAnchor;
                LowestLabelAnchor = titleLabelAnchor;
            }
            else if (nameY > titleY)
            {
                HighestLabelAnchor = titleLabelAnchor;
                LowestLabelAnchor = nameLabelAnchor;
            }
        }

        internal NameplateAnchor? GetAnchor(NameplateLabelAnchor label, bool prioritizeHealthBar)
        {
            if (prioritizeHealthBar && BarAnchor != null) { return BarAnchor; }

            NameplateAnchor? labelAnchor = null;

            switch (label)
            {
                case NameplateLabelAnchor.Name: labelAnchor = NameLabelAnchor; break;
                case NameplateLabelAnchor.Title: labelAnchor = TitleLabelAnchor; break;
                case NameplateLabelAnchor.Highest: labelAnchor = HighestLabelAnchor; break;
                case NameplateLabelAnchor.Lowest: labelAnchor = LowestLabelAnchor; break;
            }

            return labelAnchor ?? DefaultLabelAnchor;
        }
        #endregion
    }
}
