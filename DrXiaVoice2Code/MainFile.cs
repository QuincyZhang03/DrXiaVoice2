using BaseLib.Audio;
using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RunState = MegaCrit.Sts2.Core.Runs.RunState;

namespace DrXiaVoice2.DrXiaVoice2Code
{
    [ModInitializer(nameof(Initialize))]
    public partial class MainFile : Node
    {
        public const string ModId = "DrXiaVoice2"; //At the moment, this is used only for the Logger and harmony names.
        public static readonly Dictionary<string, ModSound> VoiceCache = [];
        //出现过的语音进行缓存，避免重复创建对象造成的内存溢出问题。

        public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

        public static void Initialize()
        {
            ModConfigRegistry.Register(ModId, new DrXiaVoiceConfig());
            Harmony harmony = new(ModId);
            harmony.PatchAll();
        }

        public static void PlayVoice(string voicepath)
        {
            PlayVoice(voicepath, 0);
        }

        public static void PlayVoice(string voicepath, float volumeAdd)
        {
            if (DrXiaVoiceConfig.VoiceEnabled)
            {
                ModSound voice;
                if (!VoiceCache.TryGetValue(voicepath, out voice))
                {
                    voice = new ModSound(voicepath);
                    VoiceCache[voicepath] = voice;
                }
                try
                {
                    ModAudio.PlaySound(voice, DrXiaVoiceConfig.VoiceVolume + volumeAdd);
                }
                catch (Exception e)
                {
                    Logger.Error($"Failed to play voice {voicepath}: {e.Message}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(Hook))]
    [HarmonyPatch("BeforeCardPlayed")]
    public class CardVoicePatch
    {
        static void Prefix(CombatState combatState, CardPlay cardPlay)
        {

            string CardID = cardPlay.Card.Id.Entry.ToLowerInvariant();
            Player CardOwner = cardPlay.Card.Owner;
            if (DrXiaVoiceConfig.AnnounceTeammates || LocalContext.IsMe(CardOwner))
            {
                if (CardID.StartsWith("strike_"))
                {
                    CardID = "strike";
                }
                else if (CardID.StartsWith("defend_"))
                {
                    CardID = "defend";
                }
                else if (CardID.StartsWith("mad_science-"))
                {
                    CardID = "mad_science";
                }
                else if (cardPlay.Card.EnergyCost.Canonical == -1 ||
                    (cardPlay.Card.Type == CardType.Status && CardID != "frantic_escape") ||
                    cardPlay.Card.Type == CardType.Curse)
                {
                    CardID = "GetOut";
                }
                float decrease = LocalContext.IsMe(CardOwner) ? 0 : DrXiaVoiceConfig.TeammatesVolumeDecrease;
                MainFile.PlayVoice($"res://DrXiaVoice2/voices/cards/{CardID}.mp3", decrease);
            }
        }
    }


    [HarmonyPatch(typeof(NEndTurnButton))]
    [HarmonyPatch("OnRelease")]
    public class EndTurnPatch
    {
        static void Prefix()
        {
            MainFile.PlayVoice("res://DrXiaVoice2/voices/others/end_turn.mp3");
        }
    }

    [HarmonyPatch(typeof(Hook))]
    [HarmonyPatch("AfterCombatVictory")]
    public class CombatWinPatch
    {
        static void Prefix(IRunState runState, CombatState? combatState, CombatRoom room)
        {
            MainFile.PlayVoice($"res://DrXiaVoice2/voices/others/little_win.mp3");
        }
    }

    [HarmonyPatch("OnSelected")]
    public class MapPointSelectedPatch
    {
        static void PlayHereSound()
        {
            MainFile.PlayVoice($"res://DrXiaVoice2/voices/others/here.mp3");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NAncientMapPoint))]
        static void AncientPoint()
        {
            PlayHereSound();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NNormalMapPoint))]
        static void NormalPoint()
        {
            PlayHereSound();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NBossMapPoint))]
        static void BossPoint()
        {
            PlayHereSound();
        }
    }

    [HarmonyPatch]
    public class SkipPatch
    {
        static bool SelectedAnyCards;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CardReward))]
        [HarmonyPatch("OnSelect")]
        static void InitializeCardReward()
        {
            SelectedAnyCards = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NCardRewardSelectionScreen))]
        [HarmonyPatch("SelectCard")]
        static void SelectCardMark()
        {
            SelectedAnyCards = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NCardRewardSelectionScreen))]
        [HarmonyPatch("AfterOverlayClosed")]
        static void OverlayClosed()
        {
            if (!SelectedAnyCards) //没选卡就关闭，播放“不要”
            {
                MainFile.PlayVoice($"res://DrXiaVoice2/voices/others/skip.mp3");
            }
        }
    }

    [HarmonyPatch]
    public class GameOverMarkPatch
    {
        static bool RunWin;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NGameOverScreen))]
        [HarmonyPatch("_Ready")]
        static void MarkWinOrLose(RunState ____runState)
        {
            RunWin = ____runState.CurrentRoom?.IsVictoryRoom ?? false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NGameOverContinueButton))]
        [HarmonyPatch("OnPress")]
        static void PlayWinOrLoseSound()
        {
            if (RunWin)
            {
                MainFile.PlayVoice($"res://DrXiaVoice2/voices/others/game_win.mp3");
            }
            else
            {
                MainFile.PlayVoice($"res://DrXiaVoice2/voices/others/lose.mp3");
            }
        }
    }

    public class DrXiaVoiceConfig : SimpleModConfig
    {
        public static bool VoiceEnabled { get; set; } = true;

        [ConfigVisibleIf(nameof(VoiceEnabled))]
        [ConfigSlider(-5, 5, 0.1)]
        public static float VoiceVolume { get; set; } = 0;

        [ConfigVisibleIf(nameof(VoiceEnabled))]
        public static bool AnnounceTeammates { get; set; } = false;


        [ConfigIgnore]
        public static bool ShouldShowTeammatesVolume { get => VoiceEnabled && AnnounceTeammates; }

        [ConfigVisibleIf(nameof(ShouldShowTeammatesVolume))]
        [ConfigSlider(-5, 0, 0.1)]
        public static float TeammatesVolumeDecrease { get; set; } = -2f;

    }
}
