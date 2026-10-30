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
