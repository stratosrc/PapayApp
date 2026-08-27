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

La plantilla y las fuentes incrustadas se encuentran en:

```text
src/PapayApp.UI/Resources/Raw/Templates/CERTIFICADO.pdf
src/PapayApp.UI/Resources/Raw/Fonts/
```
