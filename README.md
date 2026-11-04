# PapayApp

PapayApp sera una aplicacion de escritorio para administrar archivos Excel, guardar copias modificadas, organizar documentos y generar PDFs.

## Plataforma inicial

La primera version apunta a:

- Windows 10 version 1809 o superior
- Windows 11

Esta decision evita depender de sistemas Windows antiguos y permite usar una base tecnologica moderna con mejor soporte a futuro.

## Ruta tecnologica propuesta

- Lenguaje: C#
- Plataforma: .NET
- Interfaz: .NET MAUI
- Arquitectura: MVVM
- Excel: ClosedXML
- Base de datos local: SQLite
- Conversion a PDF: LibreOffice en modo headless

## Interfaz de trabajo

- Inicio presenta accesos a evaluaciones y certificados, la carpeta de resultados y los últimos 12 lotes generados. El historial empieza a registrarse con esta versión.
- Certificados presenta el formulario en una sola columna, muestra la plantilla activa y mantiene la acción de generación fija y los accesos a resultados.
- Archivos muestra una lista compacta con búsqueda dentro de la carpeta actual y una ruta con botones para volver a sus carpetas superiores.
- Ajustes ofrece tema claro/oscuro y tamaños de texto Normal, Mediano y Grande. **Administrar plantillas** abre una sección propia; los diseños siguen requiriendo Guardar.
- Los resultados de cada lote ofrecen abrir el primer archivo generado, abrir su carpeta, consultar el registro de errores cuando existe y preparar otro lote.
- La barra lateral conserva solo los iconos, con ayudas al pasar el cursor y nombres accesibles. Los mensajes de problemas incluyen acciones de recuperación y el detalle original.

## Compatibilidad futura

La logica principal debe mantenerse separada de la interfaz para facilitar una version futura para macOS.

Estructura objetivo:

```text
PapayApp.Core
PapayApp.Infrastructure
PapayApp.UI
PapayApp.Tests
```

`PapayApp.Core` concentrara reglas de negocio, modelos y operaciones independientes del sistema operativo.

`PapayApp.Infrastructure` concentrara integraciones con archivos, SQLite, LibreOffice y servicios del sistema.

`PapayApp.UI` concentrara la interfaz .NET MAUI.

`PapayApp.Tests` concentrara pruebas automatizadas.

## Requisitos de desarrollo

Para generar y compilar el proyecto sera necesario instalar:

- .NET SDK 10 con soporte para MAUI
- Visual Studio 2022 con la carga de trabajo ".NET Multi-platform App UI development"
- LibreOffice, para pruebas de conversion a PDF

## Comandos de desarrollo

Si `dotnet` ya esta disponible en la terminal:

```powershell
dotnet build .\PapayApp.slnx
dotnet test .\tests\PapayApp.Tests\PapayApp.Tests.csproj
dotnet run --project .\src\PapayApp.UI\PapayApp.UI.csproj
```

Si la terminal aun no ha refrescado el PATH despues de instalar .NET:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build .\PapayApp.slnx
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\PapayApp.Tests\PapayApp.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\PapayApp.UI\PapayApp.UI.csproj
```

## Compilar el instalador para Windows

Ademas de los requisitos de desarrollo, se necesita [Inno Setup 6](https://jrsoftware.org/isinfo.php). El redistribuible oficial de Windows App Runtime debe existir en:

```text
installer\prerequisites\WindowsAppRuntimeInstall-x64.exe
```

Desde **CMD**, ubicarse en la raiz del proyecto:

```cmd
cd /d C:\Users\Ale\Documents\GitHub\PapayApp
```

Primero se publica PapayApp 1.0.7 para Windows x64. La aplicacion incluye el runtime de .NET, mientras que Windows App Runtime se instala como prerrequisito oficial:

```cmd
.tools\dotnet\dotnet.exe publish src\PapayApp.UI\PapayApp.UI.csproj ^
  --configuration Release ^
  --framework net10.0-windows10.0.19041.0 ^
  --runtime win-x64 ^
  --self-contained true ^
  --output installer\app-1.0.7 ^
  -p:WindowsPackageType=None ^
  -p:WindowsAppSDKSelfContained=false ^
  -p:PublishReadyToRun=false
```

Despues se compila el instalador con Inno Setup:

```cmd
"%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\PapayApp.iss
```

El instalador resultante se guarda en:

```text
installer\output\PapayApp-1.0.7-Setup-x64.exe
```

Antes de compilar una version posterior, deben actualizarse el numero de version y la carpeta de publicacion en `installer\PapayApp.iss`, `PapayApp.UI.csproj`, `Package.appxmanifest` y `.gitignore`.

## Interfaz inicial

La primera interfaz contiene:

- Una pantalla de bienvenida minimalista.
- Una animacion nativa inspirada en el prototipo HTML/SCSS de tarjetas apiladas.
- Texto inferior con "Bienvenid@" y la fecha actual.
- Una pantalla inicial posterior lista para evolucionar.

La paleta base proviene de la referencia visual:

- `#173753` Deep Space Blue
- `#6DAEDB` Sky Reflection
- `#2892D7` Blue Bell
- `#1B4353` Charcoal Blue

La translucidez/blur de ventana se maneja como un servicio aislado en la capa de UI. En Windows se intenta usar Acrylic cuando el sistema lo permite; si no esta disponible, la aplicacion continua con un fondo solido.

## Directorio base

En el primer inicio, PapayApp solicita un directorio base. La ruta se guarda para futuros usos y dentro de ella se crean estas carpetas:

- `EVALUACION MEDICA - PERIODICA`
- `EVALUACION MEDICA - PREOCUPACIONAL`
- `EVALUACION MEDICA - REINTEGRO`
- `EVALUACION MEDICA - RETIRO`

## Plantillas

La plantilla base de Excel se conserva como asset de la aplicacion en:

```text
src/PapayApp.UI/Resources/Raw/Templates/FICHA MEDICA.xlsx
```

Esta plantilla no debe modificarse directamente. Los futuros procesos de generacion deberan copiarla antes de escribir datos.

## Certificados

La seccion `Certificados` permite generar documentos manualmente o desde un Excel con esta estructura:

- Columna A: apellidos y nombres.
- Columna B: numero de cedula de 10 digitos.
- Fila 1: encabezados.
- Fila 2 en adelante: participantes.

El texto comun admite la variable `{cedula}`. La aplicacion puede crear un PDF por persona o un solo PDF con todo el lote, conserva el ultimo folio utilizado y registra los folios que no pudieron generarse.

En **Ajustes > Plantilla de certificado** se puede cargar un PDF y editar sus componentes sobre un lienzo. Cada página conserva su fondo y permite agregar o eliminar bloques de texto y firma PNG; los rectángulos se mueven arrastrándolos y se redimensionan desde las cuatro esquinas del componente seleccionado. El selector permite elegir componentes superpuestos. Se puede ajustar la fuente (Cinzel o Alex Brush), tamaño máximo, negrita de Cinzel, color y alineación de cada texto.

Los textos combinan contenido fijo con `{nombre}`, `{cedula}`, `{folio}`, `{fecha}` (ddMMyyyy), `{tipo}`, `{texto}`, `{firmante}` y `{cargo}`. Los valores se toman de la sección Certificados; solo se exigen el texto, tipo, firmante, cargo o firma cuando el diseño los utiliza. Los textos ajustan su tamaño al área disponible; si no caben con 6 puntos, se informa el fallo en lugar de recortarlos. Los bloques de firma anteriores pueden compartir la firma principal. Los nuevos bloques de firma independientes permiten seleccionar su propio PNG.

Para agregar otro firmante, usa **Agregar firmante (firma, nombre y cargo)** en Administrar plantillas. Se crean tres componentes independientes que se pueden mover y redimensionar. Sus nombres serán las etiquetas de los controles en **Certificados > Datos adicionales de la plantilla** después de guardar. **Agregar texto** crea valores editables por lote (Texto 1, Texto 2, etc.), con soporte para varios párrafos; **Agregar texto fijo** mantiene contenido fijo o variables conocidas. En cualquier componente, el interruptor **Solicitar este dato de forma independiente al generar** permite cambiar entre ambas modalidades. Para convertir una segunda firma antigua que compartía la firma principal, activa ese interruptor en su componente y guarda.

Los valores de estos controles se completan una vez para todo el lote. Cada imagen de firma es independiente; la generación indica por nombre qué dato o archivo falta. El preset conserva la definición y el formato de los campos, mientras que sus valores se mantienen solo en memoria durante la sesión.

El listado **Plantillas guardadas** conserva cada PDF con su nombre y configuración independiente. **Nueva plantilla desde PDF** crea un borrador; solo **Guardar plantilla** lo agrega a la biblioteca. Guardar una plantilla existente actualiza únicamente ese preset. El nombre puede cambiarse sin crear un preset nuevo; los nombres no se pueden repetir.

Seleccionar una plantilla guardada solo la abre en el editor: hay que pulsar **Guardar plantilla** para activarla en la generación. La etiqueta **Plantilla activa** indica cuál se usará mientras se editan borradores. Ni cargar, ni seleccionar, ni editar componentes guarda cambios automáticamente. Al cambiar de plantilla con cambios pendientes, se puede cancelar o descartarlos. **Descartar cambios** vuelve a la plantilla activa guardada. **Restaurar diseño original** prepara una copia nueva del diseño incluido, que también debe guardarse. Las plantillas de la versión anterior se conservan como **Plantilla anterior**, sin escribir la migración hasta pulsar Guardar.

Todas las páginas se generan por participante, tanto en archivos separados como en un PDF combinado. Los PDFs guardados se copian a los datos locales de la app, de modo que no hace falta volver a localizar el archivo original.

La plantilla y las fuentes incrustadas se encuentran en:

```text
src/PapayApp.UI/Resources/Raw/Templates/CERTIFICADO.pdf
src/PapayApp.UI/Resources/Raw/Fonts/
```
