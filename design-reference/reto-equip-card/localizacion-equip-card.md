# Equip Card de retos: traspaso de localización

## Estado y alcance

Este documento registra la copia efectiva que usa actualmente la Equip Card de
retos. Las traducciones son **provisionales y están pendientes de revisión
humana**. Su finalidad es que otro agente pueda sustituirlas sin alterar el
comportamiento, el orden ni el diseño de la pantalla.

El alcance se limita a:

- la cabecera de la ranura de reto;
- el texto que representa una ranura sin reto en el frente de la tarjeta;
- los títulos de los 12 retos visibles;
- las descripciones de esos 12 retos.

## Leyenda: qué se traduce y qué no

| Elemento | Estado | Instrucción |
|---|---|---|
| Valor de `SlotChallenge` | **TRADUCIBLE** | Usar el equivalente breve de “Reto/Challenge”. |
| Valor de `EquipEmpty` | **TRADUCIBLE** | Usar el equivalente de “Vacío/Empty”. No crea una opción visible en la cuadrícula. |
| Título de cada reto | **TRADUCIBLE** | Puede adaptarse al tono de Cuphead y a la terminología oficial del idioma. |
| Descripción de cada reto | **TRADUCIBLE** | Debe conservar exactamente el significado jugable. |
| `ModifierId`, `ModText` y nombres de claves | **NO TRADUCIBLE** | Son identificadores de código. |
| Orden, cantidad y distribución 5-4-3 | **NO CAMBIAR** | Son parte del contrato de navegación y diseño. |
| Reglas jugables y compatibilidad tierra/avión | **NO CAMBIAR** | Sólo se revisa la redacción. |
| `#C94F2D` y etiquetas TMP `<color>` | **NO TRADUCIBLE** | El código añade el color; no se escriben etiquetas dentro de la traducción. |
| `\n` | **AJUSTE DE DISEÑO** | No es texto. Sólo debe añadirse, quitarse o moverse después de probar el idioma dentro del juego. |
| `HP`, `EX`, `RGB`, `Super II` y `180°` | **TÉRMINOS JUGABLES** | Pueden adaptarse a la terminología oficial del juego, sin cambiar la mecánica ni el valor. |
| Texto pintado dentro de PNG | **NO LOCALIZABLE POR CÓDIGO** | Para traducirlo se necesitan assets separados por idioma. |

## Contrato que el agente no debe alterar

Los IDs y este orden son inmutables para el trabajo de localización:

1. `BlackAndWhite`
2. `RgbShift`
3. `InkRain`
4. `UpsideDown`
5. `HpOne`
6. `StiffMode`
7. `HalfDamage`
8. `NoDash`
9. `NoEx`
10. `MiniPlaneOnly`
11. `NoBombs`
12. `NoPeashooter`

Reglas adicionales:

- `NoMiniPlane` es una regla interna de compatibilidad. No debe volver a
  agregarse como reto visible.
- `ModifierId.None` y `ChallengeNone` tampoco son opciones del selector.
- No se muestra una opción **Vacío** dentro de la cuadrícula. La acción nativa
  de desequipar sigue existiendo y `EquipEmpty` sólo representa la ranura vacía
  en el frente de la tarjeta.
- No se muestra el antiguo rótulo `AMBOS/AVIÓN`. `ModifierKindName()` puede
  seguir existiendo en el código, pero no pertenece a esta pantalla.
- `StiffMode` y `NoDash` bloquean el miniavión cuando se usan en niveles de
  avión. Los tres últimos retos sólo funcionan en niveles de avión.

Los apodos de trabajo “Trippy”, “Flip”, “Locked” y “50%” no son IDs y no deben
introducirse como claves nuevas.

## Reglas de renderizado

- El selector convierte los títulos a mayúsculas mediante
  `ToUpperInvariant()`. El traductor puede escribirlos con capitalización
  natural; la pantalla seguirá presentándolos en mayúsculas cuando corresponda.
- La cabecera `SlotChallenge` se muestra con sólo la inicial en mayúscula
  mediante `EquipCardLabel`. Para inglés, `Challenge` también está fijado como
  caso especial en `ManualChallengeEquipCard.cs`; si se cambia la cabecera
  inglesa, hay que actualizar ambos lugares.
- El título usa ajuste automático con mínimo de 12 puntos. La descripción usa
  rich text, ajuste de línea y tamaño automático entre 10 y 20 puntos. Toda
  traducción necesita revisión visual dentro del juego.
- La última oración de `StiffMode`, `NoDash`, `MiniPlaneOnly`, `NoBombs` y
  `NoPeashooter` se pinta automáticamente en naranja rojizo (`#C94F2D`). Debe
  seguir siendo una oración final independiente.
- El detector de esa última oración reconoce `. ` y `。`. Si un idioma usa otro
  separador, hay que ampliar `LastSentenceStart()` o el color no se aplicará
  correctamente.
- Actualmente sólo el español fuerza un salto de línea, en `NoBombs` y
  `NoPeashooter`: `Solo funciona en niveles\nde avión.`. Es un ajuste visual,
  no parte del significado.
- Los iconos animados pueden contener palabras pintadas en el bitmap. Esas
  palabras no cambian al modificar las cadenas de localización.

## Copia efectiva actual

En los bloques siguientes, **todos los valores entre comillas son
TRADUCIBLES**. Las claves a la izquierda del signo `=` y los IDs entre
corchetes son **NO TRADUCIBLES**. Los valores corresponden al resultado final
después de aplicar `ModLocalization.LabelReview.cs`.

### Español (España) — `SpanishSpain`

```text
SlotChallenge = "RETO"
EquipEmpty = "Vacío"

[BlackAndWhite]
Title = "BLANCO Y NEGRO"
Description = "La imagen del combate pasa a blanco y negro; los controles y las colisiones no cambian."

[RgbShift]
Title = "Mamá escucho borroso"
Description = "La imagen del combate sufre un desfase RGB y un desenfoque pulsante; los controles y las colisiones no cambian."

[InkRain]
Title = "Lluvia de tinta"
Description = "Caen gotas de tinta. Si tocan a un jugador, manchan y oscurecen la pantalla temporalmente, pero no infligen daño."

[UpsideDown]
Title = "Volteada de cabeza"
Description = "La imagen del combate gira 180°; los controles, la física y las colisiones no cambian."

[HpOne]
Title = "Una vida y te callas"
Description = "Cada jugador queda limitado a 1 HP; las curaciones y el escudo del Súper II de Ms. Chalice se anulan."

[StiffMode]
Title = "Modo tieso"
Description = "En combates terrestres, el fijado se mantiene mientras tocas el suelo y el dash queda bloqueado; puedes dirigir los saltos. En niveles de avión, no puedes transformarte en miniavión."

[HalfDamage]
Title = "Disparos rebajados"
Description = "Todos tus ataques infligen un 50 % menos de daño; el daño que recibes no cambia."

[NoDash]
Title = "NO DASH"
Description = "En combates terrestres, el dash queda bloqueado. En niveles de avión, no puedes transformarte en miniavión."

[NoEx]
Title = "NO EX"
Description = "Los ataques EX quedan bloqueados; los súperes siguen disponibles."

[MiniPlaneOnly]
Title = "SOLO BALAS DE MINIAVIÓN"
Description = "Puedes cambiar de tamaño, pero dañar a un enemigo con un disparo grande, una bomba o un EX reinicia el intento. Los súperes sí están permitidos. Solo funciona en niveles de avión."

[NoBombs]
Title = "NO DISPARO BOMBAS"
Description = "Solo puedes usar el disparo principal; las bombas quedan bloqueadas. Solo funciona en niveles\nde avión."

[NoPeashooter]
Title = "SIN DISPARO NORMAL"
Description = "Solo puedes usar bombas; el disparo principal queda bloqueado. Solo funciona en niveles\nde avión."
```

### Español (América) — `SpanishAmerica`

Esta variante usa exactamente la misma cabecera, texto vacío, títulos y
descripciones que `SpanishSpain`, salvo este título:

```text
[NoPeashooter]
Title = "SIN PEASHOOTER"
```

### Inglés — `English`

```text
SlotChallenge = "CHALLENGE"
EquipEmpty = "Empty"

[BlackAndWhite]
Title = "BLACK & WHITE"
Description = "The fight image fades to black and white; controls and collisions are unchanged."

[RgbShift]
Title = "Chromatic Chaos"
Description = "The fight image gains shifting RGB separation and pulsing blur; controls and collisions are unchanged."

[InkRain]
Title = "Ink Rain"
Description = "Ink drops fall. If one touches a player, it splatters and temporarily darkens the screen, but deals no damage."

[UpsideDown]
Title = "Upside Down"
Description = "The fight image rotates 180°; controls, physics, and collisions are unchanged."

[HpOne]
Title = "1 HP. Deal With It."
Description = "Each player is capped at 1 HP; healing and Ms. Chalice's Super II shield are negated."

[StiffMode]
Title = "Locked Mode"
Description = "In ground levels, lock mode is held while grounded and dash is disabled; you can still steer jumps. In airplane levels, mini-plane form is disabled."

[HalfDamage]
Title = "Damage -50%"
Description = "All your attacks deal 50% less damage; incoming damage is unchanged."

[NoDash]
Title = "NO DASH"
Description = "In ground fights, dash is disabled. In airplane levels, mini-plane form is disabled."

[NoEx]
Title = "NO EX"
Description = "EX attacks are disabled; supers remain available."

[MiniPlaneOnly]
Title = "MINI-PLANE ONLY"
Description = "You may change size, but damaging an enemy with a full-size shot, bomb, or EX restarts the attempt. Supers are allowed. Only works in airplane levels."

[NoBombs]
Title = "NO MINI-BOMBS"
Description = "Only the main shot is available; bombs are locked. Only works in airplane levels."

[NoPeashooter]
Title = "NO PEASHOOTER"
Description = "Only bombs are available; the main shot is locked. Only works in airplane levels."
```

### Francés — `French`

```text
SlotChallenge = "DÉFI"
EquipEmpty = "Vide"

[BlackAndWhite]
Title = "NOIR ET BLANC"
Description = "L'image du combat passe en noir et blanc ; commandes et collisions ne changent pas."

[RgbShift]
Title = "Chaos chromatique"
Description = "L'image du combat subit une séparation RGB mouvante et un flou pulsé ; commandes et collisions ne changent pas."

[InkRain]
Title = "Pluie d'encre"
Description = "Des gouttes d'encre tombent. Lorsqu'elles touchent un joueur, elles éclaboussent et assombrissent temporairement l'écran, sans infliger de dégâts."

[UpsideDown]
Title = "À l'envers"
Description = "L'image du combat pivote de 180° ; commandes, physique et collisions ne changent pas."

[HpOne]
Title = "1 PV. Fais avec."
Description = "Chaque joueur est limité à 1 PV ; les soins et le bouclier du Super II de Ms. Chalice sont annulés."

[StiffMode]
Title = "Verrouillé"
Description = "Dans les niveaux au sol, le mode de verrouillage reste actif lorsque vous touchez le sol et le dash est désactivé ; vous pouvez encore diriger vos sauts. Dans les niveaux en avion, le mini-avion est désactivé."

[HalfDamage]
Title = "Dégâts -50 %"
Description = "Toutes vos attaques infligent 50 % de dégâts en moins ; les dégâts subis ne changent pas."

[NoDash]
Title = "SANS DASH"
Description = "Dans les combats au sol, le dash est désactivé. Dans les niveaux en avion, le mini-avion est désactivé."

[NoEx]
Title = "SANS EX"
Description = "Les attaques EX sont bloquées ; les Super restent disponibles."

[MiniPlaneOnly]
Title = "MINI-AVION UNIQUEMENT"
Description = "Vous pouvez changer de taille, mais infliger des dégâts avec un tir en taille normale, une bombe ou une attaque EX relance la tentative. Les Super sont autorisés. Fonctionne uniquement dans les niveaux en avion."

[NoBombs]
Title = "SANS MINI-BOMBES"
Description = "Seul le tir principal est disponible ; les bombes sont bloquées. Fonctionne uniquement dans les niveaux en avion."

[NoPeashooter]
Title = "SANS TIR PRINCIPAL"
Description = "Seules les bombes sont disponibles ; le tir principal est bloqué. Fonctionne uniquement dans les niveaux en avion."
```

### Italiano — `Italian`

```text
SlotChallenge = "SFIDA"
EquipEmpty = "Vuoto"

[BlackAndWhite]
Title = "MONOCROMO"
Description = "L'immagine dello scontro diventa monocromatica; comandi e collisioni non cambiano."

[RgbShift]
Title = "Caos cromatico"
Description = "L'immagine dello scontro presenta uno sfasamento RGB mobile e una sfocatura pulsante; comandi e collisioni non cambiano."

[InkRain]
Title = "Pioggia d'inchiostro"
Description = "Cadono gocce d'inchiostro. Quando toccano un giocatore, sporcano e oscurano temporaneamente lo schermo senza infliggere danni."

[UpsideDown]
Title = "Sottosopra"
Description = "L'immagine dello scontro ruota di 180°; comandi, fisica e collisioni non cambiano."

[HpOne]
Title = "1 HP. Fattene una ragione."
Description = "Ogni giocatore è limitato a 1 HP; le cure e lo scudo del Super II di Ms. Chalice sono annullati."

[StiffMode]
Title = "Bloccato"
Description = "Nei livelli a terra, la mira fissa resta attiva quando tocchi terra e il dash è disattivato; puoi ancora controllare i salti. Nei livelli in aereo, il mini-aereo è disattivato."

[HalfDamage]
Title = "Danni -50%"
Description = "Tutti i tuoi attacchi infliggono il 50% di danni in meno; i danni subiti non cambiano."

[NoDash]
Title = "SENZA DASH"
Description = "Negli scontri a terra, il dash è disattivato. Nei livelli in aereo, il mini-aereo è disattivato."

[NoEx]
Title = "SENZA EX"
Description = "Gli attacchi EX sono bloccati; i Super restano disponibili."

[MiniPlaneOnly]
Title = "SOLO MINI-AEREO"
Description = "Puoi cambiare dimensione, ma danneggiare un nemico con un colpo a grandezza normale, una bomba o un EX riavvia il tentativo. Gli attacchi Super sono consentiti. Funziona solo nei livelli in aereo."

[NoBombs]
Title = "SENZA MINI BOMBE"
Description = "È disponibile solo lo sparo principale; le bombe sono bloccate. Funziona solo nei livelli in aereo."

[NoPeashooter]
Title = "SENZA MITRAGLIATRICE"
Description = "Sono disponibili solo le bombe; lo sparo principale è bloccato. Funziona solo nei livelli in aereo."
```

### Alemán — `German`

```text
SlotChallenge = "CHALLENGE"
EquipEmpty = "Leer"

[BlackAndWhite]
Title = "SCHWARZ-WEISS"
Description = "Das Kampfbild wird schwarz-weiß; Steuerung und Kollisionen bleiben unverändert."

[RgbShift]
Title = "Chromatisches Chaos"
Description = "Das Kampfbild erhält einen bewegten RGB-Versatz und pulsierende Unschärfe; Steuerung und Kollisionen bleiben unverändert."

[InkRain]
Title = "Tintenregen"
Description = "Tintentropfen fallen herab. Treffen sie einen Spieler, bespritzen und verdunkeln sie kurzzeitig den Bildschirm, verursachen aber keinen Schaden."

[UpsideDown]
Title = "Kopfüber"
Description = "Das Kampfbild dreht sich um 180°; Steuerung, Physik und Kollisionen bleiben unverändert."

[HpOne]
Title = "1 KP. Find dich damit ab."
Description = "Jeder Spieler ist auf 1 KP begrenzt; Heilung und der Schild von Ms. Chalices Super II werden aufgehoben."

[StiffMode]
Title = "Festgesetzt"
Description = "In Bodenleveln bleibt der Feststellmodus am Boden aktiv und der Dash ist gesperrt; Sprünge lassen sich weiter steuern. In Flugzeugleveln ist der Miniflieger gesperrt."

[HalfDamage]
Title = "Schaden -50 %"
Description = "Alle deine Angriffe verursachen 50 % weniger Schaden; erlittener Schaden bleibt unverändert."

[NoDash]
Title = "OHNE DASH"
Description = "In Bodenkämpfen ist der Dash deaktiviert. In Flugzeugleveln ist der Miniflieger gesperrt."

[NoEx]
Title = "OHNE EX"
Description = "EX-Attacken sind gesperrt; Super-Attacken bleiben verfügbar."

[MiniPlaneOnly]
Title = "NUR MINIFLUGZEUG"
Description = "Du kannst die Größe wechseln. Ein Treffer mit einem Schuss in Normalgröße, einer Bombe oder einer EX-Attacke startet den Versuch neu. Super-Attacken sind erlaubt. Funktioniert nur in Flugzeugleveln."

[NoBombs]
Title = "OHNE MINIBOMBEN"
Description = "Nur der Hauptschuss ist verfügbar; Bomben sind gesperrt. Funktioniert nur in Flugzeugleveln."

[NoPeashooter]
Title = "OHNE MASCHINENGEWEHR"
Description = "Nur Bomben sind verfügbar; der Hauptschuss ist gesperrt. Funktioniert nur in Flugzeugleveln."
```

### Coreano — `Korean`

```text
SlotChallenge = "도전"
EquipEmpty = "비어 있음"

[BlackAndWhite]
Title = "흑백"
Description = "전투 화면이 흑백으로 바뀌며 조작과 충돌 판정은 그대로입니다."

[RgbShift]
Title = "색채 혼돈"
Description = "전투 화면에 움직이는 RGB 색 분리와 맥동하는 흐림 효과가 적용되며 조작과 충돌 판정은 그대로입니다."

[InkRain]
Title = "잉크 비"
Description = "잉크 방울이 떨어집니다. 플레이어에게 닿으면 피해 없이 화면을 일시적으로 잉크로 얼룩지게 하고 어둡게 만듭니다."

[UpsideDown]
Title = "거꾸로"
Description = "전투 화면이 180° 회전하며 조작, 물리, 충돌 판정은 그대로입니다."

[HpOne]
Title = "HP 1. 알아서 버텨."
Description = "각 플레이어의 최대 HP가 1로 제한되며 회복과 미스 챌리스의 필살기 II 보호막은 무효화됩니다."

[StiffMode]
Title = "고정"
Description = "지상 스테이지에서는 땅에 있을 때 고정 조준 상태가 유지되고 대시를 사용할 수 없지만 점프 중에는 방향을 조절할 수 있습니다. 비행기 스테이지에서는 소형 비행기를 사용할 수 없습니다."

[HalfDamage]
Title = "피해량 -50%"
Description = "모든 공격의 피해량이 50% 감소하며 받는 피해는 그대로입니다."

[NoDash]
Title = "대시 금지"
Description = "지상 전투에서는 대시를 사용할 수 없습니다. 비행기 스테이지에서는 소형 비행기를 사용할 수 없습니다."

[NoEx]
Title = "EX 공격 금지"
Description = "EX 공격은 사용할 수 없지만 필살기는 사용할 수 있습니다."

[MiniPlaneOnly]
Title = "소형 비행기 총알만"
Description = "크기는 자유롭게 바꿀 수 있지만, 대형 비행기 탄환·폭탄·EX 공격이 적에게 맞으면 전투가 다시 시작됩니다. 필살기는 허용됩니다. 비행기 스테이지에서만 작동합니다."

[NoBombs]
Title = "소형 폭탄 금지"
Description = "기본 공격만 사용할 수 있으며 폭탄은 잠깁니다. 비행기 스테이지에서만 작동합니다."

[NoPeashooter]
Title = "기본 공격 금지"
Description = "폭탄만 사용할 수 있으며 기본 공격은 잠깁니다. 비행기 스테이지에서만 작동합니다."
```

### Ruso — `Russian`

```text
SlotChallenge = "ИСПЫТАНИЕ"
EquipEmpty = "Пусто"

[BlackAndWhite]
Title = "ЧЕРНО-БЕЛЫЙ"
Description = "Изображение боя становится чёрно-белым; управление и столкновения не меняются."

[RgbShift]
Title = "Хроматический хаос"
Description = "Изображение боя получает движущееся RGB-смещение и пульсирующее размытие; управление и столкновения не меняются."

[InkRain]
Title = "Чернильный дождь"
Description = "Сверху падают капли чернил. При касании игрока они временно пачкают и затемняют экран, но не наносят урон."

[UpsideDown]
Title = "Вверх дном"
Description = "Изображение боя поворачивается на 180°; управление, физика и столкновения не меняются."

[HpOne]
Title = "1 HP. Смирись."
Description = "Максимум каждого игрока — 1 HP; лечение и щит Супера II Мисс Чалис не действуют."

[StiffMode]
Title = "Зафиксирован"
Description = "В наземных боях на земле постоянно удерживается режим фиксации и рывок недоступен; в прыжке можно менять направление. На уровнях с самолётом мини-самолёт недоступен."

[HalfDamage]
Title = "Урон -50%"
Description = "Все ваши атаки наносят на 50% меньше урона; получаемый урон не меняется."

[NoDash]
Title = "БЕЗ РЫВКА"
Description = "В наземных боях рывок недоступен. На уровнях с самолётом мини-самолёт недоступен."

[NoEx]
Title = "БЕЗ EX"
Description = "EX-атаки заблокированы; суператаки остаются доступны."

[MiniPlaneOnly]
Title = "ТОЛЬКО МИНИ-ПУЛИ"
Description = "Можно менять размер, но попадание по врагу выстрелом большого самолёта, бомбой или EX-атакой перезапускает попытку. Суператаки разрешены. Работает только на уровнях с самолётом."

[NoBombs]
Title = "БЕЗ МИНИ-БОМБ"
Description = "Доступна только основная стрельба; бомбы заблокированы. Работает только на уровнях с самолётом."

[NoPeashooter]
Title = "БЕЗ ОБЫЧНОГО ВЫСТРЕЛА"
Description = "Доступны только бомбы; основная стрельба заблокирована. Работает только на уровнях с самолётом."
```

### Polaco — `Polish`

```text
SlotChallenge = "WYZWANIE"
EquipEmpty = "Pusto"

[BlackAndWhite]
Title = "CZARNO-BIAŁY"
Description = "Obraz walki staje się czarno-biały; sterowanie i kolizje pozostają bez zmian."

[RgbShift]
Title = "Chromatyczny chaos"
Description = "Obraz walki zyskuje ruchome przesunięcie RGB i pulsujące rozmycie; sterowanie i kolizje pozostają bez zmian."

[InkRain]
Title = "Deszcz atramentu"
Description = "Spadają krople atramentu. Po dotknięciu gracza chwilowo plamią i przyciemniają ekran, ale nie zadają obrażeń."

[UpsideDown]
Title = "Do góry nogami"
Description = "Obraz walki obraca się o 180°; sterowanie, fizyka i kolizje pozostają bez zmian."

[HpOne]
Title = "1 HP. Pogódź się z tym."
Description = "Każdy gracz ma maksymalnie 1 HP; leczenie i tarcza Super II Ms. Chalice zostają anulowane."

[StiffMode]
Title = "Zablokowany"
Description = "W walkach naziemnych na ziemi stale działa tryb zablokowanego celowania i dash jest wyłączony; podczas skoku nadal możesz sterować kierunkiem. Na poziomach samolotowych mały samolot jest zablokowany."

[HalfDamage]
Title = "Obrażenia -50%"
Description = "Wszystkie twoje ataki zadają o 50% mniej obrażeń; otrzymywane obrażenia pozostają bez zmian."

[NoDash]
Title = "BEZ DASHA"
Description = "W walkach naziemnych dash jest wyłączony. Na poziomach samolotowych mały samolot jest zablokowany."

[NoEx]
Title = "BEZ EX"
Description = "Ataki EX są zablokowane; superataki nadal są dostępne."

[MiniPlaneOnly]
Title = "TYLKO MAŁY SAMOLOT"
Description = "Możesz zmieniać rozmiar, ale trafienie wroga pociskiem dużego samolotu, bombą lub atakiem EX rozpoczyna próbę od nowa. Superataki są dozwolone. Działa tylko na poziomach samolotowych."

[NoBombs]
Title = "BEZ BOMB"
Description = "Dostępny jest tylko główny strzał; bomby są zablokowane. Działa tylko na poziomach samolotowych."

[NoPeashooter]
Title = "BEZ DZIAŁKA"
Description = "Dostępne są tylko bomby; główny strzał jest zablokowany. Działa tylko na poziomach samolotowych."
```

### Portugués (Brasil) — `PortugueseBrazil`

```text
SlotChallenge = "DESAFIO"
EquipEmpty = "Vazio"

[BlackAndWhite]
Title = "PRETO E BRANCO"
Description = "A imagem da batalha fica em preto e branco; controles e colisões não mudam."

[RgbShift]
Title = "Caos cromático"
Description = "A imagem da batalha ganha separação RGB em movimento e desfoque pulsante; controles e colisões não mudam."

[InkRain]
Title = "Chuva de tinta"
Description = "Gotas de tinta caem; ao tocar um jogador, mancham e escurecem a tela temporariamente, sem causar dano."

[UpsideDown]
Title = "De cabeça para baixo"
Description = "A imagem da batalha gira 180°; controles, física e colisões não mudam."

[HpOne]
Title = "1 HP. Se vira."
Description = "Cada jogador fica limitado a 1 HP; curas e o escudo do Super II da Ms. Chalice são anulados."

[StiffMode]
Title = "Travado"
Description = "Em fases terrestres, a mira fica travada enquanto você está no chão e o dash é bloqueado; ainda é possível controlar os saltos. Em fases de avião, o miniavião fica bloqueado."

[HalfDamage]
Title = "Dano -50%"
Description = "Todos os seus ataques causam 50% menos dano; o dano recebido não muda."

[NoDash]
Title = "SEM DASH"
Description = "Em batalhas terrestres, o dash fica desativado. Em fases de avião, o miniavião fica bloqueado."

[NoEx]
Title = "SEM EX"
Description = "Ataques EX ficam bloqueados; os Supers continuam disponíveis."

[MiniPlaneOnly]
Title = "SÓ MINIAVIÃO"
Description = "Você pode mudar de tamanho, mas acertar um inimigo com um tiro em tamanho normal, uma bomba ou um EX reinicia a tentativa. Supers são permitidos. Só funciona em fases de avião."

[NoBombs]
Title = "SEM MINIBOMBAS"
Description = "Só o tiro principal fica disponível; as bombas ficam bloqueadas. Só funciona em fases de avião."

[NoPeashooter]
Title = "SEM METRALHADORA"
Description = "Só as bombas ficam disponíveis; o tiro principal fica bloqueado. Só funciona em fases de avião."
```

### Japonés — `Japanese`

```text
SlotChallenge = "チャレンジ"
EquipEmpty = "空"

[BlackAndWhite]
Title = "モノクロ"
Description = "バトル画面がモノクロになります。操作と当たり判定は変わりません。"

[RgbShift]
Title = "色彩の混沌"
Description = "バトル画面に動くRGBずれと脈打つぼかしがかかります。操作と当たり判定は変わりません。"

[InkRain]
Title = "インクの雨"
Description = "インクのしずくが降ります。プレイヤーに触れるとダメージは与えず、一時的に画面を汚して暗くします。"

[UpsideDown]
Title = "逆さま"
Description = "バトル画面が180°回転します。操作、物理、当たり判定は変わりません。"

[HpOne]
Title = "HP1。諦めろ。"
Description = "各プレイヤーの最大HPは1です。回復とミス・チャリスの必殺技IIのシールドは無効になります。"

[StiffMode]
Title = "固定"
Description = "地上ステージでは地上にいる間ロック状態が維持され、ダッシュできません。ジャンプ中は方向を調整できます。飛行機ステージではミニ化できません。"

[HalfDamage]
Title = "ダメージ -50%"
Description = "すべての攻撃のダメージが50%減少します。受けるダメージは変わりません。"

[NoDash]
Title = "ダッシュ禁止"
Description = "地上戦ではダッシュできません。飛行機ステージではミニ化できません。"

[NoEx]
Title = "EXショット禁止"
Description = "EX攻撃は使用できませんが、必殺技は使用できます。"

[MiniPlaneOnly]
Title = "ミニショットのみ"
Description = "自由に大きさを変えられますが、通常サイズの弾、爆弾、EX攻撃が敵に当たるとバトルがリスタートします。必殺技は使用できます。飛行機ステージでのみ有効です。"

[NoBombs]
Title = "ミニボム禁止"
Description = "通常ショットのみ使用でき、爆弾は使用できません。飛行機ステージでのみ有効です。"

[NoPeashooter]
Title = "通常ショット禁止"
Description = "爆弾のみ使用でき、通常ショットは使用できません。飛行機ステージでのみ有効です。"
```

### Chino simplificado — `SimplifiedChinese`

```text
SlotChallenge = "挑战"
EquipEmpty = "空"

[BlackAndWhite]
Title = "黑白"
Description = "战斗画面会变成黑白；操作和碰撞判定不变。"

[RgbShift]
Title = "色彩混沌"
Description = "战斗画面会出现动态 RGB 色差和脉冲模糊；操作和碰撞判定不变。"

[InkRain]
Title = "墨水雨"
Description = "墨滴会从空中落下；碰到玩家时会暂时弄脏并压暗屏幕，但不会造成伤害。"

[UpsideDown]
Title = "上下颠倒"
Description = "战斗画面会旋转 180°；操作、物理和碰撞判定不变。"

[HpOne]
Title = "1 HP。认了吧。"
Description = "每位玩家的最大 HP 限制为 1；治疗和圣杯小姐必杀技 II 的护盾无效。"

[StiffMode]
Title = "锁定"
Description = "在地面关卡中，落地时会一直保持锁定瞄准状态，且无法冲刺；跳跃时仍可控制方向。在飞机关卡中无法变成小飞机。"

[HalfDamage]
Title = "伤害 -50%"
Description = "你的所有攻击造成的伤害降低 50%；受到的伤害不变。"

[NoDash]
Title = "禁止冲刺"
Description = "在地面战中无法冲刺。在飞机关卡中无法变成小飞机。"

[NoEx]
Title = "禁止EX攻击"
Description = "EX 攻击会被禁用；必杀技仍可使用。"

[MiniPlaneOnly]
Title = "仅限小飞机子弹"
Description = "可以自由改变大小，但普通形态的子弹、炸弹或 EX 攻击命中敌人后会重新开始本次战斗。必杀技可以使用。仅在飞机关卡中生效。"

[NoBombs]
Title = "禁止迷你炸弹"
Description = "只能使用主射击；炸弹会被锁定。仅在飞机关卡中生效。"

[NoPeashooter]
Title = "禁止普通射击"
Description = "只能使用炸弹；主射击会被锁定。仅在飞机关卡中生效。"
```

## Archivos que debe editar el siguiente agente

- `ModLocalization.cs`: `SlotChallenge`, `EquipEmpty` y títulos base.
- `ModLocalization.LabelReview.cs`: sobrescribe los títulos efectivos de
  `RgbShift`, `UpsideDown`, `HpOne`, `InkRain`, `HalfDamage` y `StiffMode`.
- `ModLocalization.ChallengeDescriptions.cs`: todas las descripciones y la
  lógica del fragmento naranja/rojo.
- `ManualChallengeEquipCard.cs`: orden, mayúsculas, tamaños, distribución y
  caso especial de la cabecera inglesa.
- `RouletteData.cs`: compatibilidad e IDs; no debe cambiarse sólo para revisar
  traducciones.

## Checklist para recibir una revisión

1. Entregar cada idioma con las mismas claves e IDs de este documento.
2. Confirmar explícitamente que cada descripción conserva la regla jugable.
3. Mantener la cláusula de avión como última oración en los cinco retos que la
   usan.
4. Marcar cualquier salto de línea deseado como una decisión de maquetación.
5. Probar los 12 títulos y descripciones en la Equip Card, incluidos alfabetos
   no latinos.
6. Comprobar que el texto naranja/rojo, el ajuste automático y la navegación
   siguen funcionando.
