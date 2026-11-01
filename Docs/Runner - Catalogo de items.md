# Catálogo de ítems

Las 78 fichas seleccionadas están en `Assets/Proyect/Assets/Items/Catalogo`, separadas por tier. Cada `ItemDefinition` contiene nombre, representación, descripción del efecto, calidad, categoría, valoración, enemigo requerido, notas e icono opcional. Se pueden crear más desde `Assets > Create > Runner > Item de catálogo`.

Son datos de diseño: la descripción del efecto es texto, no una instrucción ejecutable. No están conectadas al `PlayerStatItem` existente y todavía no aplican modificadores, curación, inmunidades ni crean enemigos. Tampoco se han implementado inventario, selección, tienda o pools. `requiredEnemy` documenta el requisito; un futuro sistema de selección deberá comprobarlo, incluyendo enemigos habilitados que todavía no hayan aparecido.

Se respetaron los valores y textos de la selección. En particular, Botas sanguinarias no especifica si pierde 1 punto o 1 % de vida; Núcleo inestable no precisa qué velocidad reduce; Familia orbital solo indica un Flupin adicional; Rabia desesperada necesita precisar su coste. Esas fichas tienen notas para resolverlo antes de implementarlas. Suelas de hielo requiere definir la inmunidad. Los tiers no explícitos son una clasificación inicial editable, no una decisión definitiva.


## Adquisiciones en pantalla y maldición de torre (2026-10-30)

`ItemAcquisitionHud` se crea en el jugador al recibir la primera adquisición. Presenta una tarjeta superior de 520 × 156 (referencia 1920 × 1080), icono con salto de 0,5 s, círculo degradado de color, nombre y efecto. Dura 3,6 s y desvanece durante los últimos 0,55 s; usa tiempo no escalado, no captura input y encola adquisiciones simultáneas. `Display Seconds` y `Glow Sprite` se pueden configurar agregando previamente el componente al jugador; sin imagen de resplandor genera un degradado radial.

Orígenes: Tienda (dorado), Maldición (violeta), La torre (rojo), Hallazgo/pickup (turquesa). Se conecta tras compras exitosas, selección de maldición, `StatPickup` y `PlayerStatItem.Apply` (parámetro opcional de origen). Las compras fallidas no notifican. Los iconos provienen de `ItemDefinition.Icon`; pickups también permiten un icono explícito o su SpriteRenderer. Sin imagen se muestra un símbolo provisional y el texto. No se generan miniaturas de modelos 3D. Las descripciones de tienda reflejan los efectos realmente implementados.

Cada dos **torres completas**, no salas, se aplica una sola maldición automática. Prioriza elegir al azar entre adiciones de Flupin/máscara o aumento de frecuencia de Dontsee disponibles. Si todos están al máximo o no hay roster utilizable, elige una penalización de estadísticas. Conserva las maldiciones elegidas al inicio de torre. No aplica la automática al completar la torre final; para 12 torres ocurre en 2, 4, 6, 8 y 10. Existe protección contra repetirla para la misma torre. Los enemigos no se activan en la tienda al conceder esa mejora.

Verificado: compilación runtime con scripts actuales; regla de cadencia 2/4/6/8/10, duplicados y victoria. Pendiente en Unity: compra exitosa/fallida, pickup, varias adquisiciones seguidas, distintas resoluciones, salto/desvanecimiento y legibilidad con movimiento.


### Maldiciones que potencian enemigos

La selección manual y la automática incluyen ahora Flupin impaciente/furioso, Máscaras sin respiro/furiosas, Acecho implacable y Dontsee furioso, siempre que el enemigo correspondiente esté seleccionado. Se aplican a copias existentes y futuras, se acumulan entre torres y usan la misma notificación de nombre/efecto/origen. Ver `Runner - Flupin y enemigos.md` para porcentajes y comportamiento. La alternativa de penalización al jugador solo se usa si no hay opciones de enemigos disponibles.


## Control lateral en lugar de frenado (2026-10-30)

Suelas gastadas, Suelas del guardia y Ancla del explorador conceden respectivamente +5 %, +15 % y +30 % de control lateral en suelo en la tienda; Paso inestable penaliza ese control un 15 %. El nuevo atributo `GroundControl` modifica la corrección de la velocidad lateral hacia la dirección elegida mientras hay input y el jugador está en el suelo. No modifica la aceleración hacia delante, la desaceleración al soltar controles, el control aéreo ni el deslizamiento. Los modificadores se acumulan mediante el sistema habitual de estadísticas.

Las demás fichas que mencionaban frenado se actualizaron como propuestas equivalentes de control lateral, incluido Escudo de reacción; sus efectos de catálogo no se convierten por ello en funcionales. Las ofertas explícitas de tienda sí están conectadas. Pendiente probar los valores en Play Mode, especialmente giros a alta velocidad, superficies y acumulación de objetos.


«Invocar a Guilt» añade una única sombra con retraso de 4 s y drenaje por contacto; se elimina del sorteo una vez seleccionada. Disponible también en modo solo invocaciones/copias. No añade mejoras de Guilt ni reduce su retraso como maldición.


## Modelos legendarios y prueba de tienda

Los prefabs de `Items/Modelos legendarios` se asignaron a Ancla del explorador, Botas del vendaval, Botas del meteorito, Corazón de carrera, Corazón de cazador, Corona del espectáculo y Corona del temerario. Los siete están en el pool legendario y sus efectos están conectados; los corazones acumulados suman su efecto, con un único intervalo compartido para curación por repeler. El unicornio no tiene textura ni ficha asignada.

En `TowerRewardStation`, **Debug All Legendary** fuerza las tres ofertas y las renovaciones a rareza legendaria; está activado para la prueba. Desactivarlo devuelve las probabilidades configuradas. No afecta las opciones de maldición. Validar modelos, compras, efectos y comportamiento de la escena en Play Mode.

Los modelos 3D de las ofertas giran lentamente mientras flotan. El aura energética de legendarios se coloca detrás del volumen completo del modelo respecto a la cámara; la velocidad de giro y el margen del aura se ajustan en `TowerRewardStation`. Pendiente validar la apariencia en Play Mode.

En tienda, Alas del halcón reutiliza las botas aladas y Maldición de la torre utiliza Spheretransparent. Monóculo dorado usa provisionalmente la misma esfera. Para las ofertas sin sprite propio se reutilizan ilustraciones existentes: Reloj atrasado usa Impulso, Resorte usa Resorte encantado, y Cupón del mercader y Monóculo de la fortuna usan Moneda de la suerte. Comprobar en Play Mode la esfera transparente y la legibilidad de estos reemplazos.
