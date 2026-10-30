---
tags: [gamejam, unity, primera-persona, prototipo, estadísticas]
estado: prototipo
---
# 🎮 Prueba en primera persona y estadísticas

> [!abstract] Decisión confirmada
> El juego se prueba en **primera persona**. Esta nota sustituye las suposiciones de cámara lateral o en tercera persona de las notas anteriores.

## 🧪 Escenario de prueba

En Unity, selecciona **Tools → Runner → Crear escena de prueba** y entra en Play Mode. La herramienta crea una escena nueva sin guardarla; guárdala si deseas conservarla.

La escena incluye cajas rompibles, una escotilla con una zona inferior y tres esferas de prueba. La escotilla notifica una sala completada al romperse, para comprobar el aumento de velocidad. Esto es una conexión de demostración, no un generador de salas.

- 🟡 **Amarillo:** +6 de velocidad objetivo.
- 🔵 **Azul:** +4 segundos a la duración de combo.
- 🟢 **Verde:** +1 a la amplificación de puntos; con la base de 1 resulta ×2.

Las esferas aplican un modificador plano permanente durante la prueba. No representan aún la compra, inventario ni acumulación definitiva de ítems. El HUD es provisional y muestra velocidad, salas, combo, crecimiento y amplificación.

## 🕹️ Controles

| Entrada | Acción |
| --- | --- |
| W/A/S/D | Movimiento relativo a la mirada. En el suelo, S retrocede. |
| Flechas izquierda/derecha/arriba | Moverse lateralmente o avanzar. |
| Ratón | Mirar y girar. |
| Espacio | Saltar. |
| C | Mantener para agacharse; al pulsarla en el suelo con suficiente velocidad inicia un deslizamiento que puede abrir una puerta al chocar. |
| Ctrl | En el aire inicia un pisotón. |
| Shift | Correr: +15 % a la velocidad objetivo y +10 % a la aceleración mientras se mantiene. |
| Clic derecho | Repeler a un enemigo dentro del alcance. |
| E | Abrir la puerta o reja que se está mirando. |
| Escape / clic izquierdo | Liberar / volver a capturar el ratón. |

El deslizamiento multiplica la velocidad horizontal inicial por 1,1 y mantiene ese impulso durante 0,3 segundos antes de frenar; el umbral, multiplicador, duración y frenado se ajustan en el inspector. Al bajar del umbral continúa agachado y hay que volver a pulsar C para iniciar otro deslizamiento. El pisotón anula la velocidad horizontal al iniciarse. La cámara pierde control gradualmente con la velocidad horizontal; `CameraControl` modifica su sensibilidad. Estos comportamientos requieren playtesting.

## 📊 Estadísticas modificables

Los valores siguientes son bases iniciales, configurables en el inspector del componente correspondiente. Los modificadores de `PlayerStats` se aplican a estos valores; la lista no implica que exista todavía un ítem para cada una.

| Estadística | Base | Función |
| --- | --- | --- |
| `MoveSpeed` | 6; +0.6 por sala | Velocidad objetivo horizontal. |
| `Acceleration` | 24 | Cambio de velocidad al moverse. |
| `Braking` | 18 | Frenado sin dirección. |
| `AirControl` | 0.6 | Factor de control en el aire. |
| `JumpSpeed` | 9 | Velocidad inicial del salto. |
| `PoundSpeed` | 22; +0.4 por sala | Velocidad inicial del pisotón. |
| `ImpactDamage` | daño calculado por impacto | Modifica el daño que reduce vida; no modifica el umbral de rotura directa. |
| `CameraControl` | 1 | Factor de sensibilidad de cámara. La penalización por velocidad también se aplica. |
| `ComboDuration` | 4 s | Tiempo para mantener el combo; el valor se consulta al registrar una acción. |
| `ComboMultiplierGrowth` | 0.1 | Aumento del multiplicador por acción adicional. |
| `ScoreAmplification` | 1 | Factor de puntuación de cada acción. |

Multiplicador de prueba: `(1 + (acciones - 1) × crecimiento) × amplificación`. Los puntos se redondean por acción; con base 1 y crecimiento 0.1 el aumento no es visible en las primeras acciones. Se debe balancear en pruebas.

## 🛠️ Validación pendiente

Los scripts compilan. Todavía se debe probar en Play Mode la captura de ratón, las esferas, los choques a gran velocidad, la rotura de escotilla y la sensación de primera persona. Los subpasos reducen el riesgo de atravesar geometría pero no garantizan colisiones a cualquier velocidad. No hay aún enemigo orbital, peligros, vida, tienda, generador de salas ni juego completo.
