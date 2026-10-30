---
tags: [gamejam, salas, enemigos, prototipo]
estado: experimental
---
# Pared y palancas: prueba

## Probar

En Unity, elegir **Tools → Runner → Crear prueba aislada de pared y palancas** y entrar en Play. Se eligen tres de cuatro posiciones posibles. Mirar cada bloque amarillo y pulsar **E** a corta distancia; al activarlo cambia a verde. Al activar todas, desaparece la pared. El contador es provisional. La escena creada es temporal y se puede guardar. Los cubos son representaciones de prueba; no son arte definitivo.

## Adaptar a una sala

Las salas de `Assets/PrefabsMapas/Bajito` tienen variantes de formas, entradas, salidas y piezas. `RoomGeometry` agrupa suelos, paredes y techos, pero no determina rutas ni posiciones seguras. Por eso este prototipo no coloca obstáculos automáticamente ni modifica los prefabs actuales. Para probar en una sala concreta:

En el componente `RoomAutoLayout` que ya tienen estos prefabs aparecen los campos opcionales **Wall Spawn Point** y **Lever Spawn Points**, inicialmente vacíos. Sirven para guardar, respectivamente, un marcador hijo para posición y orientación del futuro muro y una lista de marcadores hijos para las posibles palancas o botones de esa sala. Crear y asignar los vacíos manualmente en cada prefab cuando se conozca su distribución. Estos campos son solo datos de preparación: todavía no están conectados a `WallLeverEncounter` ni generan o activan el encuentro. Una sala sin marcadores conserva su comportamiento actual.

1. En una copia de la sala, crear un objeto hijo con `WallLeverEncounter` y un objeto `Barrier` con visual y collider sólido que bloquee el camino deseado. Asignarlo en el inspector; debe cubrir la abertura real y no invadir otras salas. El componente controla su activación.
2. Preparar un prefab con `WallLever`, renderer y collider sólido; asignarlo a `Lever Prefab`. Su origen y orientación deben corresponder a los marcadores. No usar el prefab de sala como plantilla de palanca.
3. Crear vacíos para las posiciones posibles en suelo o cerca del suelo, del lado accesible de la pared. Asignarlos en `Lever Positions` y ajustar `Lever Count`, `Chance` y `Interaction Range`. Hay que colocar al menos tantas posiciones distintas como palancas requeridas. Los marcadores heredan la posición y rotación de cada instancia de sala.
4. Revisar cada posición en la sala real: que se pueda alcanzar y mirar, que el raycast de E llegue al collider, que ningún tile, objeto, trampilla, entrada o pared bloquee el acceso, y que el jugador no pueda saltar ni rodear la barrera si debe bloquear el paso. El sistema no valida estas condiciones y solo debería seleccionarse una combinación de posiciones que sea resoluble.
5. El componente funciona en una sala hija del `RoomManager`: toma el `EnemyDirector` del jugador y escucha entrada y salida. Elegir una sala compatible para una prueba supervisada; las posiciones deben autorarse por variante, no reutilizarse sin revisión.

Por defecto la probabilidad es 100 % para facilitar pruebas; se decide al entrar a la sala y la selección de marcadores utiliza el azar de Unity, no la semilla del generador. Con referencias, cantidad o colliders inválidos se deja el paso libre. Esa verificación no detecta posiciones inaccesibles ni barreras mal ajustadas.

## Coordinación y eventos

El encuentro muestra la barrera al entrar, solicita turno a `EnemyDirector` y permite activar palancas al recibirlo. Conserva el turno hasta completar o cancelar, para que no inicien otros ataques mientras se resuelve; no tiene timeout. Un ataque ya activo puede terminar mientras la pared espera, y la pared bloquea desde la entrada. Trampas y otras amenazas sin coordinador no quedan suspendidas. Este comportamiento puede ser frustrante y requiere pruebas antes de integrarlo. Al salir se libera el turno y se retira el obstáculo.

En salas gestionadas, `RoomManager.Events` publica `WallLeverProgressEvent` por cada activación, con contador, total y posición, y `WallLeverOpenedEvent` al abrir. Los receptores deben suscribirse y cancelar sus suscripciones; los eventos no añaden audio ni efectos. La escena aislada no usa RoomManager y no publica en ese bus.

## Estado

Compilación de runtime y editor comprobada con proyectos temporales que incluyen los scripts nuevos. Falta probar en Play Mode y en build la interacción, accesibilidad real de cada punto, progresión, bloqueo, limpieza en cambios de sala y convivencia con Flupin y Dontsee. No hay elecciones de buffs ni estadísticas de este encuentro, ni generación automática en salas reales.
