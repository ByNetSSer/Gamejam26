---
tags: [gamejam, unity, salas, generación, gameloop]
estado: prototipo
---
# 🏰 Salas semiprocedurales y torre

> [!abstract] Idea
> Se reutilizan salas prefabricadas. Sus puntos de aparición pueden elegir cajas, obstáculos, superficies u otros prefabs. Cada salida determina qué entrada necesita la siguiente sala.

## 🔗 Conexiones

| Salida de la sala actual | Entrada necesaria de la siguiente |
| --- | --- |
| `Door` | `Door`: abertura lateral en la pared. |
| `Hatch` | `Hatch`: entrada desde arriba. |

Cada plantilla de este prototipo tiene **una salida**, de puerta o trampilla. Se puede preparar una plantilla distinta por combinación de entrada y salida. Antes de empezar, el generador comprueba que exista alguna secuencia compatible para el número de salas configurado; también evita elegir una sala que lleve a un callejón sin salida antes de la última. Esto solo verifica tipos, no garantiza que la geometría del prefab sea segura.

La salida y la entrada tienen un `Transform` de anclaje con posición y orientación. El sistema coloca la siguiente sala haciendo coincidir los anclajes. En ambos, el eje local +Z debe apuntar en la dirección del avance; en trampillas mantener los anclajes nivelados. Usar escala uniforme 1 en el prefab y sus padres. Diseñar aberturas, paredes y suelo para que coincidan y no colisionen con la sala anterior. El generador no verifica solapamiento ni rotaciones físicas arbitrarias.

## ♻️ Ciclo de salas

1. Se instancia la sala inicial y se prepara la siguiente compatible.
2. Al atravesar la puerta o caer por una trampilla rota, se confirma la transición y se activa el cierre de retorno configurado.
3. La sala que estaba detrás se desactiva inmediatamente y se destruye; la que se acaba de dejar pasa a ser la anterior. Se prepara una nueva sala siguiente si queda recorrido.
4. La transición suma una sala al jugador y registra «Sala completada» para puntos/combo. La última también cuenta.
5. Al completar la última, se emite `TowerFinished` y se detiene el controlador en la prueba. **La tienda, selección de enemigo con beneficio, regreso arriba y siguiente torre aún no están implementados.**

Hay como máximo tres salas activas: anterior, actual y siguiente. La sala anterior puede seguir existiendo hasta el final del frame después de desactivarse, por el `Destroy` diferido de Unity. El cierre de retorno es un objeto opcional configurado en cada salida; para cumplir la regla de no retorno, cada prefab debe proporcionarlo con geometría adecuada.

## 🎲 Puntos de aparición

Un prefab de sala puede tener varios componentes `RoomSpawnPoint`. Cada punto tiene probabilidad de activación y una lista de prefabs permitidos, pesos y desplazamiento local. Si se activa elige **un** prefab válido y lo instancia como hijo del punto; la limpieza ocurre al retirar la sala. Configurar cajas, candelabros, trampas, superficies u otros objetos como prefabs compatibles con su posición. Por ahora no hay límites globales, agrupaciones ni validación de zonas seguras.

`RoomManager` tiene una semilla fija opcional para reproducir la selección; si no está activa, usa una semilla aleatoria. La misma secuencia presupone igual orden de prefabs y puntos.

## 🧪 Escenario generado

En Unity: **Tools → Runner → Crear torre de prueba**. La herramienta pide guardar la escena abierta si tiene cambios, crea una escena nueva sin guardar y genera una carpeta nueva `Assets/Proyect/TorrePrueba...` sin sobrescribir carpetas anteriores. Incluye cuatro combinaciones de sala, caja e hielo como posibles apariciones y una torre de ocho salas. Guardar la escena si se quiere conservar.

Para tus propias salas, configurar `RoomDefinition` con tipo y ancla de entrada, posición inicial del jugador (para la primera sala) y `RoomExit`. La salida necesita ancla, dimensiones y un cierre opcional; si es trampilla, también un `ImpactTarget` configurado como trampilla. Asignar la plantilla inicial y el catálogo al `RoomManager`.

## ✅ Estado y pendientes

Compilación de runtime/editor sin errores y seis comprobaciones automatizadas del planificador de conexiones: combinaciones, falta de entrada, última sala válida y callejón sin salida. **Aún no se ha probado la torre en Play Mode**: comprobar alineación de puertas y alturas, paso por trampilla, cierre sin solapamiento, limpieza de salas, puntos de aparición y número de salas activas. La generación no comprueba si salas no consecutivas se solapan. La tienda, selección de riesgo y torres sucesivas permanecen pendientes.

## Prop sobre prop: pool y generación recursiva

`Assets/Proyect/Assets/Resources/PropStackPool.asset` es un catálogo editable de prefabs, tamaño (`Small`, `Medium`, `Large`) y peso. No es un pool de reutilización: instancia objetos. Cada `PropStackPoint` es un vacío hijo del prop soporte que refiere el catálogo y elige qué tamaños admite. El banco y ambos cofres tienen un punto para tamaños pequeños y medianos; los cofres también pueden salir sobre un soporte y continuar la cadena. El banco se añadió a las opciones de mobiliario de las salas. No hay todavía un prefab de mesa ni de caja de madera del catálogo nuevo.

Cuando `RoomSpawnPoint` crea un objeto, procesa sus puntos con el mismo generador aleatorio de la sala. Por defecto, cada nivel calcula `clamp01(0,80 − nivel × 0,08)`: 72 %, 64 %, 56 % y 48 %. El nivel empieza en 1 para el primer objeto encima. Profundidad máxima y presupuesto compartido por estructura son 4; son configurables en los puntos y evitan cadenas y ramificaciones infinitas. La probabilidad de cada punto pertenece a su propio prefab; si se cambian entre objetos, cada punto usa su propia configuración y el nivel global. El aumento por torre sigue afectando los puntos de sala, no esta probabilidad de apilado.

Por defecto el punto usa el centro superior de los límites de los colliders del soporte y coloca la base del collider del nuevo objeto a esa altura. Los offsets del vacío ajustan la posición; desactivar `Use Parent Collider Top` para colocar manualmente el vacío. Esto es una aproximación: los límites rectangulares no reconocen superficies curvas, inclinadas, huecos ni obstáculos; revisar tamaños, escala, colisiones, alturas, techos y posiciones reales en Play Mode. Los objetos apilados son hijos del soporte: por ahora, al romper el soporte se desactivan con él y no caen ni dan puntos por separado. No hay físicas de derrumbe. Para una mesa o caja nueva, añadirla al catálogo y colocar/configurar un vacío `PropStackPoint` en su prefab si debe poder sostener otros objetos.

## Puntos de sala por tamaño

`RoomSpawnPoint` admite `Prop Pool` y `Allowed Sizes`: si se asigna el catálogo, escoge de él según tamaño y peso; si no, conserva la lista `Options` anterior. `Chance` sigue afectada por la progresión de torre y `Pool Local Offset` permite ajustar la posición local. El objeto creado puede generar sus propios props apilados. Si no hay entradas válidas del tamaño elegido, el punto no crea nada.

Cada uno de los 14 prefabs de sala incluye en `Marcadores de contenido` tres plantillas inactivas: `Pool pequeño`, `Pool mediano` y `Pool grande`, con el catálogo compartido asignado y un tamaño permitido. Están en el origen local y no aparecen durante la partida hasta moverlas a una ubicación adecuada y activarlas. Se pueden duplicar, cambiar los tamaños permitidos y la probabilidad, o añadir `RoomSpawnPoint` a nuevos vacíos y asignar el mismo catálogo. Los seis puntos anteriores mantienen sus opciones y siguen funcionando. Revisar espacio, orientación y colisiones antes de activar puntos; no hay validación automática de geometría.

## Pools por lugar (configuración actual)

Las pools activas están en `Assets/Proyect/Assets/Resources/PropPools`. Los puntos existentes de las 14 salas de Bajito utilizan `Pool_Suelo`, con libros, jaula, banca, ambos cofres y estante; los filtros de tamaño de las plantillas siguen funcionando. Mesas y cajas aún no estaban en `Resources/Props` y deben añadirse al catálogo de suelo cuando lleguen. Los seis puntos activos anteriores ahora usan este catálogo compartido, en lugar de sus antiguas listas por categoría.

- `Pool_SobreMesa`: libros, candelabros y copa. Asignarla a un `PropStackPoint` en cada mesa nueva.
- `Pool_SobreBancaYCofre`: libros y candelabros. Ya está asignada a banca y ambos cofres.
- `Pool_PilaLibros` y `Pool_PilaLibros1`: cada una contiene solamente su propia pila, y cada prefab tiene su propio `PropStackPoint`.
- `Pool_PilaCajas`: catálogo vacío reservado para cuando existan cajas. Para que solo se apile la misma caja, utilizar un catálogo separado por variante, con esa variante como única entrada, y asignarlo a su `PropStackPoint`.

Al añadir un prefab, configurar su tamaño y peso en cada catálogo correspondiente, además de comprobar que tenga collider y `ImpactTarget` si debe romperse. En los nuevos soportes agregar un hijo vacío con `PropStackPoint`, asignar el catálogo y permitir los tamaños que de verdad quepan. Las posiciones automáticas basadas en límites de colliders y los tamaños de candelabros son provisionales; revisar visualmente y ajustar por prefab. El catálogo antiguo `PropStackPool` se conserva para no romper referencias externas, pero no define estas reglas específicas. Esta sección sustituye las descripciones anteriores de un único catálogo general para estos puntos.

## Mesas y física tras un golpe

Los prefabs `SM_Prop_Table_01`, `SM_Prop_Table_02` y `SM_Prop_Side_Table_01` se movieron desde `PolygonHorrorMansion/Prefabs/Props` a `Resources/Props` junto con sus `.meta`, conservando GUID y materiales. Están en `Pool_Suelo`; su punto superior usa `Pool_SobreMesa` y `ImpactTarget` registra `Mesa destrozada`. La mesa lateral tiene 20 de vida, rotura a 10 m/s y 3 puntos; las otras tienen 30, 12 m/s y 5 puntos. Valores provisionales.

`PropImpactPhysics` se añadió a mesas, cofres, jaula y estante. Al sobrevivir a un golpe de parry o impacto de velocidad, crea un Rigidbody, aplica gravedad e impulso limitado; no se activa al destruirse en el golpe. Masa, impulso y límite se ajustan en el inspector. Libros y candelabros pequeños no lo usan. La banca no se ha activado: su collider habilitado es una malla no convexa y necesita un collider adecuado para un Rigidbody dinámico. El componente rechaza mallas no convexas activas en vez de alterarlas automáticamente.

Los objetos apilados siguen siendo hijos y acompañan al soporte; al romperlo se desactivan con él. No hay derrumbe independiente. La física no causa daño adicional al golpear el escenario ni a otros props. Probar en Play Mode colliders importados, equilibrio de mesas, movimiento en pendientes, velocidad alta y empuje cerca del jugador; ajustar masas y fuerza. Si el objeto físico bloquea el avance o penetra en geometría, revisar sus colliders y el comportamiento del CharacterController.

## Destrucción de estructuras y feedback provisional

Al romper un `ImpactTarget`, se rompen también los `ImpactTarget` activos que son descendientes de su jerarquía. Cada uno registra su propio motivo y puntuación exactamente una vez; un objeto ya roto no vuelve a puntuar. Romper un libro superior no rompe el soporte ni sus hermanos. La regla incluye props colocados manualmente como hijos, además de los generados por `PropStackPoint`. No incluye props cercanos que no sean hijos.

`ImpactTarget` incluye feedback básico de fragmentos; `Pilalibros` y `PilaLibros1` usan pequeños rectángulos claros como páginas volando. El efecto se crea fuera del objeto destruido para seguir visible después de desactivarlo. En `Destruction Particles` se puede asignar un prefab de `ParticleSystem` para sustituirlo; `Feedback Lifetime` determina cuándo se elimina y debe cubrir su duración. `Destruction Feedback` permite desactivarlo. No se agregó audio. Esta regla sustituye el comportamiento anterior que ocultaba los hijos sin puntuarlos. Compilación comprobada; pendientes validación visual y comprobación de combo en Play Mode.

## Recuperación de caídas fuera del mapa

RoomManager añade RoomFallRecovery automáticamente. Calcula el límite inferior con los colliders activos de las salas anterior, actual y siguiente, más 8 unidades de margen. Al cruzarlo busca suelo con espacio para la cápsula cerca de la entrada de la sala actual; si no encuentra, usa una posición en suelo registrada en esa sala o el inicio de la sala inicial. Restablece el movimiento, concede 100 puntos base al combo con el motivo Exploiter y muestra el texto temblando. Cada recuperación queda registrada en la consola. Los valores se pueden configurar añadiendo RoomFallRecovery al gestor en el Inspector. El detector es una red de seguridad y no corrige el origen de las caídas. Pendiente validar en Play Mode puertas, trampillas, sala inicial/final, objetos y velocidades altas; también que las superficies de recuperación sean accesibles en todos los prefabs.


### Recuperación y cierres visibles (2026-10-31)

La recuperación valida el punto guardado antes de usarlo: debe tener suelo de la sala actual, espacio para la cápsula y quedar del lado correcto del cierre de entrada. En puerta exige distancia por delante del plano; en trampilla, la cabeza queda por debajo del cierre. Si no es válido, busca suelo en la sala actual hasta 18 unidades alrededor de la entrada; si no hay posición válida, no teletransporta ni concede puntos y reintenta. El premio Exploiter sigue otorgándose al recuperar correctamente.

La reja visual sube al abrirse y vuelve a bajar al avanzar; el bloqueo físico se activa al pasar y no permite reabrirla. Al atravesar una trampilla se restaura la baldosa original como cierre visual detrás del jugador, mientras la copia desprendida sigue cayendo; el collider de retorno sigue siendo independiente. Los prefabs genéricos sin baldosa restauran el visual de la trampilla rota. Pendiente comprobar en Play Mode las conexiones reales, posiciones de recuperación, colisiones y animaciones con todos los prefabs.


### Presentación de la tienda

Las ofertas permiten apuntar directamente a su sprite o modelo, además del altar, y muestran nombre, efecto, precio y acción cerca del centro de la pantalla. Tras comprar, se ocultan el modelo y su aura; renovar crea nuevas ofertas. Las etiquetas de rareza ya no se escriben en la descripción. Los sprites comunes usan aura blanca y los raros verde; el código reserva azul para una futura categoría adicional, todavía no presente en los pools. Los legendarios conservan sus modelos 3D sin aura. El sprite `Resources/Sprite/ShopAura` utiliza filtro Point y sin mipmaps.

La tienda mantiene solo el pedestal de renovación. `Reroll Prefab` en TowerRewardStation permite asignar su futuro modelo; sin prefab usa el cubo existente. Hasta diseñar el mecanismo de avance, N inicia la siguiente torre desde la tienda. La pantalla final de victoria conserva su interacción. Pendiente probar en Play Mode la selección, los colores y profundidad del aura, compra, renovación y avance con los prefabs reales.


### Esfera de renovación de ZonaSegura

La tienda utiliza el objeto hijo `reroll` del prefab de zona segura como interacción de renovación, respetando su esfera, pilar, luz y posición. Apuntar a cualquiera de sus partes visibles permite mostrar el precio y renovar; los colliders adicionales de interacción se crean solo durante la tienda. El conjunto se oculta en las salas iniciales y en la victoria. Si otra sala segura no tiene `reroll`, se conserva la alternativa configurada con `Reroll Prefab` o el cubo provisional. La renovación mantiene su coste acumulado y sus efectos anteriores. Pendiente validar en Play Mode puntería, colisiones y presentación.
