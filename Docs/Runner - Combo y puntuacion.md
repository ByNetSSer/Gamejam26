---
tags: [gamejam, combo, puntuación, estadísticas]
estado: prototipo
---
# 💥 Combo y puntuación

> [!abstract] Objetivo
> Mostrar qué acción dio puntos y recompensar cadenas rápidas. La base toma la idea de combo y tiempo del proyecto antiguo, sin depender de su audio, UI, singleton ni DamageNumbersPro.

## 🎯 Acciones y multiplicador

Cada acción registrada lleva un motivo y puntos base. La caja de prueba registra **Caja rota +1** y la escotilla **Escotilla rota +1**. `ScoreAward` entrega motivo, puntos base, puntos obtenidos, longitud de combo y multiplicador para una UI futura.

Cada acción reinicia la ventana de tiempo. Si la siguiente llega antes de que expire, aumenta el combo; si llega en el límite o después, empieza uno nuevo. Los puntos se suman inmediatamente, sin un pago adicional al terminar la cadena.

**Multiplicador provisional:** `(1 + (acciones - 1) × crecimiento) × amplificación`. Los puntos se redondean al entero más cercano, con mitades hacia arriba. Por ejemplo, con 10 puntos base y crecimiento de 0.5, dos acciones sucesivas otorgan 10 y 15 puntos. Con un punto base y crecimiento de 0.1, varias acciones iniciales pueden seguir mostrando +1 debido al redondeo. Los valores requieren pruebas y balance.

## 📊 Estadísticas para ítems

| Estadística | Base de prueba | Efecto |
| --- | --- | --- |
| `ComboDuration` | 4 segundos | Tiempo para la próxima acción; cambios se usan al registrar la siguiente acción. |
| `ComboMultiplierGrowth` | 0.1 | Incremento de multiplicador por acción adicional. |
| `ScoreAmplification` | 1 | Factor que multiplica la puntuación de cada acción. |

Se pueden modificar con `PlayerStatItem` o efectos de estadísticas existentes. Ejemplo: un modificador `Percent` de 0.5 sobre `ComboDuration` convierte 4 s en 6 s. Los ítems todavía no se compran ni equipan automáticamente.

## 🔌 Integración

- `Systems/Scoring/ComboState`: estado de combo reutilizable, independiente de Unity; recibe el tiempo y las estadísticas al registrar una acción.
- `Player/PlayerScore`: propiedad de puntuación del jugador; emite `Awarded(ScoreAward)` y `ComboEnded`.
- `Player/ImpactTarget`: cada destructible configura su motivo y puntos y registra la rotura en el `PlayerScore` del jugador que causó el impacto. Para excluir uno, dejar el motivo vacío. Una escotilla usa la misma configuración, sin registrar una transición de sala.
- `Player/ScoreDebugView`: UI temporal para la escena de prueba, no una interfaz final.

Un futuro enemigo, gestor de salas o sistema de estilo puede llamar `PlayerScore.Award(motivo, puntosBase)` una vez por acción. Las monedas siguen siendo un recurso futuro independiente y aún no están implementadas. No hay persistencia ni ranking.

## 🧪 Verificación y pendientes

La compilación de runtime y editor pasó. Se ejecutaron cuatro verificaciones del cálculo puro: motivo y primera acción, encadenamiento, expiración con amplificación y límite exacto del temporizador. `Tools > Runner > Verificar estadísticas y efectos` incluye además comprobaciones de combo en el editor, pero aún debe ejecutarse. Falta probar en Play Mode los impactos, la interfaz, los ítems y la sensación a velocidades altas.

## Economía y presentación experimental

Cada acción suma sus puntos amplificados al total de partida y al acumulado de la cadena. Al expirar, se entregan `floor(puntos de la cadena / pointsPerCoin)` monedas; el divisor por defecto es 10 y los restos no se acumulan. Una cadena que no alcance el divisor paga cero. El contador y las monedas son de la partida, no persistentes. Los cambios de duración se aplican al registrar la siguiente acción.

El Canvas uGUI creado por `ComboHud` muestra monedas, tiempo, puntos de la cadena y un registro de las seis últimas acciones. Los rangos D/C/B/A/S/SS usan umbrales configurables 0/10/30/70/150/300 puntos de cadena; son valores provisionales, no una reproducción exacta del sistema de ULTRAKILL. La UI de depuración anterior sigue disponible.

La sala final ofrece tres mejoras y permite comprar varias o continuar sin comprar. Los precios base configurables son 3/6/12 para común/raro/legendario, crecen linealmente un 15 % por compra previa y se redondean hacia arriba. Cada precio se fija al generar las ofertas; comprar no cambia los precios ya visibles. `ShopDiscount` se limita a 20 % al calcular nuevas ofertas. El cupón común añade 5 puntos porcentuales. El catálogo de esta sala sigue siendo provisional y no aplica automáticamente los assets `ItemDefinition`.

Para probar modelos de Itemsprops, selecciona los objetos de escena que tengan Collider y usa **Tools > Runner > Preparar props seleccionados como destructibles**. Se añade `ImpactTarget` a cada selección y se configura su nombre y 2 puntos; revisa resistencia y puntos en el inspector. La herramienta no modifica automáticamente los prefabs originales ni añade colliders. Los props de la escena de exposición no aparecen automáticamente en las salas: deben colocarse en sus prefabs de sala. La economía, los rangos, el tamaño y composición de salas requieren pruebas de Play Mode y balance.

## Cierre por daño (regla vigente)

Recibir daño aceptado finaliza y cobra el combo activo: conserva los puntos totales, entrega las monedas con la conversión habitual y reinicia contador/multiplicador. La siguiente acción comienza un combo nuevo. No se pierden los puntos acumulados. Usa un SFX de cierre por daño distinto al de vencimiento normal, además del sonido de monedas si el pago es positivo. El HUD muestra el pago. Esta regla reemplaza cualquier descripción previa de pérdida del combo por daño.


## Bolsa y cobro visual de monedas (2026-10-31)

En `Assets/Scenes/e.unity`, `UI Monedas` contiene la bolsa, el número y `Monedas del combo (Particle Image)`, editables en la escena. El objeto usa `bag` y cambia a `fullbag` desde 750 monedas mostradas; ambos tienen filtro Point. Al cerrarse el combo, el saldo real de PlayerScore se acredita inmediatamente y las partículas de AssetKits viajan desde el encabezado del combo a la bolsa. Al llegar, el contador visual sube de uno en uno y su tamaño aumenta mientras cuenta; los parámetros y el emisor se pueden ajustar desde el inspector. Las compras siguen consultando el saldo real. La tienda muestra el precio sin repetir el saldo; el contador anterior dentro del HUD de combo fue retirado.

El efecto usa la configuración de CoinAttraction incluida en el paquete, copiada a la escena. La compilación incluyó explícitamente el script nuevo y pasó; Particle Image produjo advertencias de APIs obsoletas. Falta validar en Play Mode partículas, profundidad, ritmo de la cuenta, cambios de bolsa, compras durante el conteo y ajustes visuales.


## Presentación editable del combo

En `e`, el componente `ComboHud` del jugador utiliza `UI Monedas/Panel Combo`, materializado en escena: rango, título, cadena, barra, acciones, acento y chispas son objetos editables. La interfaz aparece durante la cadena y permanece brevemente al cobrarse; la barra parpadea en rojo en el tramo final. Al subir de rango, la letra se expande y se reproducen chispas de Particle Image. Nombres, colores, umbrales, movimiento y duración se ajustan en `ComboHud`; el inspector también ofrece tres referencias opcionales a MMF_Player para acción, ascenso y cobro. Las referencias FEEL están vacías hasta que se configuren feedbacks. Las escenas sin UI asignada conservan una presentación provisional.

El sistema mantiene los puntos, umbrales, multiplicador, cierre por daño y pago existentes. `CoinWalletHud` sigue tomando el origen del cobro desde el rango. No se han añadido bonos de variedad ni detección de acciones nuevas. Compilación comprobada; pendiente validar visualmente y ajustar partículas, tamaño y legibilidad en Play Mode.


## Rangos por dificultad y efectos (2026-10-31)

Los rangos se calculan con los puntos de la cadena actual. Umbrales D/C/B/A/S/SS: Fácil 0/120/350/800/2000/4000; Normal 0/180/500/1300/3000/6500; Imposible 0/250/800/2000/5000/10000. Con 1800 puntos se alcanza A en Fácil/Normal y B en Imposible. Son valores iniciales para medir en partidas; no cambian el multiplicador ni el pago de monedas. Las tres listas se editan en ComboHud.

El panel editable del combo incluye una copia de `Masking` del paquete Particle Image y el fondo toma el color del rango. Las ofertas de `e` tienen asignado `EnergySpread` en `TowerRewardStation`; se instancia en un Canvas de espacio mundial por objeto, sigue la flotación y usa el color de rareza. Sin prefab asignado se conservan las luces anteriores como respaldo. La documentación local del paquete solo menciona explícitamente canvas Overlay y Camera, por lo que la apariencia, profundidad y rendimiento del canvas mundial deben probarse en Play Mode antes de considerar confirmada su compatibilidad.


## Ajuste de fondo y conteo

Masking ya no utiliza la imagen de demostración; su imagen lisa toma el color del rango, mientras el texto y el resto del panel conservan sus colores configurados. El panel tiembla de forma progresiva según el rango; la intensidad y frecuencia se ajustan en ComboHud. La barra conserva su aviso de tiempo restante.

El número de monedas usa el mismo componente de movimiento de dígitos que el reloj, con modo continuo para permanecer legible durante actualizaciones rápidas. El conteo visual comienza con la llegada de las partículas y adapta su duración al pago: aproximadamente 0,65 s para cantidades muy pequeñas y 3 s para 1000 o más, desde que comienza a contar. Como la pantalla tiene una frecuencia limitada, en cantidades grandes puede saltar valores entre cuadros. El saldo real sigue acreditándose de inmediato y el visual no supera el saldo tras una compra. Pendiente validar ritmo, lectura y casos de compras y pagos solapados en Play Mode.


### Tamaño y color de EnergySpread en la tienda

El prefab de demostración tiene anillos con tamaño inicial 1500, demasiado grandes en el canvas mundial. La instancia de la tienda reduce el tamaño de esos anillos a 180, limita también el haz y el brillo, y ajusta la escala mundial del efecto a 0,004; ambos valores se pueden ajustar en TowerRewardStation sin modificar el prefab original. Las partículas usan el color inicial correspondiente a la rareza y conservan su transparencia; los anillos usan un sprite monocromo asignado en la escena para admitir el tinte. Pendiente validar tamaño, color y coste visual en Play Mode.
