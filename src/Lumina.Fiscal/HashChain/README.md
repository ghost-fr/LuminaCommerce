# Hash chain service (placeholder)

Phase 3 work item, fiscal-critical. Will contain:
- `HashChainService` — SHA-256 over prescribed fields, chained to previous record hash
- `VeriFactuRecord` value object
- `QrPayloadBuilder` — ISO/IEC 18004 QR content per blueprint §8.1
Every change here requires new fixed-vector tests in tests/Lumina.Fiscal.Tests before merge.
