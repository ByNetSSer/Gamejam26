---
tags: [gamejam, unity, salas, puertas, gameloop]
estado: prototipo
---
# 🏰 Pools, sala final y puertas

> [!abstract] Actualización
> La sala inicial y la final cuentan dentro del total. El recorrido empieza con una trampilla y termina en una sala con entrada por trampilla. Esta nota actualiza [[Runner - Contrato de salas y marcadores]].

## 🔢 Número de salas

`Rooms Per Tower` incluye **inicial + intermedias + final**, con mínimo de dos. Ejemplos:

- **2:** inicial → final.
- **3:** inicial → una intermedia → final; esa intermedia debe ser trampilla → trampilla.
- **8:** inicial → seis intermedias → final.

La inicial necesita posición del jugador y salida de trampilla. La final necesita solo un marcador de entrada `Hatch`, sin marcador de salida. El final de prueba se activa al entrar y tocar el suelo; cuenta la final, detiene el movimiento y emite `TowerFinished`. La tienda y la siguiente torre aún están pendientes.

## 🗂️ Cuatro pools

En `RoomManager`, asignar cada prefab intermedio al pool que corresponde a sus marcadores:

| Pool | Entrada | Salida |
| --- | --- | --- |
| `Hatch To Hatch` | Trampilla | Trampilla |
| `Hatch To Door` | Trampilla | Puerta |
| `Door To Door` | Puerta | Puerta |
| `Door To Hatch` | Puerta | Trampilla |

Cada pool puede contener muchas variantes geométricas. El sistema valida los tipos y prepara solo la siguiente sala; descarta opciones que no permiten alcanzar la entrada de trampilla de la sala final con las salas restantes. Los prefabs inválidos se indican en la consola y no se usan. También se mantiene una lectura de la lista antigua para escenas antiguas sin pools, con aviso; es preferible migrarlas a los cuatro pools.

Los prefabs se seleccionan con reemplazo: una sala puede aparecer varias veces seguidas. Si la cantidad prevista de salas no permite conectar con la final, el gestor reconstruye los pools y añade las salas intermedias mínimas necesarias. Si no existe ninguna cadena compatible, informa qué conexión falta; no puede inventar una geometría válida sin un prefab adecuado.

## 🚪 Puertas y trampillas

Asignar `Hatch Prefab` y `Door Prefab` en el gestor. La trampilla necesita `ImpactTarget` con `hatch` activado. La puerta necesita `RoomDoor` y al menos un collider que se pueda mirar; el prefab de ejemplo tiene bisagra y panel marrón. La puerta se abre mirando su panel a una distancia máxima de 3 unidades y pulsando **E**. El jugador debe tener el cursor capturado: clic izquierdo para capturarlo, Escape para liberarlo.

La puerta desactiva sus colliders al abrirse y anima el panel; el paso de sala se confirma después de atravesarla. La trampilla marrón se rompe con pisotón y la transición se confirma al descender. Tras pasar, se habilita el cierre invisible de no retorno. Los marcadores, huecos reales y convenciones de ejes siguen los de [[Runner - Contrato de salas y marcadores]].

## 🧪 Demostración y verificación

Crear una escena nueva desde **Tools → Runner → Crear torre de prueba**. Genera una carpeta nueva con la inicial, la final, las cuatro combinaciones y prefabs marrones de puerta y trampilla. Las carpetas anteriores no se sobrescriben, y sus prefabs no cambian de color automáticamente. Guardar la escena nueva si se quiere conservar. Las escenas antiguas también necesitan asignar la nueva sala final y el prefab de puerta.

Compilación de runtime y editor sin errores. Se ejecutaron siete casos del planificador de rutas, incluyendo dos salas, una intermedia, evitar terminar por puerta y rutas que no alcanzan la trampilla final. Falta validar en Play Mode la interacción mirando la puerta, la animación, los cierres, la caída y la llegada al suelo de la final. El sistema no detecta solapamientos entre salas ni abre huecos en la geometría.
