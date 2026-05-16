using BaseLib.Audio;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
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

        public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

        public static void Initialize()
        {
            Harmony harmony = new(ModId);
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Hook))]
    [HarmonyPatch("BeforeCardPlayed")]
    public class VoicePatch
    {
        static void Prefix(CombatState combatState, CardPlay cardPlay)
        {
            string CardID = cardPlay.Card.Id.Entry.ToLowerInvariant();
            ModAudio.PlaySound(new($"res://DrXiaVoice2/voices/cards/{CardID}.mp3"));
        }
    }


    [HarmonyPatch(typeof(NEndTurnButton))]
    [HarmonyPatch("OnRelease")]
    public class EndTurnPatch
    {
        static void Prefix()
        {
            ModAudio.PlaySound(new("res://DrXiaVoice2/voices/others/end_turn.mp3"));
        }
    }

    [HarmonyPatch(typeof(Hook))]
    [HarmonyPatch("AfterCombatVictory")]
    public class CombatWinPatch
    {
        static void Prefix(IRunState runState, CombatState? combatState, CombatRoom room)
        {
            ModAudio.PlaySound(new($"res://DrXiaVoice2/voices/others/little_win.mp3"));
        }
    }

    [HarmonyPatch("OnSelected")]
    public class MapPointSelectedPatch
    {
        static void PlayHereSound()
        {
            ModAudio.PlaySound(new($"res://DrXiaVoice2/voices/others/here.mp3"));
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
                ModAudio.PlaySound(new($"res://DrXiaVoice2/voices/others/skip.mp3"));
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
                ModAudio.PlaySound(new($"res://DrXiaVoice2/voices/others/game_win.mp3"));
            }
            else
            {
                ModAudio.PlaySound(new($"res://DrXiaVoice2/voices/others/lose.mp3"));
            }
        }
    }
}
