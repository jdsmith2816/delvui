using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures.TextureWraps;
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
        private List<EnemyListCastbarHud> _castbarHud;
        private StatusEffectsListHud _buffsListHud;
        private StatusEffectsListHud _debuffsListHud;

        private IDalamudTextureWrap? _iconsTexture => TexturesHelper.GetTextureFromPath("ui/uld/enemylist_hr1.tex");

        public EnemyListHud(EnemyListConfig config, string displayName) : base(config, displayName)
        {
            Configs = EnemyListConfigs.GetConfigs();

            config.ValueChangeEvent += OnConfigPropertyChanged;

            _nameLabelHud = new LabelHud(Configs.HealthBar.NameLabel);
            _healthLabelHud = new LabelHud(Configs.HealthBar.HealthLabel);
            _orderLabelHud = new LabelHud(Configs.HealthBar.OrderLabel);

            _castbarHud = new List<EnemyListCastbarHud>();
            _buffsListHud = new StatusEffectsListHud(Configs.Buffs);
            _debuffsListHud = new StatusEffectsListHud(Configs.Debuffs);

            for (int i = 0; i < MaxEnemyCount; i++)
            {
                _smoothHPHelpers.Add(new SmoothHPHelper());
                _castbarHud.Add(new EnemyListCastbarHud(Configs.CastBar));
            }

            UpdatePreview();
        }

        protected override void InternalDispose()
        {
            _config.ValueChangeEvent -= OnConfigPropertyChanged;
        }

        private void OnConfigPropertyChanged(object sender, OnChangeBaseArgs args)
        {
            if (args.PropertyName == "Preview")
            {
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            _previewValues.Clear();
            if (!Config.Preview) { return; }

            Random RNG = new Random((int)ImGui.GetTime());

            for (int i = 0; i < MaxEnemyCount; i++)
            {
                _previewValues.Add(RNG.Next(0, 101) / 100f);
            }
        }

        protected override (List<Vector2>, List<Vector2>) ChildrenPositionsAndSizes()
        {
            Vector2 size = new Vector2(Configs.HealthBar.Size.X, MaxEnemyCount * Configs.HealthBar.Size.Y + (MaxEnemyCount - 1) * Config.VerticalPadding);
            Vector2 pos = Config.GrowthDirection == EnemyListGrowthDirection.Down ? Config.Position : Config.Position - new Vector2(0, size.Y);

            return (new List<Vector2>() { pos + size / 2f }, new List<Vector2>() { size });
        }

        public void StopPreview()
        {
            Config.Preview = false;

            foreach (EnemyListCastbarHud castbar in _castbarHud)
            {
                castbar.StopPreview();
            }

            _buffsListHud.StopPreview();
            _debuffsListHud.StopPreview();
            Configs.HealthBar.MouseoverAreaConfig.Preview = false;
            Configs.SignIcon.Preview = false;
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

            _helper.Update(Config.HideCurrentTarget, Config.Preview);

            int count = Math.Min(MaxEnemyCount, Config.Preview ? MaxEnemyCount : _helper.EnemyCount);
            uint fakeMaxHp = 100000;

            ICharacter? mouseoverTarget = null;
            bool hovered = false;
            
            for (int i = 0, j = 0; i < count && j < _helper.EnemiesData.Count; i++, j++)
            {
                // hp bar
                ICharacter? character = Config.Preview ? null : Plugin.ObjectTable.SearchById(_helper.EnemiesData.ElementAt(j).ObjectId) as ICharacter;

                if (character == null && !Config.Preview) { i--; continue; }

                uint currentHp = Config.Preview ? (uint)(_previewValues[i] * fakeMaxHp) : character?.CurrentHp ?? fakeMaxHp;
                uint maxHp = Config.Preview ? fakeMaxHp : character?.MaxHp ?? fakeMaxHp;
                int enmityLevel = Config.Preview ? Math.Max(4, i + 1) : _helper.EnemiesData.ElementAt(j).EnmityLevel;

                if (Configs.HealthBar.SmoothHealthConfig.Enabled)
                {
                    currentHp = _smoothHPHelpers[i].GetNextHp((int)currentHp, (int)maxHp, Configs.HealthBar.SmoothHealthConfig.Velocity);
                }

                int direction = Config.GrowthDirection == EnemyListGrowthDirection.Down ? 1 : -1;
                float y = Config.Position.Y + i * direction * Configs.HealthBar.Size.Y + i * direction * Config.VerticalPadding;
                Vector2 pos = new Vector2(Config.Position.X, y);

                PluginConfigColor fillColor = GetColor(character, currentHp, maxHp);
                PluginConfigColor bgColor = Configs.HealthBar.BackgroundColor;
                if (Configs.HealthBar.RangeConfig.Enabled)
                {
                    fillColor = GetDistanceColor(character, fillColor);
                    bgColor = GetDistanceColor(character, bgColor);
                }
                Rect background = new Rect(pos, Configs.HealthBar.Size, bgColor);

                PluginConfigColor borderColor = GetBorderColor(character, enmityLevel);
                Rect healthFill = BarUtilities.GetFillRect(pos, Configs.HealthBar.Size, Configs.HealthBar.FillDirection, fillColor, currentHp, maxHp);

                BarHud bar = new BarHud(
                    Configs.HealthBar.ID + $"_{i}",
                    Configs.HealthBar.DrawBorder,
                    borderColor,
                    GetBorderThickness(character),
                    DrawAnchor.TopLeft,
                    current: currentHp,
                    max: maxHp,
                    shadowConfig: Configs.HealthBar.ShadowConfig,
                    barTextureName: Configs.HealthBar.BarTextureName,
                    barTextureDrawMode: Configs.HealthBar.BarTextureDrawMode
                );

                bar.NeedsInputs = true;
                bar.SetBackground(background);
                bar.AddForegrounds(healthFill);

                if (Configs.HealthBar.Colors.UseMissingHealthBar)
                {
                    Vector2 healthMissingSize = Configs.HealthBar.Size - BarUtilities.GetFillDirectionOffset(healthFill.Size, Configs.HealthBar.FillDirection);
                    Vector2 healthMissingPos = Configs.HealthBar.FillDirection.IsInverted() ? pos : pos + BarUtilities.GetFillDirectionOffset(healthFill.Size, Configs.HealthBar.FillDirection);
                    PluginConfigColor? color = Configs.HealthBar.RangeConfig.Enabled ? GetDistanceColor(character, Configs.HealthBar.Colors.HealthMissingColor) : Configs.HealthBar.Colors.HealthMissingColor;
                    bar.AddForegrounds(new Rect(healthMissingPos, healthMissingSize, color));
                }

                // highlight
                var (areaStart, areaEnd) = Configs.HealthBar.MouseoverAreaConfig.GetArea(origin + pos, Configs.HealthBar.Size);
                bool isHovering = character != null && ImGui.IsMouseHoveringRect(areaStart, areaEnd);
                bool isSoftTarget = character != null && character.EntityId == Plugin.TargetManager.SoftTarget?.EntityId;
                if (isHovering || isSoftTarget)
                {
                    if (Configs.HealthBar.Colors.ShowHighlight)
                    {
                        Rect highlight = new Rect(pos, Configs.HealthBar.Size, Configs.HealthBar.Colors.HighlightColor);
                        bar.AddForegrounds(highlight);
                    }

                    mouseoverTarget = character;
                    hovered = isHovering;
                }

                AddDrawActions(bar.GetDrawActions(origin, Configs.HealthBar.StrataLevel));

                // mouseover area
                BarHud? mouseoverAreaBar = Configs.HealthBar.MouseoverAreaConfig.GetBar(
                    pos,
                    Configs.HealthBar.Size,
                    Configs.HealthBar.ID + "_mouseoverArea"
                );

                if (mouseoverAreaBar != null)
                {
                    AddDrawActions(mouseoverAreaBar.GetDrawActions(origin, StrataLevel.HIGHEST));
                }

                // enmity icon
                if (_iconsTexture != null && Configs.EnmityIcon.Enabled)
                {
                    var parentPos = Utils.GetAnchoredPosition(origin + pos, -Configs.HealthBar.Size, Configs.EnmityIcon.HealthBarAnchor);
                    var iconPos = Utils.GetAnchoredPosition(parentPos + Configs.EnmityIcon.Position, Configs.EnmityIcon.Size, Configs.EnmityIcon.Anchor);
                    int enmityIndex = Config.Preview ? Math.Min(3, j) : _helper.EnemiesData.ElementAt(j).EnmityLevel - 1;

                    AddDrawAction(Configs.EnmityIcon.StrataLevel, () =>
                    {
                        DrawHelper.DrawInWindow(ID + "_enmityIcon", iconPos, Configs.EnmityIcon.Size, false, (drawList) =>
                        {
                            float w = 48f / _iconsTexture.Width;
                            float h = 48f / _iconsTexture.Height;
                            Vector2 uv0 = new Vector2(w * enmityIndex, 0.48f);
                            Vector2 uv1 = new Vector2(w * (enmityIndex + 1), 0.48f + h);
                            drawList.AddImage(_iconsTexture.ImGuiHandle, iconPos, iconPos + Configs.EnmityIcon.Size, uv0, uv1);
                        });
                    });
                }

                // sign icon
                uint? signIconId = null;
                if (Configs.SignIcon.Enabled)
                {
                    signIconId = Configs.SignIcon.IconID(character);
                    if (signIconId.HasValue)
                    {
                        var parentPos = Utils.GetAnchoredPosition(origin + pos, -Configs.HealthBar.Size, Configs.SignIcon.HealthBarAnchor);
                        var iconPos = Utils.GetAnchoredPosition(parentPos + Configs.SignIcon.Position, Configs.SignIcon.Size, Configs.SignIcon.Anchor);

                        AddDrawAction(Configs.SignIcon.StrataLevel, () =>
                        {
                            DrawHelper.DrawInWindow(ID + "_signIcon", iconPos, Configs.SignIcon.Size, false, (drawList) =>
                            {
                                DrawHelper.DrawIcon(signIconId.Value, iconPos, Configs.SignIcon.Size, false, drawList);
                            });
                        });
                    }
                }

                // labels
                string? name = Config.Preview ? "Fake Name" : null;
                AddDrawAction(Configs.HealthBar.NameLabel.StrataLevel, () =>
                {
                    _nameLabelHud.Draw(origin + pos, Configs.HealthBar.Size, character, name, currentHp, maxHp);
                });

                AddDrawAction(Configs.HealthBar.HealthLabel.StrataLevel, () =>
                {
                    _healthLabelHud.Draw(origin + pos, Configs.HealthBar.Size, character, name, currentHp, maxHp);
                });

                if (!signIconId.HasValue || !Configs.SignIcon.ReplaceOrderLabel)
                {
                    int letter = i;
                    if (!Config.Preview && _helper.EnemiesData.ElementAt(i).LetterIndex.HasValue)
                    {
                        letter = _helper.EnemiesData.ElementAt(i).LetterIndex!.Value;
                    }

                    string str = char.ConvertFromUtf32(0xE071 + letter).ToString();
                    AddDrawAction(Configs.HealthBar.OrderLabel.StrataLevel, () =>
                    {
                        Configs.HealthBar.OrderLabel.SetText(str);
                        _orderLabelHud.Draw(origin + pos, Configs.HealthBar.Size);
                    });
                }

                // buffs / debuffs
                var buffsPos = Utils.GetAnchoredPosition(origin + pos, -Configs.HealthBar.Size, Configs.Buffs.HealthBarAnchor);
                AddDrawAction(Configs.Buffs.StrataLevel, () =>
                {
                    _buffsListHud.Actor = character;
                    _buffsListHud.PrepareForDraw(buffsPos);
                    _buffsListHud.Draw(buffsPos);
                });

                var debuffsPos = Utils.GetAnchoredPosition(origin + pos, -Configs.HealthBar.Size, Configs.Debuffs.HealthBarAnchor);
                AddDrawAction(Configs.Debuffs.StrataLevel, () =>
                {
                    _debuffsListHud.Actor = character;
                    _debuffsListHud.PrepareForDraw(debuffsPos);
                    _debuffsListHud.Draw(debuffsPos);
                });

                // castbar
                EnemyListCastbarHud castbar = _castbarHud[j];
                castbar.EnemyListIndex = j;

                var castbarPos = Utils.GetAnchoredPosition(origin + pos, -Configs.HealthBar.Size, Configs.CastBar.HealthBarAnchor);
                AddDrawAction(Configs.CastBar.StrataLevel, () =>
                {
                    castbar.Actor = character;
                    castbar.PrepareForDraw(castbarPos);
                    castbar.Draw(castbarPos);
                });
            }

            // mouseover
            bool ignoreMouseover = Configs.HealthBar.MouseoverAreaConfig.Enabled && Configs.HealthBar.MouseoverAreaConfig.Ignore;
            if (hovered && mouseoverTarget != null)
            {
                _wasHovering = true;
                InputsHelper.Instance.SetTarget(mouseoverTarget, ignoreMouseover);

                // left click
                if (InputsHelper.Instance.LeftButtonClicked)
                {
                    Plugin.TargetManager.Target = mouseoverTarget;
                }
            }
            else if (_wasHovering)
            {
                InputsHelper.Instance.ClearTarget();
                _wasHovering = false;
            }
        }

        private PluginConfigColor GetColor(ICharacter? character, uint currentHp = 0, uint maxHp = 0)
        {
            if (Configs.HealthBar.Colors.ColorByHealth.Enabled && (character != null || Config.Preview))
            {
                var scale = (float)currentHp / Math.Max(1, maxHp);
                return ColorUtils.GetColorByScale(scale, Configs.HealthBar.Colors.ColorByHealth);
            }

            return Configs.HealthBar.FillColor;
        }

        private PluginConfigColor GetBorderColor(ICharacter? character, int enmityLevel)
        {
            IGameObject? target = Plugin.TargetManager.SoftTarget ?? Plugin.TargetManager.Target;
            if (character != null && target != null && character.EntityId == target.EntityId)
            {
                return Configs.HealthBar.Colors.TargetBordercolor;
            }

            if (!Configs.HealthBar.Colors.ShowEnmityBorderColors)
            {
                return Configs.HealthBar.BorderColor;
            }

            return enmityLevel switch
            {
                >= 3 => Configs.HealthBar.Colors.EnmityLeaderBorderColor,
                >= 1 => Configs.HealthBar.Colors.EnmitySecondBorderColor,
                _ => Configs.HealthBar.BorderColor
            };
        }

        private int GetBorderThickness(ICharacter? character)
        {
            IGameObject? target = Plugin.TargetManager.Target;
            if (character != null && character == target)
            {
                return Configs.HealthBar.Colors.TargetBorderThickness;
            }

            return Configs.HealthBar.BorderThickness;
        }

        private PluginConfigColor GetDistanceColor(ICharacter? character, PluginConfigColor color)
        {
            byte distance = character != null ? character.YalmDistanceX : byte.MaxValue;
            float currentAlpha = color.Vector.W * 100f;
            float alpha = Configs.HealthBar.RangeConfig.AlphaForDistance(distance, currentAlpha) / 100f;

            return color.WithAlpha(alpha);
        }
    }

    #region utils
    public struct EnemyListConfigs
    {
        public EnemyListHealthBarConfig HealthBar;
        public EnemyListEnmityIconConfig EnmityIcon;
        public EnemyListSignIconConfig SignIcon;
        public EnemyListCastbarConfig CastBar;
        public EnemyListBuffsConfig Buffs;
        public EnemyListDebuffsConfig Debuffs;

        public EnemyListConfigs(
            EnemyListHealthBarConfig healthBar,
            EnemyListEnmityIconConfig enmityIcon,
            EnemyListSignIconConfig signIcon,
            EnemyListCastbarConfig castBar,
            EnemyListBuffsConfig buffs,
            EnemyListDebuffsConfig debuffs)
        {
            HealthBar = healthBar;
            EnmityIcon = enmityIcon;
            SignIcon = signIcon;
            CastBar = castBar;
            Buffs = buffs;
            Debuffs = debuffs;
        }

        public static EnemyListConfigs GetConfigs()
        {
            return new EnemyListConfigs(
                ConfigurationManager.Instance.GetConfigObject<EnemyListHealthBarConfig>(),
                ConfigurationManager.Instance.GetConfigObject<EnemyListEnmityIconConfig>(),
                ConfigurationManager.Instance.GetConfigObject<EnemyListSignIconConfig>(),
                ConfigurationManager.Instance.GetConfigObject<EnemyListCastbarConfig>(),
                ConfigurationManager.Instance.GetConfigObject<EnemyListBuffsConfig>(),
                ConfigurationManager.Instance.GetConfigObject<EnemyListDebuffsConfig>()
            );
        }
    }
    #endregion
}
