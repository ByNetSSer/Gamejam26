---
tags: [unity, feedback, feel, odin, documentación, gamejam]
estado: referencia
actualizado: 2026-10-28
---
# 🎮 Herramientas y referencias de game feel

> [!abstract] Objetivo
> Referencia para el equipo y futuras implementaciones de feedback en el runner. No implica que los paquetes mencionados ya estén instalados o integrados en este proyecto.

## Estado comprobado

- Proyecto: Unity **6000.3.9f1**, URP **17.3.0** e Input System **1.18.0**. El manifiesto no incluye Cinemachine.
- En `D:/GitHub/Local/Slash and smash/Assets/Feel` existe una copia de **FEEL v5.5**, según su `readme.txt`. Tiene `MMF_Player`, entre otros componentes.
- En `D:/GitHub/Local/Slash and smash/Assets/Plugins/Sirenix` existe una copia de **Odin**; no se confirmó su versión.
- El proyecto actual ya incluye **FEEL v5.5**, confirmado por `Assets/Feel/readme.txt`. La compilación de los scripts con FEEL pasó; falta validar los feedbacks en Play Mode. Odin no se detectó en el `Assets` del proyecto actual.

> [!warning] Versiones de documentación
> La [guía actual de sacudidas de FEEL](https://feel-docs.moremountains.com/screen-shakes.html) muestra ejemplos de FEEL 6.3 y Unity 6000.5; la copia local observada es 5.5 y el proyecto usa Unity 6000.3. Antes de aplicar esos pasos, comprobar la versión realmente instalada, sus dependencias, ejemplos y API local. Evitar asumir que el último tutorial coincide con la copia local.

## Prioridad sugerida

| Herramienta | Utilidad en este proyecto | Prioridad |
| --- | --- | --- |
| **FEEL** | Agrupar y ajustar sonido, partículas, animación y feedback de cámara para acciones. | Alta: el usuario ya dispone de él. |
| **Odin Inspector** | Mejorar organización y validación de campos y prefabs de salas e ítems. | Alta para el flujo del equipo; no produce feedback visual por sí solo. |
| **DOTween** | Animaciones de UI, objetos y transiciones mediante código. | Opcional si hace falta más control; FEEL ya cubre parte de este espacio. |
| **Cinemachine** | Herramientas de cámara e impulsos. | Opcional: la cámara actual es propia y habría que probar compatibilidad y evitar controles duplicados. |
| **Unity URP / partículas / audio** | Materiales, volúmenes, partículas y AudioSource para complementar feedback. | Aprovechar lo que ya trae Unity/URP antes de sumar más dependencias. |
| **Input System** | Entrada y, si se añade mando, soporte de vibración según dispositivo. | Ya está instalado; no asumir que el prototipo admite mando completo. |

## FEEL: uso propuesto

El [MMF_Player](https://feel-docs.moremountains.com/mmf-player.html) permite configurar feedbacks en el inspector y reproducirlos desde código con `PlayFeedbacks()`. La copia local v5.5 también contiene las sobrecargas `PlayFeedbacks()` y `PlayFeedbacks(Vector3, float, bool)`; verificar firmas y setup después de importar la versión elegida. La [lista oficial de feedbacks](https://feel-docs.moremountains.com/list_mmfeedbacks.html) ayuda a elegir efectos.

Aplicación sugerida:

| Acción | Feedback inicial posible | Fuente actual / integración |
| --- | --- | --- |
| Caja o trampilla rota | Sonido corto, partículas y sacudida suave. | `ImpactTarget.Broken`; el objetivo se desactiva justo después del evento. |
| Aterrizaje / pisotón | Sonido, pequeña reacción de cámara, partículas; intensidad según impacto. | `PlayerController.Landed` entrega velocidad de caída y si era pisotón. |
| Daño | Sonido y señal visual breve, con intervalo para evitar saturación. | `PlayerEvents.DamageReceived` cuando el daño se acepta. |
| Combo y puntos | Sonido por acción y pequeña animación de UI; variación por multiplicador. | `PlayerScore.Awarded` entrega motivo, puntos, combo y multiplicador. |
| Cambio de sala y fin de torre | Sonido o transición visual legible. | `GameEventBus`: `RoomEnteredEvent`, `RoomExitedEvent` y `TowerCompletedEvent`. |
| Velocidad elevada | Audio de viento y cambios graduales moderados de FOV. | `PlayerController.HorizontalSpeed`; implementar una capa de presentación separada. |

**Ciclo de vida:** un `MMF_Player` que viva en el objeto roto puede desactivarse junto a él antes de terminar de reproducirse. Para feedback persistente, usar un receptor que sobreviva al objeto, o instanciar partículas/audio con su propio ciclo de vida. `Broken` entrega el objeto, no el punto exacto del contacto; para posición precisa habría que ampliar el evento.

**Cámara:** el controlador `PlayerCamera` ya escribe la rotación de la cámara. Antes de añadir sacudida, diseñar una jerarquía de pivotes o una capa de feedback para evitar que dos sistemas sobrescriban el mismo `Transform`; validar que la mira y el control siguen siendo precisos. No añadir una integración de Cinemachine sin revisar cómo convive con la cámara actual.

**Tiempo y accesibilidad:** muchos temporizadores actuales usan tiempo escalado. Un hit stop basado en `Time.timeScale` puede pausar combo, daños y otros sistemas; definir duración, restauración y comportamiento bajo pausa antes de usarlo. Mantener sacudidas, FOV y flashes moderados y ajustables, especialmente en primera persona.

## Odin: uso propuesto

- Agrupar parámetros de movimiento, cámara y puntuación con atributos del inspector.
- Hacer más claro el inspector de `RoomManager` y sus cuatro pools.
- Ayudar a señalar referencias obligatorias o inconsistentes en prefabs, manteniendo la validación de runtime existente.
- Organizar ajustes de ítems y puntos de aparición para que artistas/diseñadores puedan editarlos.

Referencias oficiales: [atributos](https://odininspector.com/attributes), [tutoriales](https://odininspector.com/tutorials/) y [API](https://odininspector.com/documentation). Odin Validator puede ser un producto distinto; no asumir que está instalado o licenciado solo por tener Odin Inspector. Antes de usar atributos en código, confirmar que Odin está disponible en este proyecto y que el equipo puede abrirlo y compilarlo.

## Opcionales y documentación

- [DOTween: inicio](https://dotween.demigiant.com/getstarted.php) y [API](https://dotween.demigiant.com/documentation.php): útil para animaciones de interfaz y transiciones simples. Revisar versión y módulos del paquete realmente importado.
- [Unity: Cinemachine](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/index.html): evaluar solo si las necesidades de cámara justifican integrar otra herramienta; comprobar versión compatible con el editor concreto.
- [Unity: URP 17](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/urp-introduction.html): el proyecto ya usa URP. Los efectos y su rendimiento deben probarse con el render pixelado y la vista en primera persona.
- [Unity: vibración con Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.18/manual/Gamepad.html): relevante si se implementa soporte de mando; comprobar el dispositivo y detener vibraciones correctamente.

## Orden para una primera integración

1. FEEL 5.5 ya está importado. Si el equipo decide usar Odin, importarlo y comprobar compilación, versión y dependencias.
2. Crear un adaptador de presentación que escuche eventos existentes y dispare feedbacks configurables. Evitar introducir dependencias de FEEL en estadísticas, física o generación.
3. Empezar por **rotura de caja, aterrizaje/pisotón, daño y combo**; revisar que cada feedback termine aunque la sala o el objeto se retiren.
4. Probar en primera persona y ajustar intensidad según velocidad, frecuencia de acciones, comodidad y rendimiento. Después evaluar si DOTween o Cinemachine resuelven una necesidad que siga pendiente.

## Aún no implementado

Flupin tiene tres `MMF_Player` de demostración con escala para aviso, repulsión y daño. No hay integración de Odin ni shake, FOV dinámico, hit stop, audio reactivo o animaciones de combo confirmadas. El inicio del pisotón y la apertura de puerta no exponen aún eventos dedicados; podrían añadirse si se necesitan. No se ha creado una skill global: esta nota y `AGENTS.md` son referencias locales del proyecto.
