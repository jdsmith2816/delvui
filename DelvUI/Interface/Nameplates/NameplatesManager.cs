using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Memory;
using DelvUI.Config;
using DelvUI.Helpers;
using DelvUI.Interface.GeneralElements;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonNamePlate;
using static FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkModule;
using static FFXIVClientStructs.FFXIV.Client.UI.UI3DModule;
using StructsFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using StructsGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace DelvUI.Interface.Nameplates
{
    internal class NameplatesManager : IDisposable
    {
        #region Singleton
        public static NameplatesManager Instance { get; private set; } = null!;
        private NameplatesGeneralConfig _config = null!;

        private NameplatesManager()
        {
            Plugin.ClientState.TerritoryChanged -= ClientStateOnTerritoryChangedEvent;
            ConfigurationManager.Instance.ResetEvent += OnConfigReset;

            OnConfigReset(ConfigurationManager.Instance);
        }

        public static void Initialize()
        {
            Instance = new NameplatesManager();
        }

        ~NameplatesManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            Plugin.ClientState.TerritoryChanged -= ClientStateOnTerritoryChangedEvent;

            Instance = null!;
        }

        private void OnConfigReset(ConfigurationManager sender)
        {
            _config = sender.GetConfigObject<NameplatesGeneralConfig>();
        }
        #endregion Singleton

        private const int NameplateCount = 50;
        private const int NameplateDataArrayIndex = 4;
        private Vector2 _averageNameplateSize = new Vector2(250, 150);

        private List<NameplateData> _data = new List<NameplateData>();
        public IReadOnlyCollection<NameplateData> Data => _data.AsReadOnly();

        private NameplatesCache _cache = new NameplatesCache(50);

        private void ClientStateOnTerritoryChangedEvent(ushort territoryId)
        {
            _cache.Clear();
        }

        public unsafe void Update()
        {
            if (!_config.Enabled) { return; }

            UIModule* uiModule = StructsFramework.Instance()->GetUIModule();
            if (uiModule == null) { return; }

            UI3DModule* ui3DModule = uiModule->GetUI3DModule();
            if (ui3DModule == null) { return; }

            AddonNamePlate* addon = (AddonNamePlate*)Plugin.GameGui.GetAddonByName("NamePlate", 1);
            if (addon == null) { return; }

            RaptureAtkModule* atkModule = uiModule->GetRaptureAtkModule();
            if (atkModule == null || atkModule->AtkModule.AtkArrayDataHolder.StringArrayCount <= NameplateDataArrayIndex) { return; }

            StringArrayData* stringArray = atkModule->AtkModule.AtkArrayDataHolder.StringArrays[NameplateDataArrayIndex];
            Span<NamePlateInfo> infoArray = atkModule->NamePlateInfoEntries;
            Camera camera = Control.Instance()->CameraManager.Camera->CameraBase.SceneCamera;

            IGameObject? target = Plugin.TargetManager.Target;
            bool foundTarget = false;
            NameplateData? targetData = null;

            _data = new List<NameplateData>();
            int activeCount = ui3DModule->NamePlateObjectInfoCount;

            for (int i = 0; i < activeCount; i++)
            {
                try
                {
                    ObjectInfo* objectInfo = ui3DModule->NamePlateObjectInfoPointers[i];
                    if (objectInfo == null || objectInfo->NamePlateIndex >= NameplateCount) { continue; }

                    // actor
                    StructsGameObject* obj = objectInfo->GameObject;
                    if (obj == null) { continue; }

                    bool isTarget = false;
                    IGameObject? gameObject = Plugin.ObjectTable.CreateObjectReference(new IntPtr(obj));
                    if (target != null && new IntPtr(obj) == target.Address)
                    {
                        isTarget = true;
                        foundTarget = true;
                    }

                    // ui nameplate
                    NamePlateObject nameplateObject = addon->NamePlateObjectArray[objectInfo->NamePlateIndex];

                    // position
                    Vector2 screenPos = new Vector2(
                        nameplateObject.RootComponentNode->AtkResNode.X + nameplateObject.RootComponentNode->AtkResNode.Width / 2f,
                        nameplateObject.RootComponentNode->AtkResNode.Y + nameplateObject.RootComponentNode->AtkResNode.Height
                    );
                    screenPos = ClampScreenPosition(screenPos);

                    Vector3 worldPos = new Vector3(obj->Position.X, obj->Position.Y + obj->Height * 2.2f, obj->Position.Z);

                    // distance
                    float distance = Vector3.Distance(camera.Object.Position, worldPos);

                    // name
                    NamePlateInfo info = infoArray[objectInfo->NamePlateIndex];
                    string name = info.Name.ToString();

                    // title
                    string title = info.Title.ToString();
                    bool isTitlePrefix = info.IsPrefixTitle;
                    bool IsTitleOriginal = true;
                    Vector3 HonorificTitleColor = new Vector3(0f, 0f, 0f);
                    Vector3 HonorificTitleGlow = new Vector3(0f, 0f, 0f);

                    // Get the title from Honorific, if it exists
                    TitleData? customTitleData = HonorificHelper.Instance?.GetTitle(gameObject);
                    if (customTitleData != null && !customTitleData.IsOriginal)
                    {
                        title = customTitleData.Title;
                        isTitlePrefix = customTitleData.IsPrefix;
                        IsTitleOriginal = customTitleData.IsOriginal;
                        if (customTitleData.Color != null) { HonorificTitleColor = (Vector3)customTitleData.Color; }
                        if (customTitleData.Glow != null) { HonorificTitleGlow = (Vector3)customTitleData.Glow; }
                    }

                    // state icon
                    int iconId = 0;
                    AtkUldAsset* textureInfo = nameplateObject.NameIcon->PartsList->Parts[nameplateObject.NameIcon->PartId].UldAsset;
                    if (textureInfo != null && textureInfo->AtkTexture.Resource != null)
                    {
                        iconId = (int)textureInfo->AtkTexture.Resource->IconId;
                    }

                    // order
                    int arrayIndex = 200 + (activeCount - nameplateObject.Priority - 1);
                    string order = "";
                    try
                    {
                        if (stringArray->AtkArrayData.Size > arrayIndex && stringArray->StringArray[arrayIndex] != null)
                        {
                            order = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(stringArray->StringArray[arrayIndex])).ToString();
                        }
                    }
                    catch { }

                    NameplateData data = new NameplateData(
                        gameObject,
                        name,
                        title,
                        isTitlePrefix,
                        IsTitleOriginal,
                        HonorificTitleColor,
                        HonorificTitleGlow,
                        iconId,
                        order,
                        (ObjectKind)obj->ObjectKind,
                        obj->SubKind,
                        screenPos,
                        worldPos,
                        distance
                    );

                    if (isTarget)
                    {
                        targetData = data;
                    }
                    else
                    {
                        _data.Add(data);
                    }

                    _cache.Add(obj->GetGameObjectId().ObjectId, data);
                }
                catch { }
            }

            _data.Reverse();

            // add target nameplate last
            if (foundTarget && targetData.HasValue)
            {
                _data.Add(targetData.Value);
            }
            // create nameplate for target?
            else if (_config.AlwaysShowTargetNameplate && target != null && !foundTarget)
            {
                StructsGameObject* obj = (StructsGameObject*)target.Address;
                NameplateData? cachedData = _cache[(uint)target.GameObjectId];

                Vector3 worldPos = new Vector3(target.Position.X, target.Position.Y + obj->Height * 2.2f, target.Position.Z);
                float distance = Vector3.Distance(camera.Object.Position, worldPos);

                Plugin.GameGui.WorldToScreen(worldPos, out Vector2 screenPos);
                screenPos = ClampScreenPosition(screenPos);

                targetData = new NameplateData(
                    target,
                    target.Name.ToString(),
                    cachedData?.Title ?? "",
                    cachedData?.IsTitlePrefix ?? true,
                    cachedData?.IsTitleOriginal ?? true,
                    cachedData?.HonorificTitleColor ?? new Vector3(0f, 0f, 0f),
                    cachedData?.HonorificTitleGlow ?? new Vector3(0f, 0f, 0f),
                    cachedData?.NamePlateIconId ?? 0,
                    cachedData?.Order ?? "",
                    target.ObjectKind,
                    target.SubKind,
                    screenPos,
                    worldPos,
                    distance,
                    true
                );

                _data.Add(targetData.Value);
            }
        }

        private Vector2 ClampScreenPosition(Vector2 pos)
        {
            if (!_config.ClampToScreen) { return pos; }

            Vector2 screenSize = ImGui.GetMainViewport().Size;
            Vector2 nameplateSize = _averageNameplateSize / 2f;
            float margin = 20;

            if (pos.X + nameplateSize.X > screenSize.X)
            {
                pos.X = screenSize.X - nameplateSize.X - margin;
            }
            else if (pos.X - nameplateSize.X < 0)
            {
                pos.X = nameplateSize.X + margin;
            }

            if (pos.Y + nameplateSize.Y > screenSize.Y)
            {
                pos.Y = screenSize.Y - nameplateSize.Y - margin;
            }
            else if (pos.Y - nameplateSize.Y < 0)
            {
                pos.Y = nameplateSize.Y + margin;
            }

            return pos;
        }
    }

    #region utils
    public class NameplatesCache
    {

        private int _limit;
        private Dictionary<uint, NameplateData> _dict;
        private Queue<uint> _queue;

        public NameplatesCache(int limit)
        {
            _limit = limit;
            _dict = new Dictionary<uint, NameplateData>(limit);
            _queue = new Queue<uint>(limit);
        }

        public void Add(uint key, NameplateData data)
        {
            if (key == 0 || key == 0xE0000000) { return; }

            if (_dict.Count == _limit)
            {
                uint oldestKey = _queue.Dequeue();
                _dict.Remove(oldestKey);
            }

            if (_dict.ContainsKey(key))
            {
                _dict[key] = data;
            }
            else
            {
                _dict.Add(key, data);
                _queue.Enqueue(key);
            }
        }

        public void Clear()
        {
            _dict.Clear();
            _queue.Clear();
        }

        public NameplateData? this[uint key]
        {
            get
            {
                if (_dict.TryGetValue(key, out NameplateData data))
                {
                    return data;
                }

                return null;
            }
        }
    }

    public struct NameplateData
    {
        public IGameObject? GameObject;
        public string Name;
        public string Title;
        public bool IsTitlePrefix;
        public bool IsTitleOriginal;
        public Vector3 HonorificTitleColor;
        public Vector3 HonorificTitleGlow;
        public int NamePlateIconId;
        public string Order;
        public ObjectKind Kind;
        public byte SubKind;
        public Vector2 ScreenPosition;
        public Vector3 WorldPosition;
        public float Distance;
        public bool IgnoreOcclusion;

        public NameplateData(IGameObject? gameObject, string name, string title, bool isTitlePrefix, bool isTitleOriginal, Vector3 honorificTitleColor, Vector3 honorificTitleGlow, int namePlateIconId, string order, ObjectKind kind, byte subKind, Vector2 screenPosition, Vector3 worldPosition, float distance, bool ignoreOcclusion = false)
        {
            GameObject = gameObject;
            Name = name;
            Title = title;
            IsTitlePrefix = isTitlePrefix;
            IsTitleOriginal = isTitleOriginal;
            HonorificTitleColor = honorificTitleColor;
            HonorificTitleGlow = honorificTitleGlow;
            NamePlateIconId = namePlateIconId;
            Order = order;
            Kind = kind;
            SubKind = subKind;
            ScreenPosition = screenPosition;
            WorldPosition = worldPosition;
            Distance = distance;
            IgnoreOcclusion = ignoreOcclusion;
        }
    }
    #endregion
}
