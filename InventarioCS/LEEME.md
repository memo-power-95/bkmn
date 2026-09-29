# Inventario por Racks (C#)

Reescritura en C# (.NET Framework 4.8 + WinForms) de la pestaña **Inventario (Racks)** de `inventario/inv.py`.
Es el primer módulo migrado; Remapeo, Escaneo, Clasificación, Separación y Modelos base siguen en Python.

## Compilar e instalar

1. En Windows, con Visual Studio 2019/2022 (o el SDK de .NET), corre `compilar.bat`.
2. Copia `Inventario.App\bin\Release\net48\` a la PC. Son 3 archivos: `Inventario.exe`, `Inventario.exe.config` e `Inventario.Core.dll`.
3. No hay que instalar nada más:
   - .NET Framework 4.8 ya viene en Windows 10/11.
   - SQLite se usa desde `winsqlite3.dll`, que también viene en Windows 10/11. No hay DLLs nativas que copiar.

## Datos

- Usa `retrabajo_pcb.db` **junto al .exe**, igual que `inv.py`.
- Si pones `Inventario.exe` en la misma carpeta que `inv_v3.exe`, los dos comparten los mismos racks y el mismo historial. Pueden estar abiertos a la vez.
- Para usar otra base: `Inventario.exe "C:\ruta\retrabajo_pcb.db"`.
- Solo toca las tablas `inventario_racks`, `inventario_items` e `historial`. Las demás tablas de Python no se modifican.

## Qué hace

- Racks con nombre automático (R1, R2...) o manual, de **1 a 500 trays** (en Python eran 50).
- Escaneo de Lot ID con Enter (funciona con escáner). Un Lot ID solo puede estar una vez en todo el rack; si se repite, avisa en rojo con sonido y dice en qué tray está.
- Deshacer el último del tray actual.
- Resumen de QTY por tray y total del rack. Clic en un tray lo vuelve el tray actual.
- Exportar a Excel (este rack o todos) con el mismo formato que Python. **TOTAL QTY (celda A4) es la suma de todos los racks exportados.**
- Atajo: F2 regresa el cursor a la caja de escaneo.

## Pruebas

```bash
dotnet run --project Inventario.Tests
```

Son 8 pruebas del motor: nombres de rack, límite de trays, duplicados, deshacer, historial, formato del Excel (incluido un rack de 400 trays) y compatibilidad con la base de Python (si hay `python3`, crea la base con el esquema de `inv.py` y luego lee con Python lo que escribió C#). Necesitan el SDK de .NET 8 y corren en Windows, macOS o Linux.
