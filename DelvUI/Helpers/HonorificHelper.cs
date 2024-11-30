using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Logging;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json;
using Lumina.Data.Parsing.Uld;
using System;
using System.Linq;
using System.Numerics;

namespace DelvUI.Helpers
{

    public class TitleData
    {
        public string Title = "";
        public bool IsPrefix = false;
        public bool IsOriginal = false;
        public Vector3? Color = new Vector3(0f, 0f, 0f);
        public Vector3? Glow = new Vector3(0f, 0f, 0f);
    }

    internal class HonorificHelper
    {
        private ICallGateSubscriber<int, string>? _getCharacterTitle;

        #region Singleton
        private HonorificHelper()
        {
            _getCharacterTitle = Plugin.PluginInterface.GetIpcSubscriber<int, string>("Honorific.GetCharacterTitle");
        }

        public static void Initialize() { Instance = new HonorificHelper(); }

        public static HonorificHelper Instance { get; private set; } = null!;

        ~HonorificHelper()
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

            Instance = null!;
        }
        #endregion

        public TitleData? GetTitle(IGameObject? actor)
        {
            if (_getCharacterTitle == null ||
                actor == null ||
                actor.ObjectKind != ObjectKind.Player ||
                actor is not ICharacter character)
            {
                return null;
            }

            try
            {
                string jsonData = _getCharacterTitle.InvokeFunc(character.ObjectIndex);
                TitleData? titleData = JsonConvert.DeserializeObject<TitleData>(jsonData ?? string.Empty);
                return titleData;
            }
            catch { }

            return null;
        }
    }
}
