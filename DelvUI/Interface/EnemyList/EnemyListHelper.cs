using Dalamud.Memory;
using DelvUI.Helpers;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using StructsFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace DelvUI.Interface.EnemyList
{
    public unsafe class EnemyListHelper
    {
        private RaptureAtkModule* _raptureAtkModule = null;
        private const int EnemyListInfoIndex = 21;
        private const int EnemyListNamesIndex = 19;

        private List<EnemyListData> _enemiesData = new List<EnemyListData>();
        public IReadOnlyCollection<EnemyListData> EnemiesData => _enemiesData.AsReadOnly();
        public int EnemyCount => _enemiesData.Count;
        public bool IsCurrentTarget(ulong objectId)
            {
                var target = Plugin.TargetManager.Target;
                return target != null && target.GameObjectId == objectId;
            }

        private List<EnemyListData> GeneratePreviewData()
        {
            List<EnemyListData> previewData = new List<EnemyListData>();
            for (int i = 0; i < 8; i++)
            {
                previewData.Add(new EnemyListData((ulong)i, i, Math.Min(4, i + 1)));
            }
            return previewData;
        }

        public void Update(bool hideCurrentTarget, bool preview)
        {
            if (preview)
            {
                _enemiesData = GeneratePreviewData();
                return;
            }

            UIModule* uiModule = StructsFramework.Instance()->GetUIModule();
            if (uiModule != null)
            {
                _raptureAtkModule = uiModule->GetRaptureAtkModule();
            }

            _enemiesData.Clear();

            if (_raptureAtkModule == null || _raptureAtkModule->AtkModule.AtkArrayDataHolder.NumberArrayCount <= EnemyListInfoIndex)
            {
                return;
            }

            var numberArrayData = _raptureAtkModule->AtkModule.AtkArrayDataHolder.NumberArrays[EnemyListInfoIndex];
            if (numberArrayData->AtkArrayData.Size < 2) { return; }

            int enemyCount = numberArrayData->IntArray[1];
            for (int i = 0; i < enemyCount; i++)
            {
                int index = 8 + (i * 6);
                if (numberArrayData->AtkArrayData.Size <= index) { break; }

                ulong objectId = (ulong)numberArrayData->IntArray[index];
                if (hideCurrentTarget && IsCurrentTarget(objectId)) { continue; }
                
                int? letter = GetEnemyLetter(objectId, i);
                int enmityLevel = GetEnmityLevelForIndex(i);
                _enemiesData.Add(new EnemyListData(objectId, letter, enmityLevel));
            }
        }

        private int? GetEnemyLetter(ulong objectId, int index)
        {
            if (_raptureAtkModule == null || _raptureAtkModule->AtkModule.AtkArrayDataHolder.StringArrayCount <= EnemyListNamesIndex)
            {
                return null;
            }

            var stringArrayData = _raptureAtkModule->AtkModule.AtkArrayDataHolder.StringArrays[EnemyListNamesIndex];

            int i = index * 2;
            if (stringArrayData->AtkArrayData.Size <= i)
            {
                return null;
            }

            string name = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(stringArrayData->StringArray[i])).ToString();
            if (name.Length == 0)
            {
                return null;
            }

            bool isMarked = Utils.SignIconIDForObjectID((uint)objectId) != null;
            char letterSymbol = isMarked && name.Length > 1 ? name[2] : name[0];
            return letterSymbol - 57457;
        }

        private int GetEnmityLevelForIndex(int index)
        {
            // gets enmity level by checking texture in enemy list addon

            AtkUnitBase* enemyList = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("_EnemyList", 1);
            if (enemyList == null || enemyList->RootNode == null) { return 0; }

            int id = index == 0 ? 2 : 20000 + index; // makes no sense but it is what it is (blame SE)
            AtkResNode* node = enemyList->GetNodeById((uint)id);
            if (node == null || node->GetComponent() == null) { return 0; }

            AtkImageNode* imageNode = (AtkImageNode*)node->GetComponent()->UldManager.SearchNodeById(13);
            if (imageNode == null) { return 0; }

            return Math.Min(4, imageNode->PartId + 1);
        }
    }

    public struct EnemyListData
    {
        public ulong ObjectId;
        public int? LetterIndex;
        public int EnmityLevel;

        public EnemyListData(ulong objectId, int? letterIndex, int enmityLevel)
        {
            ObjectId = objectId;
            LetterIndex = letterIndex;
            EnmityLevel = enmityLevel;
        }
    }
}
