# Run Lumina POS locally (simple mode)

## 1. Pull branch
```powershell
git fetch origin
git checkout feat/ui/local-settings
git pull
```

## 2. Optional: seed backend (full features)
```powershell
dotnet run --project tools/Lumina.SeedDev
# set LUMINA_TENANT_ID from output
```
If you skip seed, the app still starts in **LOCAL MODE**.

## 3. Run
```powershell
dotnet run --project src\Lumina.Pos.UI
```

## 4. After login
- **Caja / TPV** — sell (local cart if no register)
- **Productos locales** — add/remove products saved under `%LocalAppData%\LuminaPos\local-settings.json`
- **Configuración** — store name, DB path, tenant GUID, fiscal on/off

## Settings file
`%LocalAppData%\LuminaPos\local-settings.json`

FiscalEnabled = false by default (simple POS, no Veri*Factu required).
