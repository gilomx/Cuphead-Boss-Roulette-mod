using System;
using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    internal enum RouletteStatus
    {
        Ready,
        Spinning,
        ResultReady,
        ResultLoading,
        SaveRequired,
        SceneLoading,
        LoadFailed
    }

    internal enum ModText
    {
        Brand,
        Tagline,
        SlotWeaponA,
        SlotWeaponB,
        SlotSuper,
        SlotCharm,
        SlotChallenge,
        SettingDifficulty,
        SettingChallenge,
        SettingAutoLoad,
        ValueEnabled,
        ValueDisabled,
        ValueEnabledFeminine,
        ValueDisabledFeminine,
        ValueSelected,
        ValueRolling,
        DifficultyEasy,
        DifficultyNormal,
        DifficultyHard,
        ActionSpin,
        ActionPlay,
        ActionClose,
        ActionOpenRoulette,
        ActionSpinAgain,
        ActionPreparing,
        ActionSpinning,
        ActionSelectSave,
        ChallengePrefix,
        ControlsLegacy,
        StatusReady,
        StatusSpinning,
        StatusResultReady,
        StatusResultLoading,
        StatusSaveRequired,
        StatusSceneLoading,
        StatusLoadFailed,
        CommonNone,
        CharmCursedRelic,
        CharmDivineRelic,
        ChallengeNoDash,
        ChallengeNoMiniPlane,
        ChallengeMiniPlaneOnly,
        ChallengeNoBombs,
        ChallengeNoPeashooter,
        ChallengeNoEx,
        ChallengeBlackAndWhite,
        ChallengeNone
    }

    internal sealed class ModLocalization : IDisposable
    {
        private readonly Dictionary<ModText, string> spanish =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> english =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> french =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> italian =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> german =
            new Dictionary<ModText, string>();

        internal event Action LanguageChanged;

        internal Localization.Languages CurrentLanguage { get; private set; }

        internal ModLocalization()
        {
            AddSpanishTexts();
            AddEnglishTexts();
            AddFrenchTexts();
            AddItalianTexts();
            AddGermanTexts();
            CurrentLanguage = ReadCurrentLanguage();
            Localization.OnLanguageChangedEvent += HandleLanguageChanged;
        }

        internal string Text(ModText id)
        {
            string value;
            if ((CurrentLanguage == Localization.Languages.SpanishSpain ||
                 CurrentLanguage == Localization.Languages.SpanishAmerica) &&
                spanish.TryGetValue(id, out value))
                return value;
            if (CurrentLanguage == Localization.Languages.English &&
                english.TryGetValue(id, out value))
                return value;
            if (CurrentLanguage == Localization.Languages.French &&
                french.TryGetValue(id, out value))
                return value;
            if (CurrentLanguage == Localization.Languages.Italian &&
                italian.TryGetValue(id, out value))
                return value;
            if (CurrentLanguage == Localization.Languages.German &&
                german.TryGetValue(id, out value))
                return value;

            // Languages without an approved table intentionally fall back to
            // the accepted Spanish copy.
            return spanish.TryGetValue(id, out value) ? value : id.ToString();
        }

        internal string StatusText(RouletteStatus status)
        {
            switch (status)
            {
                case RouletteStatus.Spinning:
                    return Text(ModText.StatusSpinning);
                case RouletteStatus.ResultReady:
                    return Text(ModText.StatusResultReady);
                case RouletteStatus.ResultLoading:
                    return Text(ModText.StatusResultLoading);
                case RouletteStatus.SaveRequired:
                    return Text(ModText.StatusSaveRequired);
                case RouletteStatus.SceneLoading:
                    return Text(ModText.StatusSceneLoading);
                case RouletteStatus.LoadFailed:
                    return Text(ModText.StatusLoadFailed);
                default:
                    return Text(ModText.StatusReady);
            }
        }

        internal string ModifierName(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return Text(ModText.ChallengeNoDash);
                case ModifierId.NoMiniPlane:
                    return Text(ModText.ChallengeNoMiniPlane);
                case ModifierId.MiniPlaneOnly:
                    return Text(ModText.ChallengeMiniPlaneOnly);
                case ModifierId.NoBombs:
                    return Text(ModText.ChallengeNoBombs);
                case ModifierId.NoPeashooter:
                    return Text(ModText.ChallengeNoPeashooter);
                case ModifierId.NoEx:
                    return Text(ModText.ChallengeNoEx);
                case ModifierId.BlackAndWhite:
                    return Text(ModText.ChallengeBlackAndWhite);
                default:
                    return Text(ModText.ChallengeNone);
            }
        }

        internal string ChallengeLabel(ModifierId id)
        {
            if (id == ModifierId.None)
                return string.Empty;
            return Text(ModText.ChallengePrefix) + " " + ModifierName(id);
        }

        public void Dispose()
        {
            Localization.OnLanguageChangedEvent -= HandleLanguageChanged;
            LanguageChanged = null;
        }

        private void HandleLanguageChanged()
        {
            CurrentLanguage = ReadCurrentLanguage();
            var handler = LanguageChanged;
            if (handler != null)
                handler();
        }

        private static Localization.Languages ReadCurrentLanguage()
        {
            try
            {
                return Localization.language;
            }
            catch
            {
                return Localization.Languages.SpanishAmerica;
            }
        }

        private void AddSpanishTexts()
        {
            spanish[ModText.Brand] = "CUPHEAD · BOSS ROULETTE";
            spanish[ModText.Tagline] = "¡EL DESTINO DECIDE TU PRÓXIMO COMBATE!";
            spanish[ModText.SlotWeaponA] = "TIRO A";
            spanish[ModText.SlotWeaponB] = "TIRO B";
            spanish[ModText.SlotSuper] = "SÚPER";
            spanish[ModText.SlotCharm] = "AMULETO";
            spanish[ModText.SlotChallenge] = "RETO";
            spanish[ModText.SettingDifficulty] = "DIFICULTAD";
            spanish[ModText.SettingChallenge] = "RETO";
            spanish[ModText.SettingAutoLoad] = "CARGA AUTOMÁTICA";
            spanish[ModText.ValueEnabled] = "ACTIVADO";
            spanish[ModText.ValueDisabled] = "DESACTIVADO";
            spanish[ModText.ValueEnabledFeminine] = "ACTIVADA";
            spanish[ModText.ValueDisabledFeminine] = "DESACTIVADA";
            spanish[ModText.ValueSelected] = "SELECCIONADO";
            spanish[ModText.ValueRolling] = "GIRANDO...";
            spanish[ModText.DifficultyEasy] = "SIMPLE";
            spanish[ModText.DifficultyNormal] = "NORMAL";
            spanish[ModText.DifficultyHard] = "EXPERTO";
            spanish[ModText.ActionSpin] = "¡GIRAR!";
            spanish[ModText.ActionPlay] = "¡JUGAR!";
            spanish[ModText.ActionClose] = "CERRAR";
            spanish[ModText.ActionOpenRoulette] = "ABRIR RULETA";
            spanish[ModText.ActionSpinAgain] = "VOLVER A GIRAR";
            spanish[ModText.ActionPreparing] = "PREPARANDO COMBATE...";
            spanish[ModText.ActionSpinning] = "GIRANDO...";
            spanish[ModText.ActionSelectSave] = "SELECCIONA UNA PARTIDA";
            spanish[ModText.ChallengePrefix] = "RETO:";
            spanish[ModText.ControlsLegacy] =
                "F6  ABRIR/CERRAR     ·     F7  GIRAR     ·     CTRL+I  SELECCIÓN FORZADA";
            spanish[ModText.StatusReady] = "PULSA ENTER PARA GIRAR";
            spanish[ModText.StatusSpinning] = "¡LA RULETA ESTÁ GIRANDO!";
            spanish[ModText.StatusResultReady] = "¡RESULTADO LISTO!";
            spanish[ModText.StatusResultLoading] =
                "¡RESULTADO LISTO! PREPARANDO COMBATE...";
            spanish[ModText.StatusSaveRequired] =
                "SELECCIONA PRIMERO UNA PARTIDA GUARDADA";
            spanish[ModText.StatusSceneLoading] =
                "CUPHEAD YA ESTÁ CARGANDO OTRA ESCENA";
            spanish[ModText.StatusLoadFailed] =
                "NO SE PUDO CARGAR. REVISA LOGOUTPUT.LOG";
            spanish[ModText.CommonNone] = "Nada";
            spanish[ModText.CharmCursedRelic] = "Reliquia Maldita";
            spanish[ModText.CharmDivineRelic] = "Reliquia Divina";
            spanish[ModText.ChallengeNoDash] = "No Dash";
            spanish[ModText.ChallengeNoMiniPlane] = "No mini avión";
            spanish[ModText.ChallengeMiniPlaneOnly] = "Solo mini avión";
            spanish[ModText.ChallengeNoBombs] = "No disparo bombas";
            spanish[ModText.ChallengeNoPeashooter] = "No disparo Peashooter";
            spanish[ModText.ChallengeNoEx] = "No EX";
            spanish[ModText.ChallengeBlackAndWhite] = "Blanco y negro";
            spanish[ModText.ChallengeNone] = "Nada";
        }

        private void AddEnglishTexts()
        {
            english[ModText.SlotWeaponA] = "SHOT-A";
            english[ModText.SlotWeaponB] = "SHOT-B";
            english[ModText.SlotSuper] = "SUPER";
            english[ModText.SlotCharm] = "CHARM";
            english[ModText.SlotChallenge] = "CHALLENGE";
            english[ModText.SettingDifficulty] = "DIFFICULTY";
            english[ModText.SettingChallenge] = "CHALLENGE";
            english[ModText.SettingAutoLoad] = "AUTO-LOAD";
            english[ModText.ValueEnabled] = "ON";
            english[ModText.ValueDisabled] = "OFF";
            english[ModText.ValueEnabledFeminine] = "ON";
            english[ModText.ValueDisabledFeminine] = "OFF";
            english[ModText.DifficultyEasy] = "SIMPLE";
            english[ModText.DifficultyNormal] = "REGULAR";
            english[ModText.DifficultyHard] = "EXPERT";
            english[ModText.ActionSpin] = "SPIN!";
            english[ModText.ActionPlay] = "PLAY!";
            english[ModText.ActionOpenRoulette] = "OPEN ROULETTE";
            english[ModText.ActionSpinAgain] = "SPIN AGAIN";
            english[ModText.ActionPreparing] = "PREPARING BATTLE...";
            english[ModText.ActionSpinning] = "SPINNING...";
            english[ModText.ChallengePrefix] = "CHALLENGE:";
            english[ModText.ChallengeNoDash] = "NO DASH";
            english[ModText.ChallengeNoMiniPlane] = "NO MINI-PLANE";
            english[ModText.ChallengeMiniPlaneOnly] = "MINI-PLANE ONLY";
            english[ModText.ChallengeNoBombs] = "NO MINI-BOMBS";
            english[ModText.ChallengeNoPeashooter] = "NO PEASHOOTER";
            english[ModText.ChallengeNoEx] = "NO EX";
            english[ModText.ChallengeBlackAndWhite] = "BLACK & WHITE";
        }

        private void AddFrenchTexts()
        {
            french[ModText.SlotWeaponA] = "TIR-A";
            french[ModText.SlotWeaponB] = "TIR-B";
            french[ModText.SlotSuper] = "SUPER";
            french[ModText.SlotCharm] = "CHARME";
            french[ModText.SlotChallenge] = "DÉFI";
            french[ModText.SettingDifficulty] = "DIFFICULTÉ";
            french[ModText.SettingChallenge] = "DÉFI";
            french[ModText.SettingAutoLoad] = "CHARGEMENT AUTO";
            french[ModText.ValueEnabled] = "ACTIVÉ";
            french[ModText.ValueDisabled] = "DÉSACTIVÉ";
            french[ModText.ValueEnabledFeminine] = "ACTIVÉ";
            french[ModText.ValueDisabledFeminine] = "DÉSACTIVÉ";
            french[ModText.DifficultyEasy] = "SIMPLE";
            french[ModText.DifficultyNormal] = "RÉGULIER";
            french[ModText.DifficultyHard] = "EXPERT";
            french[ModText.ActionSpin] = "LANCER !";
            french[ModText.ActionPlay] = "JOUER !";
            french[ModText.ActionOpenRoulette] = "OUVRIR LA ROULETTE";
            french[ModText.ActionSpinAgain] = "RELANCER";
            french[ModText.ActionPreparing] = "PRÉPARATION DU COMBAT...";
            french[ModText.ActionSpinning] = "LA ROULETTE TOURNE...";
            french[ModText.ChallengePrefix] = "DÉFI :";
            french[ModText.ChallengeNoDash] = "SANS DASH";
            french[ModText.ChallengeNoMiniPlane] = "SANS MINI-AVION";
            french[ModText.ChallengeMiniPlaneOnly] = "MINI-AVION UNIQUEMENT";
            french[ModText.ChallengeNoBombs] = "SANS MINI-BOMBES";
            french[ModText.ChallengeNoPeashooter] = "SANS LANCE-POIS";
            french[ModText.ChallengeNoEx] = "SANS EX";
            french[ModText.ChallengeBlackAndWhite] = "NOIR ET BLANC";
        }

        private void AddItalianTexts()
        {
            italian[ModText.SlotWeaponA] = "COLPO A";
            italian[ModText.SlotWeaponB] = "COLPO B";
            italian[ModText.SlotSuper] = "SUPER";
            italian[ModText.SlotCharm] = "AMULETO";
            italian[ModText.SlotChallenge] = "SFIDA";
            italian[ModText.SettingDifficulty] = "DIFFICOLTÀ";
            italian[ModText.SettingChallenge] = "SFIDA";
            italian[ModText.SettingAutoLoad] = "CARICAMENTO AUTOMATICO";
            italian[ModText.ValueEnabled] = "ATTIVA";
            italian[ModText.ValueDisabled] = "DISATTIVA";
            italian[ModText.ValueEnabledFeminine] = "ATTIVO";
            italian[ModText.ValueDisabledFeminine] = "DISATTIVO";
            italian[ModText.DifficultyEasy] = "SEMPLICE";
            italian[ModText.DifficultyNormal] = "REGOLARE";
            italian[ModText.DifficultyHard] = "ESPERTO";
            italian[ModText.ActionSpin] = "GIRA!";
            italian[ModText.ActionPlay] = "GIOCA!";
            italian[ModText.ActionOpenRoulette] = "APRI LA RULETTA";
            italian[ModText.ActionSpinAgain] = "GIRA DI NUOVO";
            italian[ModText.ActionPreparing] = "PREPARAZIONE SCONTRO...";
            italian[ModText.ActionSpinning] = "GIRO IN CORSO...";
            italian[ModText.ChallengePrefix] = "SFIDA:";
            italian[ModText.ChallengeNoDash] = "SENZA SCATTO";
            italian[ModText.ChallengeNoMiniPlane] = "SENZA MINI-AEREO";
            italian[ModText.ChallengeMiniPlaneOnly] = "SOLO MINI-AEREO";
            italian[ModText.ChallengeNoBombs] = "SENZA MINI-BOMBE";
            italian[ModText.ChallengeNoPeashooter] = "SENZA SPARASEMI";
            italian[ModText.ChallengeNoEx] = "SENZA EX";
            italian[ModText.ChallengeBlackAndWhite] = "BIANCO E NERO";
        }

        private void AddGermanTexts()
        {
            german[ModText.SlotWeaponA] = "SCHUSS-A";
            german[ModText.SlotWeaponB] = "SCHUSS-B";
            german[ModText.SlotSuper] = "SUPER";
            german[ModText.SlotCharm] = "AMULETT";
            german[ModText.SlotChallenge] = "CHALLENGE";
            german[ModText.SettingDifficulty] = "SCHWIERIGKEIT";
            german[ModText.SettingChallenge] = "CHALLENGE";
            german[ModText.SettingAutoLoad] = "AUTO-LADEN";
            german[ModText.ValueEnabled] = "EIN";
            german[ModText.ValueDisabled] = "AUS";
            german[ModText.ValueEnabledFeminine] = "EIN";
            german[ModText.ValueDisabledFeminine] = "AUS";
            german[ModText.DifficultyEasy] = "LEICHT";
            german[ModText.DifficultyNormal] = "NORMAL";
            german[ModText.DifficultyHard] = "EXPERTE";
            german[ModText.ActionSpin] = "DREHEN!";
            german[ModText.ActionPlay] = "SPIELEN!";
            german[ModText.ActionOpenRoulette] = "ROULETTE ÖFFNEN";
            german[ModText.ActionSpinAgain] = "ERNEUT DREHEN";
            german[ModText.ActionPreparing] = "KAMPF WIRD VORBEREITET...";
            german[ModText.ActionSpinning] = "ROULETTE DREHT SICH...";
            german[ModText.ChallengePrefix] = "CHALLENGE:";
            german[ModText.ChallengeNoDash] = "OHNE DASH";
            german[ModText.ChallengeNoMiniPlane] = "OHNE MINIFLUGZEUG";
            german[ModText.ChallengeMiniPlaneOnly] = "NUR MINIFLUGZEUG";
            german[ModText.ChallengeNoBombs] = "OHNE MINI-BOMBEN";
            german[ModText.ChallengeNoPeashooter] = "OHNE PEASHOOTER";
            german[ModText.ChallengeNoEx] = "OHNE EX";
            german[ModText.ChallengeBlackAndWhite] = "SCHWARZ-WEISS";
        }
    }
}
