# Rebith 2 (C#) - Gestor de respaldos

Reescritura en C# (.NET Framework 4.8 + WinForms) de `backup_manager` (Python).
Hace lo mismo que la versión en Python, pero está reforzada para producción.

## Compilar e instalar

1. En Windows, con Visual Studio 2019/2022 (o el SDK de .NET), corre `compilar.bat`.
2. Copia `Rebith.App\bin\Release\net48\` a la PC. Son 3 archivos: `Rebith.exe`, `Rebith.exe.config` y `Rebith.Core.dll`.
   - Si prefieres un instalador, compila `instalador.iss` con Inno Setup.
3. No hay que instalar nada más: .NET Framework 4.8 ya viene en Windows 10/11, y no usa DLLs nativas.

**Primer uso:**
- **Usuario inicial:** `admin` / `admin123`. Pide cambiar la contraseña al entrar.
- **Depósito predeterminado:** `D:\RebithRespaldos` si existe el disco D:. Si no, `C:\ProgramData\Rebith\Deposito`. Se cambia en Configuración.
- **Respaldos viejos:** en Configuración → **Importar...**, elige el `backups.db` de la versión en Python (normalmente en `%LOCALAPPDATA%\rebith`).
- **Respaldos con la ventana cerrada:** en Configuración → **Registrar**. Windows correrá `Rebith.exe /programado` cada 15 minutos.

## Pruebas

```bash
dotnet run --project Rebith.Tests
```

Son 25 pruebas del motor: respaldo, restauración, corrupción, réplica, cancelación, candado, programador, usuarios, auditoría e importación desde Python. Usan el código Python real para crear la base que se importa. Necesitan el SDK de .NET 8; no se instalan en la línea.

## Cómo está hecho por dentro

**Depósito:** una carpeta autocontenida, sin base de datos.

```
deposito.json        identidad del depósito
objetos\ab\<sha256>.bin   contenido comprimido (zlib, igual que Python) y sin duplicados
respaldos\<id>.json  un manifiesto por respaldo (lista de archivos + huella de la lista)
config\              objetivos, usuarios, ajustes, estado de verificación (con copia .bak)
auditoria\           bitácora encadenada
reportes\            reportes CSV de cada verificación
```

**Reglas que lo hacen robusto:**
- **Nunca queda nada a medias.**
  - Todo se escribe en un temporal y se publica de un solo movimiento.
  - El manifiesto se escribe al final.
  - Un corte de luz deja el estado anterior intacto.
- **Un archivo con problemas no tumba el respaldo.**
  - Si está en uso, sin permiso o cambió mientras se leía, se reintenta 3 veces, se anota y se sigue.
  - Si el problema es del depósito (disco lleno, red caída), el respaldo se detiene y no queda registrado.
- **Todo lo nuevo se relee y se comprueba antes de dar el respaldo por bueno.**
- **Archivos de cualquier tamaño:** se procesan en bloques de 1 MB. Un `.vpp` de 400 MB usa la misma memoria que uno de 1 KB.
- **Una operación a la vez:** un candado de archivo, que Windows suelta solo si el programa se cae.
- **Limpieza segura:** si un solo manifiesto no se puede leer, la limpieza no borra nada.
- **Restaurar:**
  - Cada archivo se comprueba con su huella antes de reemplazar el destino.
  - Recupera su fecha original.
  - Si se restaura sobre la ubicación original, antes se hace un respaldo automático para poder volver atrás.
- **La ventana nunca se congela:** las operaciones van en segundo plano, con progreso y Cancelar. Un error inesperado se anota en el log y la ventana sigue viva.

## Verificación reforzada

| Nivel | Qué hace |
|---|---|
| Comparar con el origen | OK / MODIFICADO / FALTA / **NUEVO** / ILEGIBLE. Rápida (tamaño + fecha, huella si difieren) o exacta (huella de todo). |
| Integridad del respaldo | Descomprime cada contenido y compara su SHA-256. Detecta manifiestos alterados. |
| Autocuración | Lo dañado se repara desde la réplica. Si no hay réplica, se repara en el siguiente respaldo si el original sigue igual. |
| Revisión automática | Revisa todo el depósito cada N días (7 por defecto) y guarda un reporte CSV. |
| Réplica | Segunda copia en otro disco o en la red, actualizada en cada respaldo. |
| Auditoría encadenada | Cada línea lleva la huella de la anterior. Si alguien borra o edita una línea, se detecta. |

## Diferencias con la versión en Python

- **Permisos que sí se aplican.** Sin sesión solo se consulta. Cada acción pide el permiso correspondiente, y la sesión se cierra tras 15 minutos sin uso.
- **Contraseñas en PBKDF2 con sal.** Las de Python siguen sirviendo y se convierten al entrar.
- **Un objetivo por carpeta.** Python creaba un objetivo nuevo en cada respaldo manual.
- **Programación por objetivo** (diaria o semanal, a una hora fija) y retención automática ("conservar los últimos N").
- **Si la PC estaba apagada a la hora programada, el respaldo corre al volver.** Si falla, se reintenta en 1 hora.
- **Respaldos incrementales:** los archivos sin cambios no se vuelven a leer. Si quieres que siempre se lean todos, hay una opción por objetivo.
- **Corregidos varios errores de la versión en Python:**
  - restaurar tronaba en la pestaña Historial;
  - "Reparar todos" ignoraba los modificados;
  - no se detectaban archivos nuevos;
  - el programador se detenía si se borraba el objetivo.
