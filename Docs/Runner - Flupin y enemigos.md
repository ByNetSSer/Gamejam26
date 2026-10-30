---
tags: [gamejam, enemigos, flupin, feel, combate]
estado: prototipo
---
# 🟣 Flupin y coordinación de enemigos

> [!abstract] Estado
> Primera versión jugable preparada en código. Se compilaron runtime/editor con FEEL 5.5 y se probaron las reglas del coordinador con sustitutos de Unity. **Falta probar el enemigo, su shader y FEEL dentro de Unity y en una build.**

## Flupin

Flupin es el primer enemigo de prueba. Se mueve en coordenadas del mundo, sin permanecer pegado al jugador. En escenas antiguas puede estar bajo el jugador en la jerarquía: al iniciar la partida se separa conservando su director. Si empieza demasiado cerca, se sitúa una vez a `Approach Start Distance` (12 unidades por defecto), en la dirección de su posición inicial.

1. **Preparación:** se mueve de forma curva mientras espera lejos a que el coordinador conceda el turno de ataque. La espera inicial se configura con `Initial Delay`.
2. **Acercamiento:** avanza en una espiral que reduce la distancia al jugador. La aceleración suaviza los cambios de dirección y una pequeña oscilación vertical aporta movimiento; el propio acercamiento es el aviso. El HUD provisional sigue indicando la amenaza.
3. **Impacto:** al llegar al jugador retira **un tercio de la vida actual en ese instante**. La detección comprueba también el segmento recorrido entre fotogramas.
4. **Expulsión:** el jugador puede usar **clic derecho** para repelerlo dentro del radio, incluso si está detrás de la cámara. Conserva la velocidad tangencial y continúa girando mientras se aleja. Si Flupin golpea, también sale expulsado, con más velocidad y distancia que con la repulsión normal.
5. **Recuperación:** continúa girando lejos y, tras la pausa configurada, solicita otro turno y vuelve a acercarse en espiral. Un ataque cancelado vuelve a la recuperación.

La repulsión tiene radio y cooldown configurables y usa la lista de enemigos activos, no una dirección de la cámara. Una repulsión exitosa otorga puntos por «Flupin repelido».

> [!note] Consecuencia del daño
> Un tercio de **vida actual** reduce la vida de forma proporcional: 9 → 6 → 4, por ejemplo. Los ataques proporcionales de enemigos tienen un mínimo de 1 punto de daño (limitado a la vida restante), por lo que también pueden causar derrota. Una fracción de daño configurada en cero no causa daño.

## Uso en Unity

- Crear de nuevo una escena con **Tools → Runner → Crear escena de prueba** o **Crear torre de prueba**. Ambas añaden un Flupin al jugador de demostración.
- Para una escena existente: seleccionar el jugador o un hijo y elegir **Tools → Runner → Añadir Flupin al jugador seleccionado**. Esta acción añade un nuevo enemigo; no reemplaza los existentes.
- Asignar el material y la textura deseados al `Renderer` de Flupin. No se cambia el material, el shader ni sus colores durante el ataque.
- En `Flupin`, `Approach Start Distance` controla la distancia inicial, `Radial Approach Speed` la velocidad hacia el jugador, `Orbit Degrees Per Second` el giro y `Max Tangential Speed` limita la velocidad lateral. `Flight Acceleration` suaviza la trayectoria. `Time Between Attacks` y `Recovery Duration` regulan la pausa después de la expulsión. Ajustar también el radio y cooldown del clic derecho desde `PlayerRepel`.
- Los `MMF_Player` de FEEL para aviso, repulsión y daño son opcionales. El ejemplo usa cambios de escala; los efectos que el equipo añada a FEEL pueden alterar la apariencia según su configuración.

## Eventos para sonidos y alertas

`PlayerRepel.Events` es un `EventStream` local al jugador. Los receptores se suscriben y deben cancelar la suscripción al desactivarse:

```csharp
IDisposable disponibilidad = repel.Events.Subscribe<EnemyRepelAvailabilityChangedEvent>(e =>
{
    // e.Enemy identifica a cuál de los enemigos corresponde.
    // e.Available indica si ahora el clic derecho puede repelerlo.
});
IDisposable golpe = repel.Events.Subscribe<EnemyRepelledEvent>(e =>
{
    // e.Enemy, e.Player, e.Position y e.Power están disponibles para el efecto.
});
// Al desactivar el receptor: disponibilidad.Dispose(); golpe.Dispose();
```

La disponibilidad cambia al entrar o salir del radio, al cambiar el estado del enemigo, el cooldown, la pausa o la activación del jugador. Se emite solo cuando cambia, no cada fotograma. El evento de golpe se publica una vez por cada enemigo que el clic derecho logra repeler; puede haber varios en una misma acción. La disponibilidad usa el mouse y el cooldown del clic derecho. El flujo es propio de `PlayerRepel`, no el `GameEventBus` del gestor de salas; quien necesite conectarlos puede suscribirse explícitamente.

> [!todo] Pendiente de diseño y producción
> Añadir y ajustar sonidos, alertas visuales y feedback de FEEL para estos eventos. Los eventos están preparados, pero todavía no se conectó ningún sonido o alerta nueva. Probar con varios enemigos y en escenas reales.

## Estadísticas y efectos por enemigo

Cada Flupin mantiene sus propios valores `StatValue`. Sus bases siguen en el inspector y `FlupinStat` permite modificar distancia inicial, tiempos, velocidad radial, giro, aceleración, oscilación, contacto, fracción de daño, velocidades y distancias de expulsión, atracción y recuperación. Usa los mismos tipos `Flat`, `Percent` y `PercentMult` del proyecto; los valores calculados no bajan de cero y algunas reglas aplican límites adicionales (por ejemplo, daño entre 0 y 100 % de vida actual). Los tiempos de una fase se calculan al comenzar esa fase; los cambios posteriores se reflejan al entrar en la siguiente.

```csharp
// Ejemplo: un efecto temporal sobre este Flupin concreto.
var efecto = new StatEffect(flupin.GetStat(FlupinStat.RadialApproachSpeed),
    StatModifierType.Percent, 0.25f, 5f);
efecto.Start();
// Guardar la referencia para cancelarlo antes de tiempo si hace falta.
```

También existen `AddModifier(source, stat, type, value)` y `RemoveModifiers(source)`, como en las estadísticas del jugador. Eliminar una fuente no retira las demás. No existe aún un catálogo de mejoras/maldiciones ni selección de contratos conectado a estos valores; sus efectos y balance quedan pendientes. Pueden coexistir varios Flupin con modificadores distintos: el coordinador limita los ataques simultáneos, no la cantidad de enemigos. La herramienta de demostración distribuye los añadidos en ángulos distintos.

## Presentación y render

`FlupinVisual` orienta el plano hacia la cámara si `Face Camera` está activo. Durante la partida coloca el renderer en la capa **FlupinOverlay** y desactiva su oclusión dinámica; restaura esos valores al desactivarse. Esta capa afecta al GameObject que contiene el renderer, incluidos los posibles colliders que se le añadan.

Los renderers URP de PC y móvil incluyen `Flupin Overlay`: dibuja esa capa después de los objetos transparentes, con una profundidad propia, conservando el shader y el material asignados. Así la profundidad de las paredes no tapa a Flupin. La UI normal puede dibujarse encima. Su apariencia pixel art sigue dependiendo de tu shader o textura; esta pasada no sustituye el material ni añade pixelado por sí misma.

El enemigo sigue ocupando una posición 3D: si queda fuera del campo de visión, detrás de la cámara o fuera de los planos de recorte, no aparece en pantalla. La cámara debe incluir `FlupinOverlay` en su `Culling Mask`; el material debe tener un pase compatible con URP (`UniversalForward`, `UniversalForwardOnly` o `SRPDefaultUnlit`). Un shader que consulte expresamente la profundidad de la escena puede comportarse de otra manera. Si se usa otro renderer URP, hay que añadirle esta renderer feature y excluir la capa de sus máscaras opaca y transparente habituales.

## Base compartida de enemigos

- `EnemyAgent` define registro, requisito del jugador, solicitud de ataque, cancelación y respuesta a repulsión.
- `EnemyDirector`, en el jugador, registra enemigos y mantiene una cola FIFO. Concede un solo ataque activo, incluido el aviso; espera un tiempo de gracia al terminar; comprueba si el jugador debe estar en suelo o aire y cancela si deja de cumplirse la condición. Un tiempo máximo libera el turno si algo falla.
- `PlayerRepel` aplica el clic derecho en radio a los enemigos activos que respondan a la acción.

Para un futuro enemigo, heredar `EnemyAgent`, declarar `AttackRequirement` (`Any`, `PlayerGrounded` o `PlayerAirborne`), solicitar turno cuando esté listo, avisar/atacar al recibirlo y liberar el turno al terminar o cancelar. Si deja un proyectil o peligro dañino activo, conservar el turno hasta que termine o ampliar el contrato: liberarlo antes puede permitir solapamientos.

**No es una garantía universal de justicia:** solo se coordinan enemigos que usan este director. Las trampas, suelos y proyectiles ajenos todavía no participan; no hay cálculo de rutas seguras, prioridad por amenaza ni un director de encuentros de contratos. Para evitar una situación como «debes saltar» + «te castiga por estar en el aire», integrar ambos peligros en un mismo sistema de restricciones y probar los tiempos y alternativas del jugador.

## Verificación

Compilación de runtime y editor sin errores. Siete verificaciones aisladas del coordinador; cuatro del contacto relativo, dos de la espiral, una de conservación de impulso y dos del cálculo de modificadores. Siete verificaciones aisladas cubren los eventos de repulsión, alcance, cooldown y varios enemigos. También se comprobó que las referencias y máscaras de las renderer features de PC y móvil sean coherentes.

**Pendiente probar en Play Mode y en build:** apariencia real del shader y transparencia del material, visibilidad a través de paredes, dimensiones y trayectoria percibida, clic derecho, daño, expulsión, cambios de sala y varios enemigos. Los cálculos aislados no sustituyen esa prueba visual.

## Feedback de parry

Al repeler con clic derecho se reproduce el prefab Impact Frame frente a la cámara y se ralentiza brevemente el tiempo global, recuperándolo gradualmente con tiempo real. ParryFeedback se añade automáticamente al PlayerRepel existente; en torres nuevas aparece también en el inspector para ajustar intensidad y duración. La partícula usa tiempo no escalado. Magic Time proporciona escalas locales, pero el movimiento y los temporizadores actuales usan Time.deltaTime; por eso esta primera integración utiliza Time.timeScale. El efecto requiere ajuste visual y prueba en Play Mode, y todavía no incluye sonido de parry.


Flupin se dibuja sobre la geometría opaca antes de los transparentes para que las partículas del impact frame puedan cubrirlo. Otros transparentes también pueden dibujarse encima; comprobar su interacción visual en Play Mode.


El parry añade chispas doradas de breve duración y una referencia opcional para sonido con ParryAccents; se puede asignar el sonido y ajustar volumen, color y cantidad en el inspector. Los acentos usan tiempo no escalado. La recuperación predeterminada del tiempo es inmediata y el efecto requiere prueba visual y sonora en Play Mode.

## Agresividad configurable

En el inspector de cada Flupin, **Aggressiveness** tiene valor base 1. Un valor 2 duplica la velocidad radial de acercamiento y hace que las esperas inicial, entre ataques y de recuperación transcurran al doble de ritmo; 0,5 las vuelve más lentas. No cambia el daño ni la fuerza de expulsión. La aceleración, el movimiento lateral y el coordinador de ataques mantienen sus propios límites, de modo que la velocidad o frecuencia real puede quedar limitada por ellos. El mínimo efectivo es 0,1.

Se puede modificar en tiempo de ejecución por instancia con `FlupinStat.Aggressiveness`, igual que las demás estadísticas:

```csharp
// Maldición: +50 % mientras esté activa. Cada efecto usa su propia fuente.
flupin.AddModifier(maldicion, FlupinStat.Aggressiveness, StatModifierType.Percent, 0.5f);
// Al terminar la maldición:
flupin.RemoveModifiers(maldicion);
```

El cambio afecta la velocidad y el ritmo de las esperas en curso. El coordinador aún limita a un ataque simultáneo y aplica su tiempo de gracia y duración máxima. La aplicación automática de maldiciones y buffs todavía depende del futuro sistema que las gestione.

## Dontsee (prototipo)

Dontsee aparece ocasionalmente en salas intermedias con un marcador `DontseeSpawnPoint`. `DontseeEncounter`, junto al `RoomManager`, selecciona como máximo un marcador al entrar: por defecto la probabilidad es 35 % y se dejan dos salas sin encuentros de Dontsee entre apariciones. No aparece en la inicial ni en la final. Al salir de la sala se destruye inmediatamente, incluso si espera su turno.

Solicita el turno FIFO del `EnemyDirector`; permanece invisible e inofensivo hasta recibirlo. Una vez visible dispone de 1,5 s de aviso y conserva el turno hasta salir, golpear o cancelarse, sin el límite de seis segundos de Flupin. Mirar hacia su centro dentro de la cámara con línea de visión libre lo detiene; si está fuera de pantalla o una pared con collider lo oculta, avanza hacia el jugador. No responde al clic derecho. Como regla provisional, el contacto quita un 33 % de la vida actual y termina el encuentro. Velocidad, aviso, radio, daño y capas que bloquean la visión se pueden ajustar en el prefab. El plano usa el render normal y la profundidad del entorno, sin la capa ni la pasada de Flupin. El material debe respetar profundidad; asignar la ilustración y material definitivos en el prefab.

Para probarlo, generar una torre con **Tools → Runner → Crear torre de prueba**. La herramienta crea el prefab provisional, marcadores en las salas intermedias y el componente de encuentros. En otras torres añadir `DontseeEncounter` al objeto del gestor, asignar el prefab y poner `DontseeSpawnPoint` en lugares visibles y accesibles de los prefabs de sala; el jugador necesita `EnemyDirector`. Los marcadores deben colocarse evitando paredes y puntos demasiado próximos al jugador. El movimiento es directo y no busca rutas ni evita geometría; las trampas todavía no participan en la coordinación de enemigos. La selección usa el azar de Unity y no la semilla de generación de salas.

Compilación de runtime y editor verificada con proyectos temporales que incluyen los scripts nuevos. Pendiente probar en Play Mode la mirada, oclusión, tiempos, cambios de sala, encuentros con Flupin y balance; no se ha validado una build ni el aspecto final.

Dontsee usa los dos recortes de `DontSee.png` en materiales con el shader y los ajustes pixelados de `pixel 1`. Muestra el ojo cerrado cuando se le mira y el abierto cuando no, también durante el aviso. `Sight Obstacles` es `Nothing` por defecto: la mirada solo comprueba si está dentro de la vista de la cámara, incluso detrás de paredes u otros objetos. Se pueden elegir capas si se desea que bloqueen la mirada; los prefabs existentes conservan su configuración. Los materiales están asignados al prefab de la torre de prueba existente y a las torres nuevas; el plano conserva la profundidad normal del escenario. Pendiente verificar visualmente en Play Mode el recorte, la transparencia y el cambio de estado.

## Eventos de Dontsee

Todos se publican en `RoomManager.Events`, el mismo bus del ciclo de salas. Un receptor puede suscribirse antes de que exista Dontsee y cancelar sus suscripciones al desactivarse. Los eventos incluyen `Enemy`, `Player`, `Room` y `Position`: la posición y el índice de sala son instantáneas; las referencias a objetos de escena pueden dejar de ser válidas después del evento.

- `DontseeSpawnedEvent`: se creó en la sala, todavía puede estar invisible esperando su turno.
- `DontseeActivatedEvent`: recibió el turno y se hizo visible; comienza el aviso.
- `DontseeGazeChangedEvent`: primera evaluación y cambios de mirada, sin repetición por fotograma. `Watched` es verdadero si está en pantalla y ningún obstáculo de las capas configuradas bloquea la vista; `IsInitial` identifica la primera evaluación, incluso si ya no se le mira. El evento se evalúa durante el aviso y la amenaza activa.
- `DontseeApproachChangedEvent`: `Approaching` indica inicio o detención del acercamiento por mirada o aviso. El tiempo de juego detenido puede pausar el desplazamiento sin cambiar este estado.
- `DontseeHitEvent`: contacto, con `Damage` calculado antes de aplicar daño. Se publica antes de terminar el encuentro y de aplicar el daño.
- `DontseeEndedEvent`: fin único con `Reason` (`RoomExited`, `HitPlayer`, `Cancelled` o `Disabled`). Sirve para detener sonidos continuos incluso si no hubo cambio de mirada o acercamiento al terminar.

Ejemplo de receptor:

```csharp
IDisposable mirada = manager.Events.Subscribe<DontseeGazeChangedEvent>(e =>
{
    if (e.Watched) { /* reproducir reacción al ser observado */ }
    else { /* primera vez sin mirar o se dejó de mirar: consultar e.IsInitial */ }
});
IDisposable aparicion = manager.Events.Subscribe<DontseeSpawnedEvent>(e =>
{
    // Usar e.Position para audio espacial, si corresponde.
});
// Al desactivar el receptor: mirada.Dispose(); aparicion.Dispose();
```

El bus despacha eventos anidados en cola: por ejemplo, la notificación de fin provocada durante `RoomExitedEvent` puede llegar después de que el objeto se haya desactivado. Usar las instantáneas y no depender de que el enemigo siga activo. No se han añadido audios ni efectos; quedan por conectar y probar en Play Mode.

Las estadísticas y modificadores de Dontsee y la guía común para futuros enemigos están en [[Runner - Estadisticas de enemigos]]. La probabilidad se modifica en `DontseeEncounter`; la separación de salas sigue vigente. La selección de buffs y su aplicación automática a enemigos futuros aún no existen.

## Selección y aparición entre torres en la escena e

`RoomEnemyRoster` conserva la selección de Flupin, Dontsee y la máscara durante la partida. En la escena `e` empiezan sin seleccionar y pueden incorporarse como maldiciones obligatorias en las torres pares. Las siguientes elecciones del mismo enemigo añaden un Flupin o máscara hasta cuatro por tipo; en Dontsee añaden 15 puntos porcentuales de aparición y reducen en una sala la separación mínima, con límites de 100 % y cero salas. Al menos una de las tres opciones será de enemigo si hay alguna disponible. El temporizador configurable empieza al entrar en la segunda sala intermedia: tanto la inicial como la primera intermedia están protegidas. Por defecto dura cinco segundos y se reinicia en cada torre.

Al entrar a la sala final o al terminar/perder, Flupin y las máscaras se desactivan. Se reutilizan las instancias al reaparecer cerca del jugador y se vuelve a asignar su director; los modificadores añadidos a través del roster se aplican también a las copias nuevas. Dontsee se retira al salir de su sala; el roster puede registrar modificadores persistentes mediante `AddDontseeModifier` y los aplica a cada instancia nueva. Los modificadores se pueden retirar por fuente. La elección está conectada a los altares, pero todavía no hay una pantalla de inventario.

Probar en Play Mode: esperar en la inicial, salir y medir el retraso, pasar varias salas, llegar a los pilares y continuar a otra torre. Confirmar también el material, FEEL, parry con clic derecho, destrucción de Dontsee al salir de su sala y conservación de modificadores.

## Máscara que embiste (prototipo)

`MaskCharger` ronda en espacio del mundo a una distancia preferida de 8 unidades, limitada por su alcance de ataque. Busca posiciones a los lados y delante de la dirección de desplazamiento del jugador; si está quieto usa su orientación. Si queda atrás o lejos, acelera para reposicionarse, y espera a estar delante antes de solicitar un ataque. Tras cada embestida cambia el lado preferido. `Orbit Degrees Per Second` y `Stalking Arc` ajustan el rodeo. No es hijo móvil del jugador. Al recibir turno fija la dirección hacia la posición actual del jugador y muestra una línea de aviso que marca el recorrido completo. La embestida mantiene esa dirección y atraviesa al jugador y el escenario. El recorrido termina como mínimo `Overshoot Distance` unidades después de la posición apuntada, o más lejos si la velocidad y duración configuradas producen mayor distancia. Al terminar libera el turno y entra en recuperación. El daño provisional es 33 % de vida actual, una vez por embestida, y no responde al clic derecho. El movimiento y sus distancias requieren ajuste en Play Mode.

Para preparar el modelo manualmente, añadir `MaskCharger` al objeto raíz de la máscara 3D, orientar su eje local +Z como frente y asignar ese componente en `Mask Charger` de `RoomEnemyRoster`; activar `Mask Charger Selected`. El roster la coloca detrás del jugador al comenzar el encuentro tras salir de la sala inicial y la desactiva al terminar la torre o la partida. La selección viene desactivada por defecto. El seguimiento y la embestida atraviesan geometría: no buscan rutas ni colisionan con paredes. Durante la activación se deshabilitan los colliders originales del modelo y se usa una esfera trigger para daño, activa solo durante la embestida; además se comprueba el barrido relativo para reducir pérdidas de contacto a alta velocidad. Los estados originales de colliders y rigidbodies se restauran al desactivar el componente. El barrido es una aproximación alrededor del centro del jugador, no una prueba exacta de cápsula.

`MaskCharger.Events` publica `MaskChargerStateChangedEvent` al pasar por `Following`, `Telegraph`, `Charging`, `Braking` y `Recovery`, y `MaskChargerHitEvent` al contactar; suscribirse y liberar las suscripciones para audio y FEEL. Al completar la carrera a máxima velocidad frena progresivamente durante `Braking Duration` (0,35 s por defecto) y continúa siendo peligroso hasta detenerse. La línea de aviso incluye la distancia adicional de frenada. Su duración también se puede modificar por instancia mediante `MaskChargerStat.BrakingDuration`. La velocidad, distancia y frenada se fijan al comenzar el aviso; los cambios posteriores se aplican al siguiente ataque. El turno se conserva hasta el final de la frenada.

Los campos opcionales de FEEL `Warning Feedback`, `Charge Feedback`, `Braking Feedback` y `Hit Feedback` aceptan `MMF_Player` del proyecto; dejarlos vacíos es válido. Se reproducen al empezar el aviso, la embestida, la frenada y al golpear, respectivamente. No hay efectos ni sonidos asignados automáticamente. Si se anima la máscara, usar un hijo visual para que FEEL no mueva la raíz que controla la trayectoria o altere la línea anunciada. Probar la intensidad y el tiempo en Play Mode.

Los valores de `MaskChargerStat` permiten modificadores por instancia con `GetStat`, `AddModifier` y `RemoveModifiers`; `RoomEnemyRoster` también expone métodos para añadir y retirar modificadores de la máscara asignada. La agresividad multiplica la velocidad de embestida y el ritmo de espera entre ataques, no el daño ni el aviso. No hay conexión automática con elecciones o tienda.

Flupin hereda ahora la traslación del jugador durante espera, órbita y acercamiento y conserva una velocidad relativa propia para la espiral; no hereda el giro de la cámara. Durante la expulsión vuelve al espacio del mundo y recupera la referencia relativa al regresar. Esto evita que la aceleración del jugador deje atrás al enemigo durante la persecución, pero hay que probar pasos por sala, teletransportes, cambios bruscos y repulsiones: el arrastre relativo también traslada a Flupin por paredes, coherente con su presentación superpuesta.

Compilación de runtime/editor comprobada incluyendo el archivo nuevo; hubo advertencias de paquetes FEEL/NiceVibrations y ningún error. Pendiente Play Mode: velocidades altas, obstáculos, interacción con puertas y trampillas, trayectoria fija, aviso legible, daño, coordinación con los otros enemigos y recuperación de Flupin. No se han modificado escenas ni creado herramientas de editor.


### Dontsee: preparación anticipada (2026-10-30)

Dontsee decide su aparición una sola vez para la siguiente sala intermedia elegible, usando sus `DontseeSpawnPoint`; nunca se genera en la sala actual. Conserva probabilidad, salas de separación y exclusión de salas protegidas/finales. La selección tardía de la maldición permite preparar la siguiente sala mientras todavía no se haya evaluado.

La instancia ya es visible antes de entrar: FEEL (`MMF_Scale`) anima desde escala cero hasta la escala original del prefab durante `appearanceDuration` (0,25 s por defecto). Espera inmóvil sin pedir turno hasta entrar en su sala, terminar la aparición y cumplirse la espera del roster. Obtener turno no vuelve a mostrarla ni reinicia su escala; conserva el aviso previo al acercamiento. La instancia actual y la preparada reciben modificadores y se cancelan juntas al detener los enemigos.

Validación visual pendiente en Unity: puertas y trampillas, turno ocupado por otro enemigo, probabilidad 100 % con separación cero, cancelación y cambio de torre.


### Skins de Flupin (2026-10-30)

`FlupinVisual` permite configurar `Skins` en el Inspector: nombre, `Weight`, `Idle` obligatorio y `Attack` opcional. El prefab `Assets/Proyect/Assets/enemys/Flupin/Flupin.prefab` tiene Gato (Normal, peso 60), Carita sonriente (Variant1, peso 25) y Variant2 (peso 15). Con esos valores las probabilidades son 60 %, 25 % y 15 %. Se normalizan los pesos positivos de skins con Idle; peso cero excluye una variante. Si todos pesan cero, usa la primera skin válida como predeterminada. Sin skins válidas mantiene la apariencia existente.

Se sortea una vez por instancia: desactivar/reactivar o cambiar de torre conserva su skin, y cada copia nueva sortea independientemente. `Approaching` usa Attack; Waiting, KnockedAway y Returning usan Idle. Si Attack está vacío, usa Idle también al atacar.

El quad del prefab usa PNG individuales importados como Sprite (Single), sin empaquetar en Sprite Atlas. Los cambios se aplican mediante MaterialPropertyBlock por instancia a imagen y contorno, conservando los materiales, shaders, FEEL y FlupinOverlay. También se admite SpriteRenderer. Las carpetas organizan los assets; para añadir skins hay que asignar las referencias en la lista del Inspector.

Compilación de runtime verificada incluyendo los scripts actuales. Referencias de las tres skins verificadas. Pendiente comprobación visual en Play Mode de ataque, repulsión, múltiples copias y contorno.


### Presión de máscaras y nuevas maldiciones (2026-10-30)

Las máscaras aparecen en el arco frontal a 8 m, con posiciones repartidas entre copias y fases distintas de acecho. Durante Following y Recovery heredan el desplazamiento real del jugador (incluido descenso), manteniendo su movimiento relativo para colocarse delante. La velocidad de reposicionamiento cubre el giro orbital y aumenta con la distancia al objetivo; `MaskChargerStat.OrbitSpeed` permite modificar ese giro. No heredan desplazamiento durante Telegraph, Charging ni Braking: conservan la trayectoria anunciada y esquivable.

`Attack Cadence Multiplier` vale 0,5: un intervalo configurado de 4 s pasa a 2 s. Ese intervalo se cuenta desde que empieza Recovery y transcurre en paralelo con ella; ya no se suman recuperación e intervalo completos. Recovery también reposiciona la máscara. Se solicita turno en cuanto termina la espera, está delante y dentro del alcance. Se conserva la coordinación de un ataque activo por EnemyDirector y su gracia, de modo que los 2 s no garantizan un ataque si otro enemigo ocupa el turno.

La agresividad aumenta seguimiento, giro, velocidad de embestida y ritmo de recuperación/intervalo; reduce el aviso, con mínimo de 0,3 s para preservar la señal. Se mantiene el daño únicamente durante embestida/frenada.

Nuevas maldiciones de la selección inicial y de La torre (cada 2 torres): Flupin impaciente (-20 % a espera inicial, intervalo y recuperación), Flupin furioso (+20 % agresividad), Máscaras sin respiro (-20 % aviso, intervalo y recuperación), Máscaras furiosas (+20 % agresividad), Acecho implacable (+30 % seguimiento, alcance, giro y recuperación de distancia), Dontsee furioso (+20 % agresividad). Solo se ofrecen mejoras para enemigos seleccionados; invocaciones y copias siguen disponibles. Cada recompensa elige una maldición; puede mejorar varios parámetros del mismo enemigo.

Los modificadores viven en RoomEnemyRoster y afectan instancias actuales y futuras. Las reducciones de tiempo se acumulan multiplicativamente (dos reducciones del 20 % dejan el 64 %); las mejoras porcentuales de agresividad/acecho son aditivas. Compilación runtime comprobada. Pendiente en Play Mode: persecución a alta velocidad, descenso, cambios bruscos de dirección, varias máscaras, combinación de maldiciones y legibilidad del aviso mínimo.
