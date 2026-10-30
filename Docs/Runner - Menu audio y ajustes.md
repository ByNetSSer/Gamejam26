# Menú, audio y ajustes

Implementación inicial, 2026-10-30. Revisión visual y pruebas interactivas en Play Mode pendientes.

## Entrada al juego

Abrir `Assets/Scenes/MainMenu.unity`. Jugar carga `Assets/Scenes/e.unity`, que contiene la torre actual. Ambas están habilitadas en Build Profiles, con MainMenu primero; se conservan Gameplay y SampleScene. El menú incluye Opciones, Créditos y Salir. Salir detiene Play Mode en el editor y cierra la aplicación compilada.

`RunEndScreen` escucha los eventos de victoria y derrota del gestor en `e`: detiene el juego y muestra puntos, volver a jugar y regresar al menú. El botón final de la torre 12 provoca la victoria; llegar a cero de vida provoca derrota. Las pantallas son provisionales y necesitan prueba interactiva en Play Mode.

La presentación usa el título provisional «LA TORRE», fondo oscuro y acentos dorados. Editar los textos y nombres del equipo en la escena; los créditos indican que faltan atribuciones y no inventan autores. `MainMenuController` en Content permite cambiar la escena de destino, la música y el clic. `Tools > Runner > Crear menu de juego` crea la escena si no existe y conserva las escenas abiertas. No sobrescribe un menú existente.

## Ajustes

General (master), Música y Efectos usan valores de 0 a 1. El volumen efectivo es master × categoría × volumen del cue. Sensibilidad es un multiplicador de 0.1 a 3, con 1 como valor original; se aplica después de las estadísticas de cámara, conservando efectos y penalización por velocidad.

Los valores se escriben en PlayerPrefs al cambiarlos y se guardan en disco al cerrar opciones, iniciar partida, salir, perder foco o pausar la aplicación. No se escribe a disco en cada paso del slider. Claves: `MenuUIBase.Audio.Master`, `MenuUIBase.Audio.Music`, `MenuUIBase.Audio.SoundEffects`, `Game.Settings.MouseSensitivity`. No se borran preferencias ajenas.

## Añadir audios

1. Importar clips en `Assets/Proyect/Assets/Audio/Music` o `Effects`.
2. Seleccionar los assets preparados y asignar `Clip`: MenuMusic, GameplayMusic, UIClick, PlayerLand, ImpactBreak y PlayerDamage. Están vacíos intencionalmente; no hay sonidos de prueba sintetizados en el menú real.
3. MenuMusic y UIClick ya están referenciados por MainMenu. Para música de partida añadir `AudioCuePlayer` a un objeto de Gameplay, asignar GameplayMusic y marcar `Play On Enable`.
4. Para efectos, llamar `AudioCuePlayer.Play()` desde UnityEvents/feedbacks, o `AudioManager.Instance.Play(cue)` desde código. Activar `Spatial Sound` para reproducir en la posición del objeto; la fuente persistente permite terminar un efecto aunque se destruya una sala.
5. `GameAudioBank.asset` organiza IDs (`music.menu`, `music.gameplay`, `ui.click`, `player.land`, `impact.break`, `player.damage`). Referenciar el banco y llamar `bank.Play(id)` o `bank.PlayAt(id, position)`. Los SFX de gameplay se conectan mediante `GameSfx`; ver el mapa de integración al final.

`AudioManager` persiste entre escenas y precalienta 24 voces de efectos. Reutiliza voces libres; aplica límites por cue y, al saturarse, reemplaza la voz menos prioritaria y más antigua si el nuevo sonido tiene prioridad suficiente, sin crear objetos por impacto. `Sound Pool Size` permite 4–64 voces en un manager colocado en la escena inicial. Música usa dos fuentes independientes con fundido cruzado en tiempo no escalado. Los efectos son one-shot; Loop se aplica a música. `StopSoundEffects()` corta todas las voces. Los volúmenes se aplican también a sonidos que ya están reproduciéndose.

Esto es un pool de fuentes, no precarga en RAM de todos los clips. La carga/streaming de cada clip se configura en su importador. No usar el pool de one-shots para ambientes infinitos.

El master controla el audio de este manager. AudioSources externos y audio FEEL deben reproducirse a través de este servicio para respetarlo; todavía no hay AudioMixer global ni puente automático para FEEL. No se cambió el render ni los feedbacks existentes de Flupin.

## Validación manual

- Abrir MainMenu y comprobar los cuatro botones, navegación por teclado y regreso desde cada panel.
- Cambiar los tres volúmenes y sensibilidad; cerrar y volver a ejecutar para comprobar persistencia.
- Jugar debe cargar e y capturar el cursor; comprobar que sensibilidad 1 mantiene el movimiento previo.
- Agotar la vida y, en otra partida, activar el botón de victoria: comprobar la pantalla, el cursor, volver a jugar y regresar al menú en ambos casos.
- Asignar clips y lanzar más de 24 efectos: la jerarquía del manager debe mantener 24 voces. Cambiar el volumen durante su reproducción y probar master 0.
- Probar interrupción de fades y destrucción de objetos emisores; los efectos deben terminar desde el manager.
- Completar créditos antes de publicar. La pantalla de resultado es provisional; el menú no añade pausa al gameplay.


## Tiempo y pantalla de muerte (2026-10-30)

`RoomManager.Tower Time Seconds` establece el límite por torre (180 s provisionales). La primera torre no tiene cuenta atrás ni reloj visible. Desde la segunda, se reinicia al comenzar cada torre y solo cuenta en salas intermedias: la inicial y la final son seguras. Entrar en la final detiene el reloj de inmediato, sin esperar a aterrizar; también se detiene al perder, ganar o fallar la generación. Las transiciones se resuelven antes de comprobar el vencimiento para evitar una muerte por tiempo al entrar en la zona segura en ese mismo fotograma. Usa tiempo escalado: pausa y ralentización de parry afectan la cuenta. `RunEndScreen` muestra minutos/segundos y un aviso rojo persistente durante los últimos 60 segundos. El reloj baja desde fuera de pantalla al comenzar las salas intermedias y sube para ocultarse al entrar en la sala final. Ambos movimientos usan FEEL 5.5 (`MMF_Position`, espacio RectTransform, tiempo no escalado), con `Clock Slide Duration` ajustable; se interrumpen desde la posición actual si cambia el estado. La segunda sala cuenta para el reloj aunque esté protegida de enemigos por otras reglas.

Al llegar a cero se publica `RunLostEvent` con `TimeExpired`, se bloquean movimiento/cámara y se pausa la partida. La presentación usa tiempo no escalado: oscurecimiento, calavera pixelada construida con UI (escala y shake ajustable), y fade de causa, consejo, puntuación y botones Reiniciar/Menú principal. `Darkness Duration` y `Skull Shake` se ajustan en RunEndScreen; shake 0 desactiva la sacudida. Los botones se habilitan al terminar el fade. Reiniciar carga de nuevo la escena de partida desde la primera torre.

`PlayerHealth.TakeDamage(amount, reason)` conserva la causa del golpe mortal antes de emitir Died. Flupin, Dontsee, MaskCharger, RoomCrusher y superficies peligrosas envían su causa; las llamadas sin causa conservan un mensaje genérico. Las superficies dañinas actuales se describen como peligros/lava; añadir una causa específica si se incorporan otros tipos.

Compilación runtime comprobada con un proyecto temporal que incluye los scripts actuales (sin errores ni advertencias). Pendiente validación visual e interactiva en Unity: límite de 65 s para cruzar el aviso, límite de 3 s para probar la derrota, cada golpe mortal, reinicio/menú, muerte durante parry, finalización antes de cero y restablecimiento del tiempo en la siguiente torre. Restaurar 180 s después de las pruebas. La calavera es provisional; su sonido se añadió en la integración de SFX descrita abajo.

Validación adicional pendiente en Play Mode: primera torre completa sin reloj; esperar en la inicial de la segunda sin perder tiempo; salir a una sala intermedia y comprobar la bajada; entrar en la final con poco tiempo y comprobar pausa inmediata y subida; siguiente torre con tiempo restablecido y reloj oculto.

## SFX importados e integración (2026-10-30)

`GameSfx` carga `Audio/Resources/GameSfx.asset`, que referencia el banco existente. Funciona al abrir directamente una escena de juego y en builds. Todos los efectos pasan por AudioManager y respetan master y efectos; la interfaz usa 2D y los enemigos usan 3D con seguimiento. Cada cue permite ajustar su volumen en `Audio/Effects`. Se limitan repeticiones simultáneas del mismo ID (50 ms por defecto), especialmente al romper varios objetos juntos.

| Clip | Disparador |
| --- | --- |
| jump | Salto ejecutado, incluido buffer/coyote |
| TakeDamage | Daño aceptado; intervalo mínimo de 120 ms |
| Parry | Repulsión exitosa de enemigo |
| FlupiIsParreable | Flupin entra en disponibilidad de repulsión; mínimo 300 ms |
| DontSeeAparing | Dontsee se activa y muestra su visual |
| MaskStartAtack | Inicio del aviso de ataque de máscara |
| MaskAiming | Mitad del aviso, antes de embestir |
| MaskEmbestida | Inicio de la embestida |
| AddComboPointRegisterlog | Entrada de puntos en el registro del combo |
| UpTierScore | Sube el rango mostrado por el HUD; usa sus umbrales configurados |
| BreakCombo | Combo expira y se liquida |
| Coinsound | Liquidación de combo con monedas positivas |
| ClockTick | Una vez por segundo del último minuto, solo con reloj activo y tiempo avanzando |
| DeathSoundBreaker | Transición tras morir: suena mientras se atenúan los efectos del juego y la música |
| Deathscrean | Aparición de la calavera, tras el oscurecimiento |
| OnBrea | Ambiente 3D de la sala de brea, incluso saltando; permanece atrás al salir y se atenúa por distancia |

`BreakComboFortakeDamage` (`combo.damage`) suena al perder un combo activo por daño aceptado. Se liquidan sus puntos en monedas con la conversión habitual, se conserva el total de puntos y se reinician contador/multiplicador. El HUD muestra el cierre por daño y las monedas recibidas. Un combo ya vencido se liquida antes de procesar la pérdida. `Lightning Strike` (`lightning.strike`) queda disponible como efecto de trueno independiente mediante `AudioCuePlayer` o `GameAudioBank.Play`, sin asociarlo a ataques ni inventar una frecuencia ambiental. Música, clic UI, rotura de objetos y aterrizaje siguen sin clips asignados.

Validación: compilación de runtime con todos los scripts actuales mediante proyecto temporal, sin errores ni advertencias; comprobados IDs usados y GUID de clips. Pendiente escuchar y balancear en Play Mode, comprobar silenciamiento con master/efectos a cero, secuencia de máscara y muerte, reloj en salas seguras y reinicio. El daño ahora rompe y pierde el combo activo, según confirmación del usuario.

### Corrección de significado de los clips

Confirmado por el usuario: OnBrea corresponde al contacto con brea, DeathSoundBreaker es un puente entre gameplay y pantalla de muerte, y Lightning Strike es un trueno. `ImpactBreak` vuelve a quedar vacío; `TarSurface` usa OnBrea. `GroundSurface.Tar Surface` identifica explícitamente la superficie; las maldiciones de brea y el creador de escena de prueba lo configuran automáticamente. Se usa una fuente de bucle dedicada, independiente del pool, compartida por el jugador local; el salto, salida, desactivación del sensor y cambio de escena la detienen.

Al morir, las voces que estaban reproduciéndose y el bucle de brea se atenúan durante `Darkness Duration` usando tiempo no escalado, junto con el fade de música. DeathSoundBreaker entra sin esa atenuación; la calavera y Deathscrean esperan al mayor entre esa duración y la longitud del clip de transición. El cambio de escena corta SFX pendientes. Los ajustes del usuario no se sobrescriben. Esta transición cubre AudioManager; fuentes externas/FEEL siguen fuera de ese control.

Validación de esta corrección: compilación de los scripts runtime actuales y MenuUIBase sin errores ni advertencias. Cinco comprobaciones ejecutadas sobre PlayerScore/ComboState con un arnés sin motor: pérdida sin monedas, ausencia de eventos duplicados con combo vacío, liquidación por vencimiento, conservación de puntos/monedas anteriores y reinicio del multiplicador. Pendiente Play Mode para escuchar el bucle, la transición completa y comprobar los volúmenes.

## Profundidad y concurrencia de SFX

Dontsee, Flupin y las tres fases de máscara usan `GameSfx.PlayFollowing(id, transform)`. La fuente del pool sigue al emisor en LateUpdate; se libera al terminar o se detiene si el emisor desaparece/se desactiva. Parry usa `GameSfx.PlayAt(id, impactPosition)` y termina en el lugar del impacto. Los seis clips se importan como mono para localizarlos en 3D. UI, combo, reloj y muerte permanecen en 2D. Todo pasa por el canal SoundEffects y master; música usa su canal separado.

Atenuación lineal configurable por AudioCue: volumen completo hasta 2 unidades y silencio a 30; Dontsee usa 45 para su aviso lejano. Paneo según posición relativa al AudioListener, sin Doppler para no alterar el tono en movimientos rápidos. No se añadió oclusión de paredes ni un espacializador HRTF: validar localización/distance con auriculares en Play Mode.

Pool precreado de 24 voces (configurable 4–64), sin instanciar fuentes por solicitud. Una voz terminada queda disponible y se reinicializan sus parámetros al reutilizarla. API reutilizable: `AudioManager.Play(cue)` en 2D, `PlayAt(cue, position)` para posición fija y `PlayFollowing(cue, transform)` para un emisor móvil; también expuestas en IAudioService. AudioCuePlayer ofrece Spatial Sound y Follow Emitter.

Cada cue configura Cooldown, Max Instances y Priority (menor número = mayor prioridad). Los límites de instancias son globales por cue; cooldown es por cue/emisor entre las solicitudes recientes conservadas en el pool. Para posiciones fijas se comparte el emisor 0. Combo, reloj y monedas admiten una instancia por cue. Máscara comparte Exclusive Group `mask.attack`: una fase sustituye a la anterior en la misma máscara, sin cortar otras máscaras. Muerte tiene prioridad 40, avisos enemigos/parry 70, SFX generales 128 y registro/reloj 180. DroppedSoundRequests permite observar solicitudes descartadas.

No se encolan avisos de combate: sonar después del ataque sería engañoso. Los duplicados se limitan y, al llenarse el pool, una solicitud menos importante se descarta. Esto reduce el solapamiento excesivo, sin imponer silencio entre sucesos distintos que deben oírse a la vez. Música y bucle de superficie mantienen fuentes dedicadas.

Compilación de runtime y MenuUIBase actuales comprobada. Pendiente prueba audible: rodear Dontsee, seguir una embestida, comparar cerca/lejos, dos máscaras simultáneas, ráfaga de puntos, más de 24 solicitudes, desaparición de emisor, muerte/reinicio y master/SFX a cero. Ajustar distancias y mezcla tras escucharlo.

## Suavizado de SFX y música

AudioManager incorpora `Sfx Attack` de 0.015 s y `Sfx Release` de 0.04 s. La envolvente suaviza entrada y final natural de one-shots. Al sustituir una voz o desaparecer su emisor, conserva hasta cuatro colas de salida en fuentes precreadas: el nuevo sonido entra mientras el anterior se apaga desde su muestra/posición actual. La capacidad es 24 voces principales más 4 colas breves; si las cuatro están ocupadas, el reemplazo adicional se corta sin reservar más memoria. Los cambios de escena y StopSoundEffects mantienen corte inmediato para limpiar todo. El bucle de brea conserva su gestión independiente.

Música: dos fuentes y fundido cruzado SmoothStep, con duración del AudioCue. MenuMusic y GameplayMusic quedan configurados a 1.2 s (todavía necesitan clips). PlayMusic sobre la misma canción no reinicia su reproducción. Interrumpir una transición conserva las ganancias actuales; una tercera canción libera la fuente más silenciosa en hasta 40 ms antes de reutilizarla. StopMusic atenúa ambas fuentes. Master y categorías siguen aplicándose a las dos canciones, voces y colas; no se modifican preferencias.

Validación: compilación de runtime/MenuUIBase sin errores ni advertencias. Pendiente escucha en Unity: reemplazo de fases de máscara, ráfagas que agoten colas, canciones A→B→C, volver a A durante el cruce, detener durante el cruce y master/SFX/música a cero. Los fundidos se actualizan por frame con tiempo no escalado; no constituyen automatización DSP precisa a nivel de muestra.

## Ambiente por sala y señales de superficies (revisión vigente)

Esta revisión sustituye el comportamiento anterior de OnBrea por contacto. Al activarse la maldición de brea, RoomCurseDirector solicita un bucle 3D en el centro de los bounds de los suelos. TarSurface usa volumen 0.25, distancia mínima 4 y máxima 40. No sigue al jugador ni se interrumpe por saltar/salir del suelo. Al pasar de sala permanece en la anterior, que ya conserva RoomManager; se atenúa por distancia y se retira suavemente al desactivarse/destruirse esa sala. Se reservan tres fuentes de ambiente reutilizables, separadas de one-shots/colas y bajo volumen SFX/master. Muerte retira los ambientes; cambio de escena los limpia. La API reutilizable es AudioManager.PlayRoomLoop(cue, roomTransform, position). Las superficies de brea aisladas del playground no crean ambiente de sala automáticamente.

IceFreeze (`surface.ice.freeze`) se solicita al comenzar la transformación de una sala en hielo. LavaEnter y LavaExit (`surface.lava.enter/exit`) se solicitan al empezar/dejar el contacto con lava: GroundSurfaceSensor agrega los contactos, evitando avisos repetidos al caminar entre colliders contiguos. Saltar fuera de lava cuenta como salir; volver a caer cuenta como entrar. No se reproduce salida por desactivar el sensor. Se conservan el daño y su intervalo existentes.

GroundSurface.AudioKind identifica Normal/Ice/Tar/Lava; las maldiciones y el creador de pruebas lo configuran. El prefab Hielo también queda marcado. GroundSurfaceSensor expone FootstepSurface, FootstepCueId y PlayFootstep() para el futuro sistema de cadencia/animación de pasos. Hay cues FootstepNormal/Ice/Tar/Lava; no se genera todavía una cadencia de pasos.

El usuario confirmó que faltan los clips: IceFreeze, LavaEnter, LavaExit y los cuatro Footstep tienen Clip vacío intencionalmente, con eventos y banco preparados. Asignarlos en Audio/Effects cuando estén disponibles. Sin inventar sonidos sustitutivos. OnBrea ya usa su clip importado como mono.

Validación: compilación de runtime y MenuUIBase actuales sin errores ni advertencias. Pendiente Play Mode: saltar en brea sin interrumpir ambiente, alejarse por puerta/trampilla y oírlo atrás, retirada de sala anterior, dos salas de brea, muerte/reinicio; cuando lleguen clips, hielo una vez por transformación, entrada/salida lava sin spam en juntas y selección de pasos.

## Música por listas y prioridad (implementación vigente)

Se asignaron `MusicsMPT/MenuSong.mp3` a MenuMusic y `MusicsMPT/Song1.mp3` a GameplayMusic. `Audio/Resources/Music` contiene MenuPlaylist, CalmPlaylist, IntensePlaylist y LastMinutePlaylist, además de GameplayMusicSettings. Las dos últimas listas están vacías hasta añadir sus canciones; no se cambia artificialmente el tono o la velocidad de Song1. Sin canciones intensas/urgentes se mantiene la base válida.

MusicPlaylist contiene AudioCues de canal Music y Crossfade Seconds (4 por defecto). Selecciona aleatoriamente una canción distinta de la anterior si existe alternativa, deduplicando por AudioClip y omitiendo entradas vacías/SFX. Con una sola canción vuelve al principio mediante las dos fuentes del fundido cruzado. No usa AudioSource.loop para estas listas. El cambio empieza al quedar 4 s; para clips de menos de 8 s, el margen se limita a la mitad de su duración. Usa posición real de reproducción, no un temporizador de duración estimada. Las voces son reutilizables, no se instancia una fuente por canción.

MainMenuController inicia MenuPlaylist y la transición a gameplay conserva la canción saliente para cruzarla con la nueva. RoomManager añade GameplayMusicDirector, por lo que también funciona entrando directamente al gameplay. Cuenta las salas visitadas durante toda la partida, incluyendo inicial/final; a partir de la tercera (Intense From Room configurable) selecciona IntensePlaylist si tiene canciones. No se reinicia el contador entre torres. Al reiniciar partida sí se reinicia.

Mientras TimerRunning y TimeRemaining <= Urgent Seconds (60), solicita LastMinutePlaylist con prioridad 100. Al entrar en zona segura o cambiar de torre se retira la petición y vuelve a la base calmada/intensa vigente. La primera torre no tiene reloj activo, por lo que no solicita música urgente. Derrota, victoria y fallo de generación suspenden la reproducción automática y atenúan música; no vuelve a arrancar sobre la pantalla final.

API: `MusicPlaylistPlayer.Instance.SetBase(playlist)` actualiza la base; `RequestOverride("clave", playlist, priority)` solicita música temporal; `ClearOverride("clave")` la retira. En estas peticiones, mayor prioridad gana y, a igualdad, gana la petición más reciente. No se apilan melodías indefinidamente: la prioridad reemplaza la selección por fundido cruzado. Solo se superponen las dos canciones durante la transición. La política de listas vive separada de las fuentes de AudioManager.

Master × Música × volumen del cue se aplica a ambas fuentes durante el cruce; SFX mantiene su categoría. Se conservan preferencias y opciones existentes del menú. No se añadió HUD de volumen ni menú de pausa. Configurar listas, umbrales y transición desde assets del inspector; crear nuevos AudioCues de canal Music y añadirlos a Tracks.

Validación: compilación runtime y MenuUIBase actuales sin errores ni advertencias. Arnés de selección con cinco casos: lista vacía, una canción, 1000 selecciones sin repetición con alternativas, cues duplicados del mismo clip y descarte de SFX/clips vacíos. Pendiente escucha en Unity: ciclo de una canción, varias canciones, menú→partida, tercera sala, último minuto, zona segura, muerte y reinicio, cambios repetidos de prioridad y volúmenes en pleno cruce.

### Regla de música para salas seguras

La secuencia vigente es MenuPlaylist en menú → SafeRoomsPlaylist en sala inicial → Calm/IntensePlaylist en salas intermedias → SafeRoomsPlaylist en sala final/tienda. GameplayMusicSettings.Safe Rooms referencia la nueva lista, compartida por comienzo y tienda. Al pasar de una sala segura a una intermedia entra la música de gameplay mediante fundido; no empieza simplemente por cargar la escena de juego. La selección calmada/intensa conserva el umbral de salas visitadas.

Al entrar en una sala segura se retiran las peticiones temporales de gameplay y se cambia la base en una sola operación, evitando iniciar fugazmente una canción de combate antes de la segura. El aviso de último minuto exige estar en sala intermedia y tener TimerRunning. SafeRoomsPlaylist está vacía porque faltan sus canciones: se atenúa la música saliente durante 4 s y queda silencio musical (SFX continúan). Añadir AudioCues Music en su lista Tracks activará la misma rotación sin repeticiones y fundidos cruzados. No se usa música de menú/gameplay como sustituto.

Una lista base vacía ahora produce silencio sin suspender el director, permitiendo que al iniciar el nivel se reactive automáticamente la música correcta. StopMusic por derrota/victoria sí suspende la reproducción. Compilación runtime/MenuUIBase comprobada sin errores ni advertencias; pendiente validación audible de menú→inicio→intermedia→tienda→nuevo inicio, incluido salir del último minuto hacia tienda.

### Corrección: daño liquida el combo, no lo pierde

La aclaración del usuario sustituye las referencias anteriores a pérdida: PlayerHealth llama FinishComboOnDamage al aceptar daño. Se paga el combo activo mediante el mismo cálculo que al expirar, se conserva TotalPoints y se reinician contador, puntos activos y multiplicador. ComboPaid/CoinsChanged notifican el cobro una sola vez. LastComboEndReason distingue Damage de Expired para reproducir BreakComboFortakeDamage o BreakCombo, respectivamente; Coinsound también suena cuando hay monedas positivas. El HUD indica «Combo cerrado por daño» con el pago. Un combo ya vencido se liquida como expirado; golpear sin combo no paga otra vez.

Validación: runtime compilado sin errores/advertencias y seis comprobaciones ejecutadas: pago con conservación de puntos, causa de daño, ausencia de doble pago, siguiente combo desde cero, vencimiento previo y redondeo para puntos inferiores a una moneda. Pendiente validación audible/visual en Unity.

### Añadir música desde el inspector

En Unity elegir `Tools > Runner > Música` y la lista deseada: Menú, Salas seguras, Gameplay tranquilo, Gameplay intenso o Último minuto. En el inspector de la lista, arrastrar un AudioClip al campo «Canción nueva» y pulsar «Añadir canción a esta lista». El editor busca un AudioCue de música existente o crea uno en `Audio/Music`, lo agrega y guarda el asset; evita añadir el mismo clip dos veces a la misma lista. Una canción puede pertenecer a varias listas. «Canciones (Audio Cues)» permite quitar o reordenar referencias y «Transición (segundos)» ajusta el fundido. Para cambiar umbrales, seleccionar `Audio/Resources/Music/GameplayMusicSettings`.

Se detectaron MenuSong y Song1, Song2, Song3. MenuSong y Song1 conservan las asignaciones previas; Song2/Song3 no se asignaron automáticamente porque sus nombres no identifican la categoría. Compilación del nuevo inspector verificada incluyendo explícitamente el archivo nuevo en un proyecto temporal; pendiente probar la interacción en el editor Unity.

### Asignación de música confirmada

Song1 permanece en CalmPlaylist (fácil); Song2 y Song3 están en IntensePlaylist. SafeRoomsPlaylist sigue vacía hasta recibir la canción de sala segura. La música intensa se activa desde el umbral configurado, actualmente la tercera sala visitada, y alterna sin repetir cuando hay alternativas.

### Pasos al caminar (revisión vigente)

Footsteps.mp3 está asignado a FootstepNormal y se reproduce como bucle 2D con una fuente reutilizable de SFX. GroundSurfaceSensor lo activa solo si el controlador está habilitado, hay entrada de movimiento y desplazamiento horizontal real, está en suelo y no está corriendo, deslizándose ni pisoteando; se detiene al cesar estas condiciones, pausar, morir o cambiar escena. Agachado usa 55 % del volumen del cue, configurable en Crouch Footstep Volume; Min Footstep Speed evita sonar contra paredes. Master/SFX afectan el volumen. No se detectó una mecánica de dash independiente en el controlador actual; si se añade, debe incorporarse a estas condiciones.

Las superficies seleccionan su cue FootstepIce/Tar/Lava, y si no tiene clip usan FootstepNormal hasta que lleguen sonidos propios. Esta implementación reemplaza la nota anterior que indicaba que no existía reproducción de pasos. Compilación runtime comprobada; falta escuchar en Play Mode el ritmo y unión del bucle, velocidades de caminata, agachado, pared, salto, deslizamiento, carrera, pausas y cambios de superficie.
