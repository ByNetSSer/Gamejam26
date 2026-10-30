---
tags: [gamejam, unity, salas, marcadores, generación]
estado: prototipo
---
# 🧭 Orientación de salas al bajar

> [!abstract] Alcance
> El generador alinea marcadores, no inspecciona automáticamente toda la geometría de las salas. Las salas pueden ser de tamaños y formas distintos si cumplen el contrato de conexión y se prueban sus colliders y aberturas.

## Conexión por marcadores

El `RoomDefinition` indica un marcador de entrada y uno de salida. El generador rota y traslada la sala siguiente para hacer coincidir el **centro de la entrada** con el de la salida anterior. En una puerta también alinea el eje +Z de ambos en la dirección de avance. El tamaño de la sala y la distancia entre sus marcadores no tienen que ser iguales a los de las demás.

Los marcadores deben permanecer activos, con escala global `(1,1,1)` y +Y hacia arriba. La orientación +Z de una puerta de salida va hacia fuera, y la +Z de la entrada de la sala siguiente va hacia dentro. La primera sala no necesita entrada; la final solo necesita entrada por trampilla. Ver [[Runner - Contrato de salas y marcadores]] y [[Runner - Pools final y puertas]].

## Trampillas: giros aleatorios

En `RoomManager`, `Hatch Rotations` permite elegir los giros al conectar desde una trampilla: `Forward` 0°, `Right` 90°, `Backward` 180° y `Left` 270°, relativos al marcador de salida. El centro de entrada permanece en el mismo punto. Se usa la misma semilla que la selección de salas; con semilla fija y configuración idéntica se puede reproducir la secuencia. Si no se selecciona ninguna opción se usa 0°. Por defecto están habilitados los cuatro giros.

**Huecos cuadrados:** deben tener dimensiones compatibles con la trampilla y permiten probar los cuatro giros. **Huecos rectangulares:** al girar 90° o 270° se intercambian ancho y profundidad; si sus dimensiones no encajan, limitar los giros permitidos, por ejemplo a 0° y 180°. El generador aún no valida las dimensiones de entrada ni el área real del agujero.

La cámara/jugador no se gira automáticamente: gira la sala inferior alrededor del centro de la entrada. Por ello se mantiene la orientación que el jugador tenía al caer. Las salidas por puerta no reciben giro aleatorio extra: la orientación queda determinada por sus marcadores.

## Lo que aún debe comprobarse

- Coincidencia de alturas, anchos, profundidad, grosor y colliders de la abertura real y del prefab de puerta o trampilla.
- Que el jugador pueda entrar y salir sin quedarse atrapado y que el cierre de retorno no lo solape.
- Que la geometría de una sala no tape la abertura ni se cruce con la anterior. No hay detección de solapamiento ni regeneración por geometría.
- Las salas de forma irregular o pasillos largos deben conservar espacio suficiente para los giros y para reaccionar a gran velocidad.
- No se garantiza que un prefab de cualquier escala funcione: marcadores y padres deben cumplir la escala global 1 y estar nivelados.

## Verificación

La compilación de runtime/editor pasó y cuatro comprobaciones de selección de orientación pasaron (cuatro direcciones, semilla reproducible, opciones restringidas y ninguna opción). **Tools → Runner → Verificar conexiones de salas** contiene comprobaciones de posición y orientación para los cuatro giros, pero no se ha ejecutado en Unity. Aún falta probar en Play Mode salas reales de distintos tamaños, huecos y puertas.
