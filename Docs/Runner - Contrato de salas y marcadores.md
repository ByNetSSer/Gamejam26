---
tags: [gamejam, unity, salas, prefabs, marcadores]
estado: prototipo
---
# 🚪 Contrato de salas y marcadores

> [!abstract] Para el equipo de arte y nivel
> Una sala es un prefab de geometría con objetos vacíos que indican dónde se conecta. Las salas no necesitan hielo, cajas ni lógica de aparición para funcionar. La primera sala tiene una salida de trampilla y una posición inicial, sin entrada necesaria.

Esta nota actualiza [[Runner - Salas y generacion]], incluida la antigua descripción de la escena de demostración.

## 🧩 Las cinco plantillas

| Prefab | Entrada | Salida |
| --- | --- | --- |
| Inicial | Ninguna; el jugador empieza dentro | Trampilla en el suelo |
| 1 | Trampilla en el techo | Trampilla en el suelo |
| 2 | Trampilla en el techo | Puerta en pared |
| 3 | Puerta en pared | Puerta en pared |
| 4 | Puerta en pared | Trampilla en el suelo |

Se pueden crear muchas variantes de cada combinación: en L, largas o pequeñas. Al conectar una puerta se busca una entrada de puerta; al conectar una trampilla se busca una entrada de trampilla. La salida de la sala elegida determina la conexión posterior.

## 📍 Preparar un prefab

1. En la raíz del prefab de sala, añadir `RoomDefinition`.
2. Crear un **GameObject vacío para la entrada** y asignarlo en `Entry Anchor`, junto con `Entry Type`. En la sala inicial dejar la entrada sin asignar.
3. Crear un **GameObject vacío para la salida** y asignarlo en `Exit Marker`, junto con `Exit Type` (`Door` o `Hatch`). Ajustar el ancho y la altura de puerta o profundidad de trampilla en `Opening Width` y `Opening Height Or Depth`.
4. Solo para la sala inicial, crear y asignar `Player Start`, donde aparecerá el jugador, y seleccionar salida `Hatch`.
5. Abrir huecos reales en el suelo, techo o paredes y colocar colliders sólidos únicamente en la geometría que deba bloquear al jugador. Los marcadores no abren huecos automáticamente.
6. Guardar la sala como prefab y añadirla a la lista `Room Prefabs` del `RoomManager`; asignar la inicial a `Starting Room`.

En el `RoomManager`, asignar también un `Hatch Prefab`: debe tener collider y `ImpactTarget` con la opción `hatch` activada. Su origen se coloca en el centro del marcador y debe coincidir con el tamaño del hueco; la herramienta de prueba crea uno de 3 × 3 unidades. Al preparar una sala con salida `Hatch`, el sistema instancia esa trampilla. Al pisotearla se rompe y permite bajar. Una salida `Door` queda abierta para cruzarla. El sistema crea un cierre de retorno invisible en la salida después de que el jugador avance la distancia `Exit Clearance`; ajustar esa distancia si cambia el tamaño del personaje.

## 🧭 Orientación de los vacíos

- Puerta de **salida**: colocar el vacío en el centro de la abertura y orientar su eje azul local **+Z hacia fuera**.
- Puerta de **entrada**: colocar el vacío en el centro de su abertura y orientar **+Z hacia dentro de la nueva sala**, en la dirección en la que viajará el jugador.
- Trampilla de salida: vacío en el centro del hueco, a la altura del suelo. Trampilla de entrada: vacío en el centro del hueco del techo, a la altura del techo.
- En todos los marcadores mantener +Y hacia arriba, sin inclinación en X/Z, y escala global `(1, 1, 1)`. En trampillas, +Z establece la orientación horizontal de la sala inferior.

El generador hace coincidir **posición y orientación** de salida y entrada. Por ejemplo, si una puerta de salida mira a la derecha, gira la sala siguiente para que su puerta de entrada mire en esa misma dirección de avance. En el editor, los gizmos muestran la entrada en verde, la salida en celeste y el eje +Z como una línea.

> [!warning] Diseño de geometría
> No se valida automáticamente que los huecos coincidan en dimensiones, que la trampilla cubra el hueco o que dos salas no consecutivas se solapen. La geometría, el espacio para el cierre y los colliders deben comprobarse con el jugador. Las salas en L pueden girar con el marcador de salida, pero el trazado puede volver sobre una sala anterior.

## 🧪 Cómo probar

**Tools → Runner → Crear torre de prueba** genera una carpeta nueva con la sala inicial, las cuatro combinaciones y un prefab compartido de trampilla; no añade hielo ni puntos de objetos. La escena creada queda sin guardar. Las carpetas y prefabs generados anteriormente no se sobrescriben: crea una nueva torre de prueba para obtener la versión actual.

**Tools → Runner → Verificar conexiones de salas**, fuera de Play Mode, comprueba alineación con rotaciones, paso y cierre de puerta, y sala inicial sin entrada. El planificador de compatibilidad pasó seis comprobaciones automatizadas y el código compiló. La verificación de Unity y el recorrido completo siguen pendientes de ejecución; se debe probar también trampilla, huecos y tres salas activas en Play Mode.

Los `RoomSpawnPoint` siguen disponibles para una etapa posterior y son opcionales. La torre aún termina en un estado de prueba sin tienda ni selección de la siguiente torre.

## Geometría y puntos de aparición de los prefabs nuevos

Los prefabs de `Assets/PrefabsMapas/Bajito` usan la primera letra para la entrada y la segunda para la salida: `P` es puerta y `T` es trampilla. Cada uno incluye `RoomGeometry`, con listas de paredes, suelos y techos ya asignadas a los objetos `Paredes`, `Piso` y `Techo`. Una entrada puede ser un objeto que agrupa varias piezas; ampliar las listas si una sala tiene más objetos separados. Otros sistemas pueden consultar `Walls`, `Floors` y `Ceilings` en la instancia de la sala. Estas referencias no detectan por sí solas la proximidad del jugador ni construyen la aplanadora; para copiar el suelo habrá que considerar su geometría, colliders, material y huecos.

Para añadir apariciones, crear objetos vacíos hijos en los lugares deseados y añadir `RoomSpawnPoint` a cada uno; configurar su probabilidad y opciones de prefabs. Al poblar una sala, `RoomDefinition` encuentra estos puntos automáticamente. No es necesario incluirlos en `RoomGeometry`.

Los prefabs nuevos todavía necesitan `RoomDefinition`, marcadores de entrada y salida orientados y la revisión de huecos y colliders antes de asignarlos a los cuatro pools de `RoomManager`. La primera sala y la final siguen requiriendo sus prefabs especiales. Los nombres identifican la combinación, pero no indican de forma fiable la posición ni orientación de cada conexión; no se han supuesto automáticamente.

## Maldiciones de suelo y aparición de Dontsee

En la escena `e`, `RoomCurseDirector` escucha la entrada y salida de salas intermedias. Por defecto hay un 10 % total de probabilidad de maldición, repartido por igual entre hielo, brea y lava; el inspector permite cambiar la probabilidad y la duración de transición de dos segundos. Hielo reduce frenado y giro; brea reduce velocidad y aceleración; lava aplica un punto de daño por segundo de contacto. Estos valores son provisionales. La transición conserva la textura original como base y mezcla patrones procedurales en el shader. Al salir se restauran los materiales y se retiran los efectos. Los eventos tipados `RoomCurseStartedEvent` y `RoomCurseEndedEvent` exponen sala y tipo de maldición.

Cada prefab de `PrefabsMapas/Bajito` tiene dos objetos vacíos `Aparición Dontsee` con `DontseeSpawnPoint`. El encuentro escoge uno al entrar en una sala elegible; se pueden mover en el prefab para adaptar el nivel. La posición, la apariencia del shader y los efectos deben comprobarse en Play Mode con las conexiones y colliders reales.

## Marcadores preliminares para contenido

Los 14 prefabs de sala bajo `PrefabsMapas/Bajito` tienen un grupo `Marcadores de contenido`, con dos puntos para cada categoría: candelabros, libros, reserva de cajas/barriles y posibles armas de pared. Son GameObjects vacíos, sin `RoomSpawnPoint`, prefabs asignados ni lógica de aparición. Se han propuesto a partir de las posiciones existentes de Dontsee y desplazamientos locales; no se ha comprobado que coincidan con paredes, suelo libre, puertas o trampillas. Antes de usarlos hay que abrir cada prefab en Unity, ajustar altura, posición y orientación según su geometría y comprobar accesibilidad y paso. En las armas el eje +Z es solo una orientación provisional, no garantiza mirar al centro.

Pendiente: definir e implementar la guadaña/hacha, su aviso, predicción limitada de la trayectoria y velocidad del jugador, daño, ciclo de vida y coordinación con `EnemyDirector`; colocarla visible en la siguiente sala preparada y evitar ataques antes de entrar. Estos marcadores no crean enemigos ni implementan ese ataque. También hay que conectar los puntos de props a `RoomSpawnPoint` y sus pools cuando se decida la distribución y se disponga de cajas y barriles.

## Pools de props y golpe del parry

Los 14 prefabs de sala tienen seis `RoomSpawnPoint`: dos de candelabros, dos de libros/copas y dos de cofres/jaulas (espacios previstos para cajas y barriles cuando estén disponibles). En cada punto se elige una opción con igual peso. Un punto de libros/copas tiene probabilidad 100 %; los otros cinco empiezan en 55 %. `TowerDifficulty.spawnChancePerTower` multiplica esa probabilidad por `1 + 0,1 × (torre - 1)` por defecto, limitada a 100 %. Es un aumento de cantidad esperada, no una garantía de que cada sala sucesiva tenga más objetos; el máximo son seis por sala. Las salas inicial y final no reciben estos puntos automáticamente. La selección usa el generador aleatorio de salas.

El clic derecho del `PlayerRepel` aplica también 10 puntos de daño directos al primer `ImpactTarget` bajo la mira a 3 metros, sin atravesar colliders sólidos. Alcance, daño y capas son configurables en ese componente; comparte el cooldown del parry y puede repeler enemigos en la misma acción. La escotilla sigue requiriendo pisotón. La rotura otorga la puntuación configurada y el daño por velocidad permanece activo.

Los puntos proceden de posiciones preliminares: revisar en Unity su altura, orientación y solapamientos con geometría, entradas, trampillas y otros props. Las armas de pared siguen siendo marcadores sin comportamiento. Falta probar en Play Mode generación, colisiones, parry, escalado y balance. Esta sección sustituye la indicación anterior de que los puntos de props estaban sin conectar.
