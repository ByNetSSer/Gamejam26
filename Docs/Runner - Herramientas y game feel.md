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



## Distorsion gradual por velocidad (2026-10-30)



`PlayerController` incorpora automaticamente `PlayerSpeedFeedback` si no existe. Usa un Volume global de runtime con prioridad 100 y perfil propio de Lens Distortion; conserva los assets del Global Volume, el pixelado y el overlay de Flupin. Activa postprocesado en la camara del jugador. Solo controla la distorsion; no modifica FOV.



Valores iniciales de ajuste: empieza a 8 u/s, alcanza -0.3 a 45 u/s y permanece limitada por encima. La respuesta cuadratica mantiene los cambios bajos al principio (20 u/s: aproximadamente -0.032; 30 u/s: -0.106). Suavizado exponencial de 0.3 s, con tiempo escalado. Usa velocidad horizontal; `Include Vertical Speed` permite incluir caidas/pisoton. Al deshabilitar el controlador vuelve suavemente a cero; al deshabilitar el feedback retira su volumen.



Para guardar ajustes, agregar `PlayerSpeedFeedback` al jugador/prefab fuera de Play Mode y editar Start Speed, Full Effect Speed, Response Exponent, Maximum Distortion y Smoothing Time. Los umbrales son absolutos, no se normalizan con la velocidad maxima actual del jugador. Compilacion de runtime verificada con proyecto temporal que incluye el script nuevo y referencias DLL locales; falta validacion visual en Play Mode a 6, 20, 30, 45 y mas de 45 u/s, frenado, derrota y parry.



## Pulso visual al recibir dano (2026-10-30)



`PlayerHealth` incorpora `PlayerDamageFeedback` automaticamente; escucha solo `PlayerEvents.DamageReceived` (dano aceptado). Pulso de 0.45 s: vineta negra 0.4, exposicion -0.35, distorsion -0.38 y aberracion cromatica 0.15. La intensidad depende del dano respecto a la vida maxima (pulso completo al perder 30%); golpes consecutivos refrescan sin acumular oscuridad ilimitada. Recuperacion suave con tiempo no escalado, incluso al detenerse la partida por derrota.



Volume temporal de prioridad 200, por encima del feedback de velocidad y debajo del parry. El peso vuelve a cero para recuperar los valores de los volumenes inferiores, incluida la distorsion de carrera. No altera perfiles guardados. Para conservar ajustes, agregar el componente al jugador/prefab fuera de Play Mode. Compilacion de runtime correcta, incluyendo ambos scripts nuevos; pendiente comprobar visualmente dano leve/fuerte, lava repetida, dano corriendo, parry, derrota y desactivar/reactivar el componente.





## Hachazo y resistencia de props (2026-10-30)



El botón derecho conserva el parry y golpea el primer destructible bajo la mira a 3 unidades. Daño inicial 20 más 0,6 por unidad de velocidad hacia el objetivo; `ImpactDamage` también modifica el hachazo. Los props que sobreviven reaccionan con partículas y, si tienen `PropImpactPhysics`, impulso. La rotura usa el cue `impact.break` y sus fragmentos existentes. No hay modelo ni animación de hacha todavía.



Las vidas existentes distinguen objetos ligeros (taza 6, libros/candelabro pequeño 10–12), medianos (16–24) y pesados (30–35). En props con daño proporcional, al primer impacto se escalan vida y velocidad de rotura según la relación entre la velocidad objetivo de sprint al inicio de la torre y la de la primera torre. El escalado es `min(3, 1 + 0,25 × (relación − 1))`, con relación mínima 1. La referencia se fija por torre y los props no superan el triple de su vida y umbral iniciales. Para los impactos horizontales proporcionales, la velocidad de referencia de daño es el mayor entre el umbral escalado y el 45 % de la velocidad máxima actual; el daño crece con la velocidad perpendicular y los modificadores de impacto. Los golpes lentos pueden dañar un objeto sin romperlo de inmediato y el multiplicador de velocidad, los ítems y el daño acumulado también influyen. El pisotón mantiene su cálculo vertical. Trampillas y props con daño no proporcional conservan sus reglas anteriores. Valores iniciales sujetos a balance en Play Mode.



## Sensación de movimiento (2026-10-30)

La cápsula cambia inmediatamente; la cámara interpola la bajada y subida durante 0,12 s. El balanceo vertical depende de la velocidad y se activa en suelo sin deslizamiento; el aterrizaje añade una bajada corta. El FOV aumenta hasta 6 grados a 30 u/s y vuelve suavemente al frenar. Los valores están en PlayerController y pueden reducirse a cero. El feedback usa tiempo escalado y se limpia al desactivar el controlador. Pendiente validar comodidad, techos bajos, salto, deslizamiento y velocidades altas en Play Mode. El parry y el hachazo usan clic derecho.


## Props y señal de velocidad máxima (2026-10-30)

`PlayerSpeedFeedback` añade Chromatic Aberration a su volumen global temporal (prioridad 100), sin editar el perfil guardado: intensidad 0–1 según velocidad horizontal / velocidad objetivo de sprint actual, con suavizado de 0,12 s. La relación se limita a 1 incluso si un deslizamiento supera la velocidad objetivo. El volumen de daño de prioridad 200 puede modificar temporalmente el resultado. El HUD de depuración muestra la velocidad y el porcentaje de esa misma referencia.

`ImpactTarget` multiplica el daño por 1 a 85 % de la velocidad máxima y aumenta linealmente hasta 2,5 al 100 %, tanto en colisiones como en hachazo; la predicción de rotura usa el mismo cálculo. No afecta a trampillas. Cuando un prop sobrevive a un golpe, FEEL 5.5 anima brevemente hacia un color rojizo y devuelve el color de los materiales que exponen _BaseColor o _Color; el impacto conserva sus partículas e impulso existentes. Los materiales se instancian solo al necesitar el feedback y se destruyen con el prop. Los parámetros de umbral, multiplicador, color y duración son configurables en ImpactTarget. Pendiente validar visualmente en Play Mode los shaders de cada prop, los golpes repetidos, la composición con daño/parry, y balancear el valor máximo de aberración.


## Balance y fragmentos de props (2026-10-30)

Los prefabs de props proporcionales del pool tienen 20 % menos vida y 15 % menos velocidad de rotura que antes. Ambas cifras importan: la vida reducida facilita el hachazo y la velocidad reducida facilita la colisión, cuyo daño se calcula como fracción de vida máxima. Conservan el escalado por torre. `ImpactTarget` admite un prefab opcional de fragmentos físicos, limitado a 12 hijos y cuatro segundos por defecto; ver el brief de arte para prepararlo. Pendiente validar en Play Mode.


## Bonus de velocidad y ojo de pez (2026-10-30)

El bonus contra props comienza al 50 % de la velocidad horizontal de sprint máximo y sube linealmente de 1× a 2,5× al 100 %. Afecta al hachazo y a los impactos, incluida la predicción de rotura. El ojo de pez usa el mismo porcentaje en el volumen global temporal de velocidad: respuesta cuadrática desde 0 hasta Lens Distortion −0,12 al 100 %, suavizado de 0,3 s. La aberración cromática conserva su escala 0–1. La distorsión anterior por velocidades absolutas queda sustituida por este cálculo relativo. El efecto se limita al 100 % si un impulso supera el sprint objetivo y usa velocidad horizontal. Los valores requieren validación visual y de balance en Play Mode.


## Dígitos animados en interfaz (2026-10-30)

`CharacterCounterFeedback` anima dígitos en textos uGUI `Text`: sacudida independiente y transición de compresión al cambiar. El reloj aumenta la intensidad durante el último minuto; el encabezado del combo aumenta con su rango y el registro de puntos también usa el efecto. Shake, duración y frecuencia se ajustan en el componente; Intensity permite control desde código. Usa tiempo escalado. El panel de diagnóstico `ScoreDebugView` sigue usando OnGUI y no admite directamente este componente. Pendiente validar en Play Mode la legibilidad, los cambios rápidos, saltos de línea y el aspecto con la tipografía final.

## Manos animadas en primera persona (2026-11-01)

`FirstPersonHandAnimation` se incorpora automáticamente al jugador. Carga los fotogramas desde `Resources/Anjimations` y crea `SpriteRenderer` como hijos de la cámara: son planos en el mundo 3D, no elementos de Canvas. Reutilizan el shader `Runner/TutorialSheet`, con cola Overlay, `ZWrite Off` y `ZTest Always`, para verse por encima del escenario.

El clic izquierdo reproduce `PunitoAnimation` cuando `PlayerRepel` acepta el intento de golpe; el derecho reproduce `ParryAnimation` cuando acepta el parry. Una interacción enfocada con E en `TowerRewardStation` reproduce `AgarrarFinale`. Las acciones sustituyen temporalmente las manos de carrera.

La carrera usa dos copias de `HandAnimation`: la izquierda se obtiene mediante espejo horizontal. Los lados se alternan por ciclos completos; mientras uno reproduce todos sus fotogramas, el otro permanece oculto. La mano activa empieza separada y converge hasta un límite según `PlayerController.TargetSpeed`, calculada desde `Base Speed` y sus modificadores/progresión; la cadencia aumenta con la misma referencia. `Walking Hand Distance` y `Minimum Hand Distance At Maximum Speed` controlan las posiciones extremas. `Minimum Base Speed` mantiene la primera distancia y `Slow Run Frames Per Second`; `Maximum Base Speed` alcanza la distancia límite y `Fast Run Frames Per Second`. Entre ambos valores se interpola progresivamente y por encima del máximo queda saturado. Al no depender de la velocidad física instantánea, tocar una pared no altera la presentación de las manos; mientras haya input tampoco se ocultan por el frenado de la colisión.

El componente queda serializado en `Jugador` y funciona con `ExecuteAlways`. En Edit Mode, `Preview Speed` permite probar el intervalo `Minimum Base Speed`–`Maximum Base Speed`, `Preview Animation Phase` escoge el fotograma visible y `Preview Both Hands` facilita comparar los dos lados; estos tres valores solo afectan la previsualización. Durante `PlayerController.IsReadingTutorial`, pausa o cursor desbloqueado se ocultan todas las manos en juego. Pendiente validar composición visual en 16:9 y otras relaciones de aspecto, carrera lenta/máxima, las tres acciones y transición de tutorial.
