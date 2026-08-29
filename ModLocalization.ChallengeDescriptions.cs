namespace Gilomx.CupheadBossRoulette
{
    internal sealed partial class ModLocalization
    {
        private const string AirplaneDescriptionColor = "#C94F2D";

        internal string ModifierKindName(ModifierKind kind)
        {
            switch (CurrentLanguage)
            {
                case Localization.Languages.SpanishSpain:
                case Localization.Languages.SpanishAmerica:
                    return kind == ModifierKind.Ground ? "TIERRA" :
                        kind == ModifierKind.Plane ? "AVIÓN" : "AMBOS";
                case Localization.Languages.French:
                    return kind == ModifierKind.Ground ? "SOL" :
                        kind == ModifierKind.Plane ? "AÉRIEN" : "LES DEUX";
                case Localization.Languages.Italian:
                    return kind == ModifierKind.Ground ? "TERRA" :
                        kind == ModifierKind.Plane ? "AEREO" : "ENTRAMBI";
                case Localization.Languages.German:
                    return kind == ModifierKind.Ground ? "BODEN" :
                        kind == ModifierKind.Plane ? "FLUG" : "BEIDE";
                case Localization.Languages.Korean:
                    return kind == ModifierKind.Ground ? "지상" :
                        kind == ModifierKind.Plane ? "비행" : "모두";
                case Localization.Languages.Russian:
                    return kind == ModifierKind.Ground ? "ЗЕМЛЯ" :
                        kind == ModifierKind.Plane ? "ВОЗДУХ" : "ОБА";
                case Localization.Languages.Polish:
                    return kind == ModifierKind.Ground ? "ZIEMIA" :
                        kind == ModifierKind.Plane ? "SAMOLOT" : "OBA";
                case Localization.Languages.PortugueseBrazil:
                    return kind == ModifierKind.Ground ? "SOLO" :
                        kind == ModifierKind.Plane ? "AVIÃO" : "AMBOS";
                case Localization.Languages.Japanese:
                    return kind == ModifierKind.Ground ? "地上" :
                        kind == ModifierKind.Plane ? "飛行" : "両方";
                case Localization.Languages.SimplifiedChinese:
                    return kind == ModifierKind.Ground ? "地面" :
                        kind == ModifierKind.Plane ? "飞机" : "两者";
                case Localization.Languages.English:
                default:
                    return kind == ModifierKind.Ground ? "GROUND" :
                        kind == ModifierKind.Plane ? "AIR" : "BOTH";
            }
        }

        internal string ModifierDescription(ModifierId id)
        {
            switch (CurrentLanguage)
            {
                case Localization.Languages.SpanishSpain:
                case Localization.Languages.SpanishAmerica:
                    return SpanishModifierDescription(id);
                case Localization.Languages.French:
                    return FrenchModifierDescription(id);
                case Localization.Languages.Italian:
                    return ItalianModifierDescription(id);
                case Localization.Languages.German:
                    return GermanModifierDescription(id);
                case Localization.Languages.Korean:
                    return KoreanModifierDescription(id);
                case Localization.Languages.Russian:
                    return RussianModifierDescription(id);
                case Localization.Languages.Polish:
                    return PolishModifierDescription(id);
                case Localization.Languages.PortugueseBrazil:
                    return PortugueseModifierDescription(id);
                case Localization.Languages.Japanese:
                    return JapaneseModifierDescription(id);
                case Localization.Languages.SimplifiedChinese:
                    return ChineseModifierDescription(id);
                case Localization.Languages.English:
                default:
                    return EnglishModifierDescription(id);
            }
        }

        internal string ModifierDescriptionRichText(ModifierId id)
        {
            var description = ModifierDescription(id);
            if (string.IsNullOrEmpty(description) ||
                !HasAirplaneDescriptionPart(id))
                return description;

            if (id == ModifierId.NoMiniPlane)
                return ColorAirplaneDescriptionPart(description);

            var sentenceStart = LastSentenceStart(description);
            if (sentenceStart <= 0 || sentenceStart >= description.Length)
                return description;
            return description.Substring(0, sentenceStart) +
                   ColorAirplaneDescriptionPart(
                       description.Substring(sentenceStart));
        }

        private static bool HasAirplaneDescriptionPart(ModifierId id)
        {
            return id == ModifierId.NoDash ||
                   id == ModifierId.NoMiniPlane ||
                   id == ModifierId.MiniPlaneOnly ||
                   id == ModifierId.NoBombs ||
                   id == ModifierId.NoPeashooter ||
                   id == ModifierId.StiffMode;
        }

        private static int LastSentenceStart(string description)
        {
            var searchBefore = description.Length - 2;
            if (searchBefore < 0)
                return 0;

            var westernStart = description.LastIndexOf(
                ". ", searchBefore, System.StringComparison.Ordinal);
            var fullWidthStart = description.LastIndexOf(
                '。', searchBefore);
            if (fullWidthStart > westernStart)
                return fullWidthStart + 1;
            return westernStart < 0 ? 0 : westernStart + 2;
        }

        private static string ColorAirplaneDescriptionPart(string text)
        {
            return "<color=" + AirplaneDescriptionColor + ">" + text +
                   "</color>";
        }

        private static string SpanishModifierDescription(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return "En combates terrestres, el dash queda bloqueado. En niveles de avión, no puedes transformarte en miniavión.";
                case ModifierId.NoMiniPlane:
                    return "En combates aéreos, no puedes transformarte en miniavión.";
                case ModifierId.MiniPlaneOnly:
                    return "Puedes cambiar de tamaño, pero dañar a un enemigo con un disparo grande, una bomba o un EX reinicia el intento. Los súperes sí están permitidos. Solo funciona en niveles de avión.";
                case ModifierId.NoBombs:
                    return "Solo puedes usar el disparo principal; las bombas quedan bloqueadas. Solo funciona en niveles\nde avión.";
                case ModifierId.NoPeashooter:
                    return "Solo puedes usar bombas; el disparo principal queda bloqueado. Solo funciona en niveles\nde avión.";
                case ModifierId.NoEx:
                    return "Los ataques EX quedan bloqueados; los súperes siguen disponibles.";
                case ModifierId.BlackAndWhite:
                    return "La imagen del combate pasa a blanco y negro; los controles y las colisiones no cambian.";
                case ModifierId.RgbShift:
                    return "La imagen del combate sufre un desfase RGB y un desenfoque pulsante; los controles y las colisiones no cambian.";
                case ModifierId.UpsideDown:
                    return "La imagen del combate gira 180°; los controles, la física y las colisiones no cambian.";
                case ModifierId.HpOne:
                    return "Cada jugador queda limitado a 1 HP; las curaciones y el escudo del Súper II de Ms. Chalice se anulan.";
                case ModifierId.InkRain:
                    return "Caen gotas de tinta. Si tocan a un jugador, manchan y oscurecen la pantalla temporalmente, pero no infligen daño.";
                case ModifierId.HalfDamage:
                    return "Todos tus ataques infligen un 50 % menos de daño; el daño que recibes no cambia.";
                case ModifierId.StiffMode:
                    return "En combates terrestres, el fijado se mantiene mientras tocas el suelo y el dash queda bloqueado; puedes dirigir los saltos. En niveles de avión, no puedes transformarte en miniavión.";
                default:
                    return "No se aplica ningún reto.";
            }
        }

        private static string EnglishModifierDescription(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return "In ground fights, dash is disabled. In airplane levels, mini-plane form is disabled.";
                case ModifierId.NoMiniPlane:
                    return "In airplane fights, you cannot shrink into mini-plane form.";
                case ModifierId.MiniPlaneOnly:
                    return "You may change size, but damaging an enemy with a full-size shot, bomb, or EX restarts the attempt. Supers are allowed. Only works in airplane levels.";
                case ModifierId.NoBombs:
                    return "Only the main shot is available; bombs are locked. Only works in airplane levels.";
                case ModifierId.NoPeashooter:
                    return "Only bombs are available; the main shot is locked. Only works in airplane levels.";
                case ModifierId.NoEx:
                    return "EX attacks are disabled; supers remain available.";
                case ModifierId.BlackAndWhite:
                    return "The fight image fades to black and white; controls and collisions are unchanged.";
                case ModifierId.RgbShift:
                    return "The fight image gains shifting RGB separation and pulsing blur; controls and collisions are unchanged.";
                case ModifierId.UpsideDown:
                    return "The fight image rotates 180°; controls, physics, and collisions are unchanged.";
                case ModifierId.HpOne:
                    return "Each player is capped at 1 HP; healing and Ms. Chalice's Super II shield are negated.";
                case ModifierId.InkRain:
                    return "Ink drops fall. If one touches a player, it splatters and temporarily darkens the screen, but deals no damage.";
                case ModifierId.HalfDamage:
                    return "All your attacks deal 50% less damage; incoming damage is unchanged.";
                case ModifierId.StiffMode:
                    return "In ground levels, lock mode is held while grounded and dash is disabled; you can still steer jumps. In airplane levels, mini-plane form is disabled.";
                default:
                    return "No challenge is applied.";
            }
        }

        private static string FrenchModifierDescription(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return "Dans les combats au sol, le dash est désactivé. Dans les niveaux en avion, le mini-avion est désactivé.";
                case ModifierId.NoMiniPlane:
                    return "Dans les combats aériens, impossible de passer en mini-avion.";
                case ModifierId.MiniPlaneOnly:
                    return "Vous pouvez changer de taille, mais infliger des dégâts avec un tir en taille normale, une bombe ou une attaque EX relance la tentative. Les Super sont autorisés. Fonctionne uniquement dans les niveaux en avion.";
                case ModifierId.NoBombs:
                    return "Seul le tir principal est disponible ; les bombes sont bloquées. Fonctionne uniquement dans les niveaux en avion.";
                case ModifierId.NoPeashooter:
                    return "Seules les bombes sont disponibles ; le tir principal est bloqué. Fonctionne uniquement dans les niveaux en avion.";
                case ModifierId.NoEx:
                    return "Les attaques EX sont bloquées ; les Super restent disponibles.";
                case ModifierId.BlackAndWhite:
                    return "L'image du combat passe en noir et blanc ; commandes et collisions ne changent pas.";
                case ModifierId.RgbShift:
                    return "L'image du combat subit une séparation RGB mouvante et un flou pulsé ; commandes et collisions ne changent pas.";
                case ModifierId.UpsideDown:
                    return "L'image du combat pivote de 180° ; commandes, physique et collisions ne changent pas.";
                case ModifierId.HpOne:
                    return "Chaque joueur est limité à 1 PV ; les soins et le bouclier du Super II de Ms. Chalice sont annulés.";
                case ModifierId.InkRain:
                    return "Des gouttes d'encre tombent. Lorsqu'elles touchent un joueur, elles éclaboussent et assombrissent temporairement l'écran, sans infliger de dégâts.";
                case ModifierId.HalfDamage:
                    return "Toutes vos attaques infligent 50 % de dégâts en moins ; les dégâts subis ne changent pas.";
                case ModifierId.StiffMode:
                    return "Dans les niveaux au sol, le mode de verrouillage reste actif lorsque vous touchez le sol et le dash est désactivé ; vous pouvez encore diriger vos sauts. Dans les niveaux en avion, le mini-avion est désactivé.";
                default:
                    return "Aucun défi n'est appliqué.";
            }
        }

        private static string ItalianModifierDescription(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return "Negli scontri a terra, il dash è disattivato. Nei livelli in aereo, il mini-aereo è disattivato.";
                case ModifierId.NoMiniPlane:
                    return "Negli scontri aerei, non puoi trasformarti in mini-aereo.";
                case ModifierId.MiniPlaneOnly:
                    return "Puoi cambiare dimensione, ma danneggiare un nemico con un colpo a grandezza normale, una bomba o un EX riavvia il tentativo. Gli attacchi Super sono consentiti. Funziona solo nei livelli in aereo.";
                case ModifierId.NoBombs:
                    return "È disponibile solo lo sparo principale; le bombe sono bloccate. Funziona solo nei livelli in aereo.";
                case ModifierId.NoPeashooter:
                    return "Sono disponibili solo le bombe; lo sparo principale è bloccato. Funziona solo nei livelli in aereo.";
                case ModifierId.NoEx:
                    return "Gli attacchi EX sono bloccati; i Super restano disponibili.";
                case ModifierId.BlackAndWhite:
                    return "L'immagine dello scontro diventa monocromatica; comandi e collisioni non cambiano.";
                case ModifierId.RgbShift:
                    return "L'immagine dello scontro presenta uno sfasamento RGB mobile e una sfocatura pulsante; comandi e collisioni non cambiano.";
                case ModifierId.UpsideDown:
                    return "L'immagine dello scontro ruota di 180°; comandi, fisica e collisioni non cambiano.";
                case ModifierId.HpOne:
                    return "Ogni giocatore è limitato a 1 HP; le cure e lo scudo del Super II di Ms. Chalice sono annullati.";
                case ModifierId.InkRain:
                    return "Cadono gocce d'inchiostro. Quando toccano un giocatore, sporcano e oscurano temporaneamente lo schermo senza infliggere danni.";
                case ModifierId.HalfDamage:
                    return "Tutti i tuoi attacchi infliggono il 50% di danni in meno; i danni subiti non cambiano.";
                case ModifierId.StiffMode:
                    return "Nei livelli a terra, la mira fissa resta attiva quando tocchi terra e il dash è disattivato; puoi ancora controllare i salti. Nei livelli in aereo, il mini-aereo è disattivato.";
                default:
                    return "Non viene applicata alcuna sfida.";
            }
        }

        private static string GermanModifierDescription(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return "In Bodenkämpfen ist der Dash deaktiviert. In Flugzeugleveln ist der Miniflieger gesperrt.";
                case ModifierId.NoMiniPlane:
                    return "In Flugzeugkämpfen kannst du dich nicht in den Miniflieger verwandeln.";
                case ModifierId.MiniPlaneOnly:
                    return "Du kannst die Größe wechseln. Ein Treffer mit einem Schuss in Normalgröße, einer Bombe oder einer EX-Attacke startet den Versuch neu. Super-Attacken sind erlaubt. Funktioniert nur in Flugzeugleveln.";
                case ModifierId.NoBombs:
                    return "Nur der Hauptschuss ist verfügbar; Bomben sind gesperrt. Funktioniert nur in Flugzeugleveln.";
                case ModifierId.NoPeashooter:
                    return "Nur Bomben sind verfügbar; der Hauptschuss ist gesperrt. Funktioniert nur in Flugzeugleveln.";
                case ModifierId.NoEx:
                    return "EX-Attacken sind gesperrt; Super-Attacken bleiben verfügbar.";
                case ModifierId.BlackAndWhite:
                    return "Das Kampfbild wird schwarz-weiß; Steuerung und Kollisionen bleiben unverändert.";
                case ModifierId.RgbShift:
                    return "Das Kampfbild erhält einen bewegten RGB-Versatz und pulsierende Unschärfe; Steuerung und Kollisionen bleiben unverändert.";
                case ModifierId.UpsideDown:
                    return "Das Kampfbild dreht sich um 180°; Steuerung, Physik und Kollisionen bleiben unverändert.";
                case ModifierId.HpOne:
                    return "Jeder Spieler ist auf 1 KP begrenzt; Heilung und der Schild von Ms. Chalices Super II werden aufgehoben.";
                case ModifierId.InkRain:
                    return "Tintentropfen fallen herab. Treffen sie einen Spieler, bespritzen und verdunkeln sie kurzzeitig den Bildschirm, verursachen aber keinen Schaden.";
                case ModifierId.HalfDamage:
                    return "Alle deine Angriffe verursachen 50 % weniger Schaden; erlittener Schaden bleibt unverändert.";
                case ModifierId.StiffMode:
                    return "In Bodenleveln bleibt der Feststellmodus am Boden aktiv und der Dash ist gesperrt; Sprünge lassen sich weiter steuern. In Flugzeugleveln ist der Miniflieger gesperrt.";
                default:
                    return "Es wird keine Challenge angewendet.";
            }
        }

        private static string KoreanModifierDescription(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return "지상 전투에서는 대시를 사용할 수 없습니다. 비행기 스테이지에서는 소형 비행기를 사용할 수 없습니다.";
                case ModifierId.NoMiniPlane:
                    return "비행기 전투에서는 소형 비행기로 변신할 수 없습니다.";
                case ModifierId.MiniPlaneOnly:
                    return "크기는 자유롭게 바꿀 수 있지만, 대형 비행기 탄환·폭탄·EX 공격이 적에게 맞으면 전투가 다시 시작됩니다. 필살기는 허용됩니다. 비행기 스테이지에서만 작동합니다.";
                case ModifierId.NoBombs:
                    return "기본 공격만 사용할 수 있으며 폭탄은 잠깁니다. 비행기 스테이지에서만 작동합니다.";
                case ModifierId.NoPeashooter:
                    return "폭탄만 사용할 수 있으며 기본 공격은 잠깁니다. 비행기 스테이지에서만 작동합니다.";
                case ModifierId.NoEx:
                    return "EX 공격은 사용할 수 없지만 필살기는 사용할 수 있습니다.";
                case ModifierId.BlackAndWhite:
                    return "전투 화면이 흑백으로 바뀌며 조작과 충돌 판정은 그대로입니다.";
                case ModifierId.RgbShift:
                    return "전투 화면에 움직이는 RGB 색 분리와 맥동하는 흐림 효과가 적용되며 조작과 충돌 판정은 그대로입니다.";
                case ModifierId.UpsideDown:
                    return "전투 화면이 180° 회전하며 조작, 물리, 충돌 판정은 그대로입니다.";
                case ModifierId.HpOne:
                    return "각 플레이어의 최대 HP가 1로 제한되며 회복과 미스 챌리스의 필살기 II 보호막은 무효화됩니다.";
                case ModifierId.InkRain:
                    return "잉크 방울이 떨어집니다. 플레이어에게 닿으면 피해 없이 화면을 일시적으로 잉크로 얼룩지게 하고 어둡게 만듭니다.";
                case ModifierId.HalfDamage:
                    return "모든 공격의 피해량이 50% 감소하며 받는 피해는 그대로입니다.";
                case ModifierId.StiffMode:
                    return "지상 스테이지에서는 땅에 있을 때 고정 조준 상태가 유지되고 대시를 사용할 수 없지만 점프 중에는 방향을 조절할 수 있습니다. 비행기 스테이지에서는 소형 비행기를 사용할 수 없습니다.";
                default:
                    return "도전이 적용되지 않습니다.";
            }
        }

        private static string RussianModifierDescription(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return "В наземных боях рывок недоступен. На уровнях с самолётом мини-самолёт недоступен.";
                case ModifierId.NoMiniPlane:
                    return "В воздушных боях нельзя превращаться в мини-самолёт.";
                case ModifierId.MiniPlaneOnly:
                    return "Можно менять размер, но попадание по врагу выстрелом большого самолёта, бомбой или EX-атакой перезапускает попытку. Суператаки разрешены. Работает только на уровнях с самолётом.";
                case ModifierId.NoBombs:
                    return "Доступна только основная стрельба; бомбы заблокированы. Работает только на уровнях с самолётом.";
                case ModifierId.NoPeashooter:
                    return "Доступны только бомбы; основная стрельба заблокирована. Работает только на уровнях с самолётом.";
                case ModifierId.NoEx:
                    return "EX-атаки заблокированы; суператаки остаются доступны.";
                case ModifierId.BlackAndWhite:
                    return "Изображение боя становится чёрно-белым; управление и столкновения не меняются.";
                case ModifierId.RgbShift:
                    return "Изображение боя получает движущееся RGB-смещение и пульсирующее размытие; управление и столкновения не меняются.";
                case ModifierId.UpsideDown:
                    return "Изображение боя поворачивается на 180°; управление, физика и столкновения не меняются.";
                case ModifierId.HpOne:
                    return "Максимум каждого игрока — 1 HP; лечение и щит Супера II Мисс Чалис не действуют.";
                case ModifierId.InkRain:
                    return "Сверху падают капли чернил. При касании игрока они временно пачкают и затемняют экран, но не наносят урон.";
                case ModifierId.HalfDamage:
                    return "Все ваши атаки наносят на 50% меньше урона; получаемый урон не меняется.";
                case ModifierId.StiffMode:
                    return "В наземных боях на земле постоянно удерживается режим фиксации и рывок недоступен; в прыжке можно менять направление. На уровнях с самолётом мини-самолёт недоступен.";
                default:
                    return "Испытание не применяется.";
            }
        }

        private static string PolishModifierDescription(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return "W walkach naziemnych dash jest wyłączony. Na poziomach samolotowych mały samolot jest zablokowany.";
                case ModifierId.NoMiniPlane:
                    return "W walkach powietrznych nie możesz zmienić się w mały samolot.";
                case ModifierId.MiniPlaneOnly:
                    return "Możesz zmieniać rozmiar, ale trafienie wroga pociskiem dużego samolotu, bombą lub atakiem EX rozpoczyna próbę od nowa. Superataki są dozwolone. Działa tylko na poziomach samolotowych.";
                case ModifierId.NoBombs:
                    return "Dostępny jest tylko główny strzał; bomby są zablokowane. Działa tylko na poziomach samolotowych.";
                case ModifierId.NoPeashooter:
                    return "Dostępne są tylko bomby; główny strzał jest zablokowany. Działa tylko na poziomach samolotowych.";
                case ModifierId.NoEx:
                    return "Ataki EX są zablokowane; superataki nadal są dostępne.";
                case ModifierId.BlackAndWhite:
                    return "Obraz walki staje się czarno-biały; sterowanie i kolizje pozostają bez zmian.";
                case ModifierId.RgbShift:
                    return "Obraz walki zyskuje ruchome przesunięcie RGB i pulsujące rozmycie; sterowanie i kolizje pozostają bez zmian.";
                case ModifierId.UpsideDown:
                    return "Obraz walki obraca się o 180°; sterowanie, fizyka i kolizje pozostają bez zmian.";
                case ModifierId.HpOne:
                    return "Każdy gracz ma maksymalnie 1 HP; leczenie i tarcza Super II Ms. Chalice zostają anulowane.";
                case ModifierId.InkRain:
                    return "Spadają krople atramentu. Po dotknięciu gracza chwilowo plamią i przyciemniają ekran, ale nie zadają obrażeń.";
                case ModifierId.HalfDamage:
                    return "Wszystkie twoje ataki zadają o 50% mniej obrażeń; otrzymywane obrażenia pozostają bez zmian.";
                case ModifierId.StiffMode:
                    return "W walkach naziemnych na ziemi stale działa tryb zablokowanego celowania i dash jest wyłączony; podczas skoku nadal możesz sterować kierunkiem. Na poziomach samolotowych mały samolot jest zablokowany.";
                default:
                    return "Nie jest nakładane żadne wyzwanie.";
            }
        }

        private static string PortugueseModifierDescription(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return "Em batalhas terrestres, o dash fica desativado. Em fases de avião, o miniavião fica bloqueado.";
                case ModifierId.NoMiniPlane:
                    return "Em batalhas de avião, você não pode se transformar em miniavião.";
                case ModifierId.MiniPlaneOnly:
                    return "Você pode mudar de tamanho, mas acertar um inimigo com um tiro em tamanho normal, uma bomba ou um EX reinicia a tentativa. Supers são permitidos. Só funciona em fases de avião.";
                case ModifierId.NoBombs:
                    return "Só o tiro principal fica disponível; as bombas ficam bloqueadas. Só funciona em fases de avião.";
                case ModifierId.NoPeashooter:
                    return "Só as bombas ficam disponíveis; o tiro principal fica bloqueado. Só funciona em fases de avião.";
                case ModifierId.NoEx:
                    return "Ataques EX ficam bloqueados; os Supers continuam disponíveis.";
                case ModifierId.BlackAndWhite:
                    return "A imagem da batalha fica em preto e branco; controles e colisões não mudam.";
                case ModifierId.RgbShift:
                    return "A imagem da batalha ganha separação RGB em movimento e desfoque pulsante; controles e colisões não mudam.";
                case ModifierId.UpsideDown:
                    return "A imagem da batalha gira 180°; controles, física e colisões não mudam.";
                case ModifierId.HpOne:
                    return "Cada jogador fica limitado a 1 HP; curas e o escudo do Super II da Ms. Chalice são anulados.";
                case ModifierId.InkRain:
                    return "Gotas de tinta caem; ao tocar um jogador, mancham e escurecem a tela temporariamente, sem causar dano.";
                case ModifierId.HalfDamage:
                    return "Todos os seus ataques causam 50% menos dano; o dano recebido não muda.";
                case ModifierId.StiffMode:
                    return "Em fases terrestres, a mira fica travada enquanto você está no chão e o dash é bloqueado; ainda é possível controlar os saltos. Em fases de avião, o miniavião fica bloqueado.";
                default:
                    return "Nenhum desafio é aplicado.";
            }
        }

        private static string JapaneseModifierDescription(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return "地上戦ではダッシュできません。飛行機ステージではミニ化できません。";
                case ModifierId.NoMiniPlane:
                    return "飛行機戦ではミニ飛行機に変身できません。";
                case ModifierId.MiniPlaneOnly:
                    return "自由に大きさを変えられますが、通常サイズの弾、爆弾、EX攻撃が敵に当たるとバトルがリスタートします。必殺技は使用できます。飛行機ステージでのみ有効です。";
                case ModifierId.NoBombs:
                    return "通常ショットのみ使用でき、爆弾は使用できません。飛行機ステージでのみ有効です。";
                case ModifierId.NoPeashooter:
                    return "爆弾のみ使用でき、通常ショットは使用できません。飛行機ステージでのみ有効です。";
                case ModifierId.NoEx:
                    return "EX攻撃は使用できませんが、必殺技は使用できます。";
                case ModifierId.BlackAndWhite:
                    return "バトル画面がモノクロになります。操作と当たり判定は変わりません。";
                case ModifierId.RgbShift:
                    return "バトル画面に動くRGBずれと脈打つぼかしがかかります。操作と当たり判定は変わりません。";
                case ModifierId.UpsideDown:
                    return "バトル画面が180°回転します。操作、物理、当たり判定は変わりません。";
                case ModifierId.HpOne:
                    return "各プレイヤーの最大HPは1です。回復とミス・チャリスの必殺技IIのシールドは無効になります。";
                case ModifierId.InkRain:
                    return "インクのしずくが降ります。プレイヤーに触れるとダメージは与えず、一時的に画面を汚して暗くします。";
                case ModifierId.HalfDamage:
                    return "すべての攻撃のダメージが50%減少します。受けるダメージは変わりません。";
                case ModifierId.StiffMode:
                    return "地上ステージでは地上にいる間ロック状態が維持され、ダッシュできません。ジャンプ中は方向を調整できます。飛行機ステージではミニ化できません。";
                default:
                    return "チャレンジは適用されません。";
            }
        }

        private static string ChineseModifierDescription(ModifierId id)
        {
            switch (id)
            {
                case ModifierId.NoDash:
                    return "在地面战中无法冲刺。在飞机关卡中无法变成小飞机。";
                case ModifierId.NoMiniPlane:
                    return "在飞机战中无法变成小飞机。";
                case ModifierId.MiniPlaneOnly:
                    return "可以自由改变大小，但普通形态的子弹、炸弹或 EX 攻击命中敌人后会重新开始本次战斗。必杀技可以使用。仅在飞机关卡中生效。";
                case ModifierId.NoBombs:
                    return "只能使用主射击；炸弹会被锁定。仅在飞机关卡中生效。";
                case ModifierId.NoPeashooter:
                    return "只能使用炸弹；主射击会被锁定。仅在飞机关卡中生效。";
                case ModifierId.NoEx:
                    return "EX 攻击会被禁用；必杀技仍可使用。";
                case ModifierId.BlackAndWhite:
                    return "战斗画面会变成黑白；操作和碰撞判定不变。";
                case ModifierId.RgbShift:
                    return "战斗画面会出现动态 RGB 色差和脉冲模糊；操作和碰撞判定不变。";
                case ModifierId.UpsideDown:
                    return "战斗画面会旋转 180°；操作、物理和碰撞判定不变。";
                case ModifierId.HpOne:
                    return "每位玩家的最大 HP 限制为 1；治疗和圣杯小姐必杀技 II 的护盾无效。";
                case ModifierId.InkRain:
                    return "墨滴会从空中落下；碰到玩家时会暂时弄脏并压暗屏幕，但不会造成伤害。";
                case ModifierId.HalfDamage:
                    return "你的所有攻击造成的伤害降低 50%；受到的伤害不变。";
                case ModifierId.StiffMode:
                    return "在地面关卡中，落地时会一直保持锁定瞄准状态，且无法冲刺；跳跃时仍可控制方向。在飞机关卡中无法变成小飞机。";
                default:
                    return "不应用任何挑战。";
            }
        }
    }
}
