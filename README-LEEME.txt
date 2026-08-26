LA PICHI RULETA 0.6.0

ENGLISH

A boss roulette directly inside Cuphead.

Spin the roulette and let it choose your next boss, weapons, Super, Charm and optional challenge.

INSTALLATION

1) Close Cuphead. Extract the ZIP directly into the folder where Cuphead.exe is located and replace the mod files when asked.
2) Start Cuphead normally.
3) Load a save file.
4) From the world map, press F6 to open the roulette.

That's it!

HOW TO USE

Open the roulette from the world map and choose your options.

When you spin, the roulette can randomly select:

• Boss
• Weapons
• Super
• Charm
• Optional Challenge
• Difficulty

Once the result is ready, the mod can automatically equip everything and take you directly into the selected battle.

Automatic battle loading can also be disabled if you prefer to review the result or spin again first.

Your original equipment is restored after completing or leaving the fight.

CONTROLS

Keyboard

• F6 — Open / close the roulette
• Arrow Keys — Navigate and change options
• Enter — Confirm
• Escape — Close
• F7 — Spin again when automatic battle loading is disabled

Controller

• Left Trigger + Equip Card button — Open / close the roulette
• Equip without Left Trigger keeps its normal native behavior
• Once open, use your controller normally to navigate and select options
• Right Trigger — Spin again when automatic battle loading is disabled

CHALLENGES

The roulette currently includes:

• No Dash
• No Mini-Plane
• Mini-Plane Only
• No Mini-Bombs (airplane levels)
• No Peashooter (airplane levels)
• No EX
• Black & White
• Chromatic Chaos
• Upside Down
• 1 HP. Deal With It.
• Ink Rain
• Damage -50%
• Locked Mode

Available challenges automatically change depending on whether the selected boss is a ground or airplane battle.

Locked Mode behaves like holding Cuphead's native Lock button while grounded and also blocks dashing. You can still jump and steer in the air. In King Dice's airplane rooms, it adapts by blocking mini-plane transformation.

CREATOR TOOLS / OBS

From the world map, open Pause > LA PICHI RULETA > STREAM OVERLAY. Enable it, choose COPY URL, and paste the address into an OBS Browser Source. A source size of 1080 × 400 is recommended. The background is already transparent.

PREVIEW temporarily displays the overlay so you can position it and check your settings without starting a battle.

RETRY controls what happens to the result when you retry a battle:

• KEEP leaves the current result visible.
• REAPPEAR plays the result entrance again when the new attempt begins.

LOGO shows the LA PICHI RULETA logo when there is no active battle result.

The local server always uses port 18081 and never switches to another port. If another application is already using it, CONTROL PANEL opens a local warning page with instructions. Close the application using the port, return to the game and select CONTROL PANEL again. The server remains available for CONTROL PANEL when STATUS is disabled; that setting only hides or shows the OBS source content. The overlay does not require an internet connection. Size, order, alignment, opacity and logo settings update live.

STREAM RULES can exchange exact TikTok gifts for catalog interactions. They now appear directly below the catalog without an extra tab, while REDEEMS IN PROGRESS appears in the Dashboard above the real-time feed. Keep TikFinity open on the same computer; the mod starts its hidden companion automatically and reports the local connection in the Dashboard. You never need to open the companion yourself. Creating or editing a rule is confirmed even while Cuphead is unfocused, without advancing gameplay in the background. When a gift creates an interaction, its bundled image appears in-game beside the donor name at 80% transparency; nothing is downloaded during the fight.

TIKTOK LIVE STUDIO

TikTok Live Studio does not accept local addresses such as localhost or 127.0.0.1 directly, so a local hostname must be configured to use the overlay.

1) Press the Windows key.
2) Type PowerShell.
3) Right-click Windows PowerShell and select Run as administrator.
4) Run this command:

notepad C:\Windows\System32\drivers\etc\hosts

5) Add this line at the end of the file:

127.0.0.1 ruleta.test

6) Save the file and close Notepad.

Then use COPY URL from Creator Tools and replace only localhost or 127.0.0.1 with ruleta.test.

For example:

http://127.0.0.1:18081/

becomes:

http://ruleta.test:18081/

Always keep port 18081 in the address.

FEATURES

• Supports Cuphead's native Simple, Regular and Expert difficulties
• Single-player and local co-op support
• Full keyboard and controller support
• Automatic or manual battle loading
• Works with or without The Delicious Last Course DLC
• Supports all 12 languages available in Cuphead
• Local OBS overlay with live Creator Tools settings
• Creator Tools catalog interactions work in roulette and normally entered gameplay levels
• Automatic local TikFinity connection with configurable TikTok gift rules
• Automatically restores your previous equipment after the fight

FEEDBACK

Feedback is especially welcome about:

• Ideas for new challenges
• Controller support
• Local co-op / 2-player behavior
• Translations

If you speak any of the supported languages and something sounds unnatural, unclear or different from the terminology normally used by the Cuphead community, please let me know.

Bug reports and other suggestions are welcome too!


ESPAÑOL

Una ruleta de jefes directamente dentro de Cuphead.

Gira la ruleta y deja que elija tu próximo jefe, armas, Super, Amuleto y un reto opcional.

INSTALACIÓN

1) Cierra Cuphead. Extrae el ZIP directamente en la carpeta donde se encuentra Cuphead.exe y reemplaza los archivos del mod cuando se te pregunte.
2) Inicia Cuphead normalmente.
3) Carga una partida.
4) Desde el mapa, presiona F6 para abrir la ruleta.

¡Eso es todo!

CÓMO USARLA

Abre la ruleta desde el mapa y elige tus opciones.

Al girarla, la ruleta puede elegir aleatoriamente:

• Jefe
• Armas
• Super
• Amuleto
• Reto opcional
• Dificultad

Cuando el resultado esté listo, el mod puede equipar todo automáticamente y llevarte directamente a la batalla seleccionada.

También puedes desactivar la entrada automática a la batalla si prefieres revisar el resultado o volver a girar antes de entrar.

Tu equipamiento original se restaura después de completar o abandonar la batalla.

CONTROLES

Teclado

• F6 — Abrir / cerrar la ruleta
• Flechas — Navegar y cambiar opciones
• Enter — Confirmar
• Escape — Cerrar
• F7 — Volver a girar cuando la entrada automática a la batalla está desactivada

Mando

• Gatillo izquierdo + botón de la tarjeta de equipamiento — Abrir / cerrar la ruleta
• Equip sin gatillo izquierdo conserva su funcionamiento nativo
• Una vez abierta, usa el mando normalmente para navegar y seleccionar las opciones
• Gatillo derecho — Volver a girar cuando la entrada automática está desactivada

RETOS

Actualmente la ruleta incluye:

• No Dash
• No miniavión
• Solo balas de miniavión
• No disparo bombas (niveles de avión)
• Sin Peashooter (niveles de avión)
• Sin EX
• Blanco y negro
• Mamá escucho borroso
• Volteada de cabeza
• Una vida y te callas
• Lluvia de tinta
• Disparos rebajados
• Modo Tieso

Los retos disponibles cambian automáticamente dependiendo de si el jefe seleccionado es una batalla terrestre o un nivel de avión.

Modo Tieso se comporta como si mantuvieras pulsado el fijado nativo de Cuphead mientras estás en el suelo y también bloquea el dash. Todavía puedes saltar y dirigir el movimiento en el aire. En las salas aéreas de Rey Dado, se adapta bloqueando la transformación en miniavión.

CREATOR TOOLS / OBS

Desde el mapa abre Pausa > LA PICHI RULETA > STREAM OVERLAY. Actívalo, elige COPIAR URL y pega la dirección en una Fuente de navegador de OBS. Se recomienda un tamaño de 1080 × 400 para la fuente. El fondo ya es transparente.

VISTA PREVIA muestra temporalmente el overlay para que puedas colocarlo y comprobar tus ajustes sin iniciar una batalla.

REINTENTO controla qué ocurre con el resultado cuando vuelves a intentar una batalla:

• MANTENER deja el resultado actual visible.
• REAPARECER vuelve a reproducir la entrada del resultado al comenzar el nuevo intento.

LOGO muestra el logo de LA PICHI RULETA cuando no hay un resultado de batalla activo.

El servidor local siempre usa el puerto 18081 y nunca cambia a otro. Si otra aplicación ya lo está usando, PANEL DE CONTROL abre una página local con instrucciones. Cierra la aplicación que usa el puerto, vuelve al juego y selecciona PANEL DE CONTROL otra vez. El servidor permanece disponible para PANEL DE CONTROL cuando ESTADO está desactivado; ese ajuste solo oculta o muestra el contenido de la fuente de OBS. El overlay no requiere conexión a internet. Los ajustes de tamaño, orden, alineación, opacidad y logo se actualizan en vivo.

REGLAS DE STREAM permite intercambiar regalos exactos de TikTok por interacciones del catálogo. Ahora aparecen directamente debajo del catálogo, sin una pestaña adicional, mientras CANJEOS EN CURSO vive en el Dashboard encima del feed de tiempo real. Mantén TikFinity abierto en el mismo equipo; el mod inicia su acompañante oculto automáticamente y muestra la conexión local en el Dashboard. Nunca necesitas abrir el acompañante por tu cuenta. Crear o editar una regla se confirma aunque Cuphead esté desenfocado, sin hacer avanzar la partida en segundo plano. Cuando un regalo crea una interacción, su imagen incluida aparece dentro del juego junto al nombre del donador con 80 % de transparencia; no se descarga nada durante el combate.

TIKTOK LIVE STUDIO

TikTok Live Studio no acepta directamente direcciones locales como localhost o 127.0.0.1, por lo que es necesario configurar un nombre local para usar el overlay.

1) Presiona la tecla Windows.
2) Escribe PowerShell.
3) Haz clic derecho en Windows PowerShell y selecciona Ejecutar como administrador.
4) Ejecuta este comando:

notepad C:\Windows\System32\drivers\etc\hosts

5) Al final del archivo agrega esta línea:

127.0.0.1 ruleta.test

6) Guarda el archivo y cierra el Bloc de notas.

Después usa COPIAR URL desde Creator Tools y sustituye únicamente localhost o 127.0.0.1 por ruleta.test.

Por ejemplo:

http://127.0.0.1:18081/

se convierte en:

http://ruleta.test:18081/

Conserva siempre el puerto 18081 en la dirección.

CARACTERÍSTICAS

• Compatible con las dificultades nativas Simple, Regular y Experto
• Compatible con un jugador y cooperativo local
• Compatibilidad completa con teclado y mando
• Entrada automática o manual a las batallas
• Funciona con o sin el DLC The Delicious Last Course
• Compatible con los 12 idiomas disponibles en Cuphead
• Overlay local para OBS con ajustes en vivo desde Creator Tools
• Interacciones del catálogo en la ruleta y en niveles iniciados normalmente
• Conexión local automática con TikFinity y reglas configurables por regalo de TikTok
• Restaura automáticamente tu equipamiento anterior después de la batalla

FEEDBACK

Me interesa especialmente recibir comentarios sobre:

• Ideas para nuevos retos
• Funcionamiento con mando
• Funcionamiento en cooperativo local / 2 jugadores
• Traducciones

Si hablas alguno de los idiomas compatibles y encuentras algo que suena poco natural, no se entiende bien o no coincide con la terminología que normalmente utiliza la comunidad de Cuphead en tu idioma, házmelo saber.

¡Los reportes de errores y cualquier otra sugerencia también son bienvenidos!

¡Disfruten, cochos!
