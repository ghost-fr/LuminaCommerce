# AEAT VeriFactu schemas (mirror)

**Provenance:** Third-party mirror (`github.com/hectorsipe/aeat-verifactu`), packaged as
`aeat-official-schemas-mirror.zip` on Drive (Claude, 2026-09-11).

**Not** a direct download from `sede.agenciatributaria.gob.es`. Before production use,
diff these files against the official AEAT technical package in a browser.

## Files

| File | Role |
|------|------|
| `SistemaFacturacion.wsdl` | SOAP service definition |
| `SuministroInformacion.xsd` | Core types including RegistroFacturacionAlta (8 hash fields) |
| `SuministroLR.xsd` | Supply / booking types |
| `ConsultaLR.xsd` / `RespuestaConsultaLR.xsd` | Query |
| `RespuestaSuministro.xsd` | Supply response |
| `EventosSIF.xsd` | SIF events |
| `xmldsig-core-schema.xsd` | XML signature |

## Endpoints (from WSDL / findings)

- Production: `www1` / `www10`.agenciatributaria.gob.es
- Preprod: `prewww1` / `prewww10`.aeat.es (`*10` = certificate-authenticated)

See `docs/CLAUDE_VERIFIED_FINDINGS_2026-09-11.txt`.
