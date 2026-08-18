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
        CreatorMenuRouletteOverlay,
        CreatorMenuStatus,
        CreatorMenuRetry,
        CreatorMenuPreview,
        CreatorMenuSize,
        CreatorMenuOrder,
        CreatorMenuAlignment,
        CreatorMenuOpacity,
        CreatorMenuLogo,
        CreatorActionCopyUrl,
        CreatorActionBack,
        CreatorValueEnabled,
        CreatorValueDisabled,
        CreatorRetryKeep,
        CreatorRetryReappear,
        CreatorOrderIconsAbove,
        CreatorOrderTextAbove,
        CreatorAlignmentLeft,
        CreatorAlignmentCenter,
        CreatorAlignmentRight,
        CreatorFeedbackUrlCopied,
        ChallengeNoDash,
        ChallengeNoMiniPlane,
        ChallengeMiniPlaneOnly,
        ChallengeNoBombs,
        ChallengeNoPeashooter,
        ChallengeNoEx,
        ChallengeBlackAndWhite,
        ChallengeRgbShift,
        ChallengeUpsideDown,
        ChallengeHpOne,
        ChallengeInkRain,
        ChallengeHalfDamage,
        ChallengeStiffMode,
        ChallengeNone
    }

    internal sealed partial class ModLocalization : IDisposable
    {
        private readonly Dictionary<ModText, string> spanish =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> spanishAmerica =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> english =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> french =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> italian =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> german =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> korean =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> russian =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> polish =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> portugueseBrazil =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> japanese =
            new Dictionary<ModText, string>();
        private readonly Dictionary<ModText, string> simplifiedChinese =
            new Dictionary<ModText, string>();

        internal event Action LanguageChanged;

        internal Localization.Languages CurrentLanguage { get; private set; }

        internal ModLocalization()
        {
            AddSpanishTexts();
            AddSpanishAmericaTexts();
            AddEnglishTexts();
            AddFrenchTexts();
            AddItalianTexts();
            AddGermanTexts();
            AddKoreanTexts();
            AddRussianTexts();
            AddPolishTexts();
            AddPortugueseBrazilTexts();
            AddJapaneseTexts();
            AddSimplifiedChineseTexts();
            ApplyApprovedLabelReviewTexts();
            ApplyCreatorToolsBrandTexts();
            CurrentLanguage = ReadCurrentLanguage();
            Localization.OnLanguageChangedEvent += HandleLanguageChanged;
        }

        internal string Text(ModText id)
        {
            string value;
            if (CurrentLanguage == Localization.Languages.SpanishAmerica &&
                spanishAmerica.TryGetValue(id, out value))
                return value;
            if (CurrentLanguage == Localization.Languages.SpanishSpain &&
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
            if (CurrentLanguage == Localization.Languages.Korean &&
                korean.TryGetValue(id, out value))
                return value;
            if (CurrentLanguage == Localization.Languages.Russian &&
                russian.TryGetValue(id, out value))
                return value;
            if (CurrentLanguage == Localization.Languages.Polish &&
                polish.TryGetValue(id, out value))
                return value;
            if (CurrentLanguage == Localization.Languages.PortugueseBrazil &&
                portugueseBrazil.TryGetValue(id, out value))
                return value;
            if (CurrentLanguage == Localization.Languages.Japanese &&
                japanese.TryGetValue(id, out value))
                return value;
            if (CurrentLanguage == Localization.Languages.SimplifiedChinese &&
                simplifiedChinese.TryGetValue(id, out value))
                return value;

            // Unknown or future languages fall back to the accepted Spanish copy.
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
                case ModifierId.RgbShift:
                    return Text(ModText.ChallengeRgbShift);
                case ModifierId.UpsideDown:
                    return Text(ModText.ChallengeUpsideDown);
                case ModifierId.HpOne:
                    return Text(ModText.ChallengeHpOne);
                case ModifierId.InkRain:
                    return Text(ModText.ChallengeInkRain);
                case ModifierId.HalfDamage:
                    return Text(ModText.ChallengeHalfDamage);
                case ModifierId.StiffMode:
                    return Text(ModText.ChallengeStiffMode);
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
            spanish[ModText.DifficultyEasy] = "FÁCIL";
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
            spanish[ModText.ChallengeNoDash] = "NO DASH";
            spanish[ModText.ChallengeNoMiniPlane] = "NO MINIAVIÓN";
            spanish[ModText.ChallengeMiniPlaneOnly] = "SOLO BALAS DE MINIAVIÓN";
            spanish[ModText.ChallengeNoBombs] = "NO DISPARO BOMBAS";
            spanish[ModText.ChallengeNoPeashooter] = "SIN DISPARO NORMAL";
            spanish[ModText.ChallengeNoEx] = "NO EX";
            spanish[ModText.ChallengeBlackAndWhite] = "BLANCO Y NEGRO";
            spanish[ModText.ChallengeRgbShift] = "RGB";
            spanish[ModText.ChallengeUpsideDown] = "180°";
            spanish[ModText.ChallengeHpOne] = "HP.1";
            spanish[ModText.ChallengeInkRain] = "LLUVIA DE TINTA";
            spanish[ModText.ChallengeHalfDamage] = "DAÑO -50%";
            spanish[ModText.ChallengeStiffMode] = "MODO TIESO";
            spanish[ModText.ChallengeNone] = "Nada";
        }

        private void AddSpanishAmericaTexts()
        {
            foreach (var pair in spanish)
                spanishAmerica[pair.Key] = pair.Value;
            spanishAmerica[ModText.ChallengeNoPeashooter] = "SIN PEASHOOTER";
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
            english[ModText.ChallengeRgbShift] = "RGB";
            english[ModText.ChallengeUpsideDown] = "180°";
            english[ModText.ChallengeHpOne] = "HP.1";
            english[ModText.ChallengeInkRain] = "INK RAIN";
            english[ModText.ChallengeHalfDamage] = "DAMAGE -50%";
            english[ModText.ChallengeStiffMode] = "STIFF MODE";
        }

        private void AddFrenchTexts()
        {
            french[ModText.SlotWeaponA] = "ARME A";
            french[ModText.SlotWeaponB] = "ARME B";
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
            french[ModText.DifficultyEasy] = "FACILE";
            french[ModText.DifficultyNormal] = "NORMAL";
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
            french[ModText.ChallengeNoPeashooter] = "SANS TIR PRINCIPAL";
            french[ModText.ChallengeNoEx] = "SANS EX";
            french[ModText.ChallengeBlackAndWhite] = "NOIR ET BLANC";
            french[ModText.ChallengeRgbShift] = "RGB";
            french[ModText.ChallengeUpsideDown] = "180°";
            french[ModText.ChallengeHpOne] = "HP.1";
            french[ModText.ChallengeInkRain] = "INK RAIN";
            french[ModText.ChallengeHalfDamage] = "DAMAGE -50%";
            french[ModText.ChallengeStiffMode] = "STIFF MODE";
        }

        private void AddItalianTexts()
        {
            italian[ModText.SlotWeaponA] = "SPARO A";
            italian[ModText.SlotWeaponB] = "SPARO B";
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
            italian[ModText.DifficultyEasy] = "FACILE";
            italian[ModText.DifficultyNormal] = "NORMALE";
            italian[ModText.DifficultyHard] = "ESPERTO";
            italian[ModText.ActionSpin] = "GIRA!";
            italian[ModText.ActionPlay] = "GIOCA!";
            italian[ModText.ActionOpenRoulette] = "APRI LA ROULETTE";
            italian[ModText.ActionSpinAgain] = "GIRA DI NUOVO";
            italian[ModText.ActionPreparing] = "PREPARAZIONE SCONTRO...";
            italian[ModText.ActionSpinning] = "GIRO IN CORSO...";
            italian[ModText.ChallengePrefix] = "SFIDA:";
            italian[ModText.ChallengeNoDash] = "SENZA DASH";
            italian[ModText.ChallengeNoMiniPlane] = "SENZA MINI-AEREO";
            italian[ModText.ChallengeMiniPlaneOnly] = "SOLO MINI-AEREO";
            italian[ModText.ChallengeNoBombs] = "SENZA MINI BOMBE";
            italian[ModText.ChallengeNoPeashooter] = "SENZA MITRAGLIATRICE";
            italian[ModText.ChallengeNoEx] = "SENZA EX";
            italian[ModText.ChallengeBlackAndWhite] = "MONOCROMO";
            italian[ModText.ChallengeRgbShift] = "RGB";
            italian[ModText.ChallengeUpsideDown] = "180°";
            italian[ModText.ChallengeHpOne] = "HP.1";
            italian[ModText.ChallengeInkRain] = "INK RAIN";
            italian[ModText.ChallengeHalfDamage] = "DAMAGE -50%";
            italian[ModText.ChallengeStiffMode] = "STIFF MODE";
        }

        private void AddGermanTexts()
        {
            german[ModText.SlotWeaponA] = "WAFFE A";
            german[ModText.SlotWeaponB] = "WAFFE B";
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
            german[ModText.ActionSpinAgain] = "NOCHMAL DREHEN";
            german[ModText.ActionPreparing] = "KAMPF WIRD VORBEREITET...";
            german[ModText.ActionSpinning] = "ROULETTE DREHT SICH...";
            german[ModText.ChallengePrefix] = "CHALLENGE:";
            german[ModText.ChallengeNoDash] = "OHNE DASH";
            german[ModText.ChallengeNoMiniPlane] = "OHNE MINIFLUGZEUG";
            german[ModText.ChallengeMiniPlaneOnly] = "NUR MINIFLUGZEUG";
            german[ModText.ChallengeNoBombs] = "OHNE MINIBOMBEN";
            german[ModText.ChallengeNoPeashooter] = "OHNE MASCHINENGEWEHR";
            german[ModText.ChallengeNoEx] = "OHNE EX";
            german[ModText.ChallengeBlackAndWhite] = "SCHWARZ-WEISS";
            german[ModText.ChallengeRgbShift] = "RGB";
            german[ModText.ChallengeUpsideDown] = "180°";
            german[ModText.ChallengeHpOne] = "HP.1";
            german[ModText.ChallengeInkRain] = "INK RAIN";
            german[ModText.ChallengeHalfDamage] = "DAMAGE -50%";
            german[ModText.ChallengeStiffMode] = "STIFF MODE";
        }

        private void AddKoreanTexts()
        {
            korean[ModText.SlotWeaponA] = "무기 A";
            korean[ModText.SlotWeaponB] = "무기 B";
            korean[ModText.SlotSuper] = "필살기";
            korean[ModText.SlotCharm] = "능력";
            korean[ModText.SlotChallenge] = "도전";
            korean[ModText.SettingDifficulty] = "난이도";
            korean[ModText.SettingChallenge] = "도전";
            korean[ModText.SettingAutoLoad] = "자동 로드";
            korean[ModText.ValueEnabled] = "켜짐";
            korean[ModText.ValueDisabled] = "꺼짐";
            korean[ModText.ValueEnabledFeminine] = "켜짐";
            korean[ModText.ValueDisabledFeminine] = "꺼짐";
            korean[ModText.DifficultyEasy] = "쉬움";
            korean[ModText.DifficultyNormal] = "보통";
            korean[ModText.DifficultyHard] = "어려움";
            korean[ModText.ActionSpin] = "돌리기!";
            korean[ModText.ActionPlay] = "시작!";
            korean[ModText.ActionOpenRoulette] = "룰렛 열기";
            korean[ModText.ActionSpinAgain] = "다시 돌리기";
            korean[ModText.ActionPreparing] = "전투 준비 중...";
            korean[ModText.ActionSpinning] = "회전 중...";
            korean[ModText.ChallengePrefix] = "도전:";
            korean[ModText.ChallengeNoDash] = "대시 금지";
            korean[ModText.ChallengeNoMiniPlane] = "소형 비행기 금지";
            korean[ModText.ChallengeMiniPlaneOnly] = "소형 비행기 총알만";
            korean[ModText.ChallengeNoBombs] = "소형 폭탄 금지";
            korean[ModText.ChallengeNoPeashooter] = "기본 공격 금지";
            korean[ModText.ChallengeNoEx] = "EX 공격 금지";
            korean[ModText.ChallengeBlackAndWhite] = "흑백";
            korean[ModText.ChallengeRgbShift] = "RGB";
            korean[ModText.ChallengeUpsideDown] = "180°";
            korean[ModText.ChallengeHpOne] = "HP.1";
            korean[ModText.ChallengeInkRain] = "INK RAIN";
            korean[ModText.ChallengeHalfDamage] = "DAMAGE -50%";
            korean[ModText.ChallengeStiffMode] = "STIFF MODE";
        }

        private void AddRussianTexts()
        {
            russian[ModText.SlotWeaponA] = "УДАР 1";
            russian[ModText.SlotWeaponB] = "УДАР 2";
            russian[ModText.SlotSuper] = "СПЕЦАТАКА";
            russian[ModText.SlotCharm] = "НАВЫК";
            russian[ModText.SlotChallenge] = "ИСПЫТАНИЕ";
            russian[ModText.SettingDifficulty] = "СЛОЖНОСТЬ";
            russian[ModText.SettingChallenge] = "ИСПЫТАНИЕ";
            russian[ModText.SettingAutoLoad] = "АВТОЗАГРУЗКА";
            russian[ModText.ValueEnabled] = "ВКЛ.";
            russian[ModText.ValueDisabled] = "ВЫКЛ.";
            russian[ModText.ValueEnabledFeminine] = "ВКЛ.";
            russian[ModText.ValueDisabledFeminine] = "ВЫКЛ.";
            russian[ModText.DifficultyEasy] = "НИЗКАЯ";
            russian[ModText.DifficultyNormal] = "ОБЫЧНАЯ";
            russian[ModText.DifficultyHard] = "ВЫСОКАЯ";
            russian[ModText.ActionSpin] = "КРУТИТЬ!";
            russian[ModText.ActionPlay] = "ИГРАТЬ!";
            russian[ModText.ActionOpenRoulette] = "ОТКРЫТЬ РУЛЕТКУ";
            russian[ModText.ActionSpinAgain] = "КРУТИТЬ ЕЩЁ РАЗ";
            russian[ModText.ActionPreparing] = "ПОДГОТОВКА К БОЮ...";
            russian[ModText.ActionSpinning] = "РУЛЕТКА КРУТИТСЯ...";
            russian[ModText.ChallengePrefix] = "ИСПЫТАНИЕ:";
            russian[ModText.ChallengeNoDash] = "БЕЗ РЫВКА";
            russian[ModText.ChallengeNoMiniPlane] = "БЕЗ МИНИ-САМОЛЁТА";
            russian[ModText.ChallengeMiniPlaneOnly] = "ТОЛЬКО МИНИ-ПУЛИ";
            russian[ModText.ChallengeNoBombs] = "БЕЗ МИНИ-БОМБ";
            russian[ModText.ChallengeNoPeashooter] = "БЕЗ ОБЫЧНОГО ВЫСТРЕЛА";
            russian[ModText.ChallengeNoEx] = "БЕЗ EX";
            russian[ModText.ChallengeBlackAndWhite] = "ЧЕРНО-БЕЛЫЙ";
            russian[ModText.ChallengeRgbShift] = "RGB";
            russian[ModText.ChallengeUpsideDown] = "180°";
            russian[ModText.ChallengeHpOne] = "HP.1";
            russian[ModText.ChallengeInkRain] = "INK RAIN";
            russian[ModText.ChallengeHalfDamage] = "DAMAGE -50%";
            russian[ModText.ChallengeStiffMode] = "STIFF MODE";
        }

        private void AddPolishTexts()
        {
            polish[ModText.SlotWeaponA] = "BROŃ A";
            polish[ModText.SlotWeaponB] = "BROŃ B";
            polish[ModText.SlotSuper] = "SUPER";
            polish[ModText.SlotCharm] = "CZAR";
            polish[ModText.SlotChallenge] = "WYZWANIE";
            polish[ModText.SettingDifficulty] = "TRUDNOŚĆ";
            polish[ModText.SettingChallenge] = "WYZWANIE";
            polish[ModText.SettingAutoLoad] = "AUTOMATYCZNE ŁADOWANIE";
            polish[ModText.ValueEnabled] = "WŁĄCZONE";
            polish[ModText.ValueDisabled] = "WYŁĄCZONE";
            polish[ModText.ValueEnabledFeminine] = "WŁĄCZONE";
            polish[ModText.ValueDisabledFeminine] = "WYŁĄCZONE";
            polish[ModText.DifficultyEasy] = "PROSTY";
            polish[ModText.DifficultyNormal] = "ZWYKŁY";
            polish[ModText.DifficultyHard] = "EKSPERCKI";
            polish[ModText.ActionSpin] = "ZAKRĘĆ!";
            polish[ModText.ActionPlay] = "GRAJ!";
            polish[ModText.ActionOpenRoulette] = "OTWÓRZ RULETKĘ";
            polish[ModText.ActionSpinAgain] = "ZAKRĘĆ PONOWNIE";
            polish[ModText.ActionPreparing] = "PRZYGOTOWANIE DO WALKI...";
            polish[ModText.ActionSpinning] = "RULETKA SIĘ KRĘCI...";
            polish[ModText.ChallengePrefix] = "WYZWANIE:";
            polish[ModText.ChallengeNoDash] = "BEZ DASHA";
            polish[ModText.ChallengeNoMiniPlane] = "BEZ MAŁEGO SAMOLOTU";
            polish[ModText.ChallengeMiniPlaneOnly] = "TYLKO MAŁY SAMOLOT";
            polish[ModText.ChallengeNoBombs] = "BEZ BOMB";
            polish[ModText.ChallengeNoPeashooter] = "BEZ DZIAŁKA";
            polish[ModText.ChallengeNoEx] = "BEZ EX";
            polish[ModText.ChallengeBlackAndWhite] = "CZARNO-BIAŁY";
            polish[ModText.ChallengeRgbShift] = "RGB";
            polish[ModText.ChallengeUpsideDown] = "180°";
            polish[ModText.ChallengeHpOne] = "HP.1";
            polish[ModText.ChallengeInkRain] = "INK RAIN";
            polish[ModText.ChallengeHalfDamage] = "DAMAGE -50%";
            polish[ModText.ChallengeStiffMode] = "STIFF MODE";
        }

        private void AddPortugueseBrazilTexts()
        {
            portugueseBrazil[ModText.SlotWeaponA] = "TIRO-A";
            portugueseBrazil[ModText.SlotWeaponB] = "TIRO-B";
            portugueseBrazil[ModText.SlotSuper] = "SUPER";
            portugueseBrazil[ModText.SlotCharm] = "RELÍQUIAS";
            portugueseBrazil[ModText.SlotChallenge] = "DESAFIO";
            portugueseBrazil[ModText.SettingDifficulty] = "DIFICULDADE";
            portugueseBrazil[ModText.SettingChallenge] = "DESAFIO";
            portugueseBrazil[ModText.SettingAutoLoad] = "CARREGAMENTO AUTOMÁTICO";
            portugueseBrazil[ModText.ValueEnabled] = "ATIVADO";
            portugueseBrazil[ModText.ValueDisabled] = "DESATIVADO";
            portugueseBrazil[ModText.ValueEnabledFeminine] = "ATIVADO";
            portugueseBrazil[ModText.ValueDisabledFeminine] = "DESATIVADO";
            portugueseBrazil[ModText.DifficultyEasy] = "FÁCIL";
            portugueseBrazil[ModText.DifficultyNormal] = "NORMAL";
            portugueseBrazil[ModText.DifficultyHard] = "ESPECIALISTA";
            portugueseBrazil[ModText.ActionSpin] = "GIRAR!";
            portugueseBrazil[ModText.ActionPlay] = "JOGAR!";
            portugueseBrazil[ModText.ActionOpenRoulette] = "ABRIR ROLETA";
            portugueseBrazil[ModText.ActionSpinAgain] = "GIRAR NOVAMENTE";
            portugueseBrazil[ModText.ActionPreparing] = "PREPARANDO COMBATE...";
            portugueseBrazil[ModText.ActionSpinning] = "GIRANDO...";
            portugueseBrazil[ModText.ChallengePrefix] = "DESAFIO:";
            portugueseBrazil[ModText.ChallengeNoDash] = "SEM DASH";
            portugueseBrazil[ModText.ChallengeNoMiniPlane] = "SEM MINIAVIÃO";
            portugueseBrazil[ModText.ChallengeMiniPlaneOnly] = "SÓ MINIAVIÃO";
            portugueseBrazil[ModText.ChallengeNoBombs] = "SEM MINIBOMBAS";
            portugueseBrazil[ModText.ChallengeNoPeashooter] = "SEM METRALHADORA";
            portugueseBrazil[ModText.ChallengeNoEx] = "SEM EX";
            portugueseBrazil[ModText.ChallengeBlackAndWhite] = "PRETO E BRANCO";
            portugueseBrazil[ModText.ChallengeRgbShift] = "RGB";
            portugueseBrazil[ModText.ChallengeUpsideDown] = "180°";
            portugueseBrazil[ModText.ChallengeHpOne] = "HP.1";
            portugueseBrazil[ModText.ChallengeInkRain] = "INK RAIN";
            portugueseBrazil[ModText.ChallengeHalfDamage] = "DAMAGE -50%";
            portugueseBrazil[ModText.ChallengeStiffMode] = "STIFF MODE";
        }

        private void AddJapaneseTexts()
        {
            japanese[ModText.SlotWeaponA] = "ショットA";
            japanese[ModText.SlotWeaponB] = "ショットB";
            japanese[ModText.SlotSuper] = "必殺技";
            japanese[ModText.SlotCharm] = "お守り";
            japanese[ModText.SlotChallenge] = "チャレンジ";
            japanese[ModText.SettingDifficulty] = "難易度";
            japanese[ModText.SettingChallenge] = "チャレンジ";
            japanese[ModText.SettingAutoLoad] = "自動ロード";
            japanese[ModText.ValueEnabled] = "オン";
            japanese[ModText.ValueDisabled] = "オフ";
            japanese[ModText.ValueEnabledFeminine] = "オン";
            japanese[ModText.ValueDisabledFeminine] = "オフ";
            japanese[ModText.DifficultyEasy] = "シンプル";
            japanese[ModText.DifficultyNormal] = "レギュラー";
            japanese[ModText.DifficultyHard] = "エキスパート";
            japanese[ModText.ActionSpin] = "回す！";
            japanese[ModText.ActionPlay] = "プレイ！";
            japanese[ModText.ActionOpenRoulette] = "ルーレットを開く";
            japanese[ModText.ActionSpinAgain] = "もう一度回す";
            japanese[ModText.ActionPreparing] = "バトル準備中...";
            japanese[ModText.ActionSpinning] = "回転中...";
            japanese[ModText.ChallengePrefix] = "チャレンジ：";
            japanese[ModText.ChallengeNoDash] = "ダッシュ禁止";
            japanese[ModText.ChallengeNoMiniPlane] = "ミニ化禁止";
            japanese[ModText.ChallengeMiniPlaneOnly] = "ミニショットのみ";
            japanese[ModText.ChallengeNoBombs] = "ミニボム禁止";
            japanese[ModText.ChallengeNoPeashooter] = "通常ショット禁止";
            japanese[ModText.ChallengeNoEx] = "EXショット禁止";
            japanese[ModText.ChallengeBlackAndWhite] = "モノクロ";
            japanese[ModText.ChallengeRgbShift] = "RGB";
            japanese[ModText.ChallengeUpsideDown] = "180°";
            japanese[ModText.ChallengeHpOne] = "HP.1";
            japanese[ModText.ChallengeInkRain] = "INK RAIN";
            japanese[ModText.ChallengeHalfDamage] = "DAMAGE -50%";
            japanese[ModText.ChallengeStiffMode] = "STIFF MODE";
        }

        private void AddSimplifiedChineseTexts()
        {
            simplifiedChinese[ModText.SlotWeaponA] = "武器A";
            simplifiedChinese[ModText.SlotWeaponB] = "武器B";
            simplifiedChinese[ModText.SlotSuper] = "必杀技";
            simplifiedChinese[ModText.SlotCharm] = "护符";
            simplifiedChinese[ModText.SlotChallenge] = "挑战";
            simplifiedChinese[ModText.SettingDifficulty] = "难度";
            simplifiedChinese[ModText.SettingChallenge] = "挑战";
            simplifiedChinese[ModText.SettingAutoLoad] = "自动加载";
            simplifiedChinese[ModText.ValueEnabled] = "开启";
            simplifiedChinese[ModText.ValueDisabled] = "关闭";
            simplifiedChinese[ModText.ValueEnabledFeminine] = "开启";
            simplifiedChinese[ModText.ValueDisabledFeminine] = "关闭";
            simplifiedChinese[ModText.DifficultyEasy] = "简单";
            simplifiedChinese[ModText.DifficultyNormal] = "普通";
            simplifiedChinese[ModText.DifficultyHard] = "专家";
            simplifiedChinese[ModText.ActionSpin] = "转动！";
            simplifiedChinese[ModText.ActionPlay] = "开始！";
            simplifiedChinese[ModText.ActionOpenRoulette] = "打开轮盘";
            simplifiedChinese[ModText.ActionSpinAgain] = "再转一次";
            simplifiedChinese[ModText.ActionPreparing] = "战斗准备中...";
            simplifiedChinese[ModText.ActionSpinning] = "转动中...";
            simplifiedChinese[ModText.ChallengePrefix] = "挑战：";
            simplifiedChinese[ModText.ChallengeNoDash] = "禁止冲刺";
            simplifiedChinese[ModText.ChallengeNoMiniPlane] = "禁止缩小";
            simplifiedChinese[ModText.ChallengeMiniPlaneOnly] = "仅限小飞机子弹";
            simplifiedChinese[ModText.ChallengeNoBombs] = "禁止迷你炸弹";
            simplifiedChinese[ModText.ChallengeNoPeashooter] = "禁止普通射击";
            simplifiedChinese[ModText.ChallengeNoEx] = "禁止EX攻击";
            simplifiedChinese[ModText.ChallengeBlackAndWhite] = "黑白";
            simplifiedChinese[ModText.ChallengeRgbShift] = "RGB";
            simplifiedChinese[ModText.ChallengeUpsideDown] = "180°";
            simplifiedChinese[ModText.ChallengeHpOne] = "HP.1";
            simplifiedChinese[ModText.ChallengeInkRain] = "INK RAIN";
            simplifiedChinese[ModText.ChallengeHalfDamage] = "DAMAGE -50%";
            simplifiedChinese[ModText.ChallengeStiffMode] = "STIFF MODE";
        }
    }
}
