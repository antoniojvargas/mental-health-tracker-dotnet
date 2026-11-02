# Registros de decisiones de arquitectura (ADR)

Cada ADR recoge **una** decisión: el contexto que obligaba a decidir, lo que se decidió, lo que se
descartó y lo que cuesta. Se escriben cuando la decisión no se deduce del código y alguien, dentro de
seis meses, se preguntaría «¿por qué es así?». [`architecture.md`](../architecture.md) las resume y
enlaza; el razonamiento completo vive aquí, en un solo sitio.

| Nº | Decisión | Estado | Fecha | Áreas |
| --- | --- | --- | --- | --- |
| [0001](0001-el-cliente-decide-el-dia.md) | El cliente decide el día del registro; el servidor solo lo acota a «UTC + 1 día» | Aceptada | 2026-10-12 | backend, frontend |
| [0002](0002-tiempo-real-de-mejor-esfuerzo.md) | El tiempo real es de mejor esfuerzo; la fuente de verdad es la API | Aceptada | 2026-10-13 | backend, frontend |
| [0003](0003-confianza-en-el-proxy.md) | Confianza en el proxy: la API no se expone, se alcanza a través de nginx | Aceptada | 2026-10-12 | backend, infraestructura |
| [0004](0004-opciones-del-formulario-1-a-1-con-los-enums.md) | Las opciones del formulario son 1:1 con los enums del backend | Aceptada | 2026-10-13 | frontend |

## Cómo añadir una

1. Copia [`0000-plantilla.md`](0000-plantilla.md) como `NNNN-titulo-en-kebab-case.md` con el siguiente
   número libre (los números no se reutilizan).
2. Rellena contexto, decisión, alternativas y consecuencias; borra la nota de la plantilla.
3. Añade la fila a la tabla de arriba y, si afecta a la arquitectura general, un resumen con enlace en
   `architecture.md` (no dupliques el texto).
4. Una ADR aceptada no se reescribe: si la decisión cambia, se escribe otra que la **sustituye** y se
   cambia el estado de la antigua a «Sustituida por [ADR NNNN]». Las correcciones de erratas o de datos
   que dejaron de ser ciertos sí se hacen en el sitio.

Estados: *Propuesta* → *Aceptada* → *Sustituida* (o *Descartada*).
