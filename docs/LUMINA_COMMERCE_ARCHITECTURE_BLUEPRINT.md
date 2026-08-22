# Lumina Commerce Platform  
## Architectural Blueprint (v1.0)

This document describes a modern, independently designed retail, POS and back-office platform.  
It covers the same functional domains as classic Spanish retail systems (catalogue, pricing, POS tickets, commercial documents, stock, receivables, reporting) while replacing monolithic global state, typed DataSets and tight UI coupling with clean architecture, explicit contexts, and first-class VeriFactu compliance.

---

## 1. High-Level Architecture

```text
┌─────────────────────────────────────────────────────────────────────┐
│  Presentation Layer                                                 │
│  • POS Client (desktop / tablet / kiosk)                            │
│  • Back-office Web / Desktop Admin                                  │
│  • Mobile Store App                                                 │
│  • Self-service / Customer Display                                  │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ thin UI, commands & queries only
┌──────────────────────────────▼──────────────────────────────────────┐
│  Application Layer (use-cases / commands / queries)                 │
│  • SaleCommandHandler, InvoiceCommandHandler, StockAdjustHandler…   │
│  • PricingService, TaxCalculator, PromotionEngine                   │
│  • VeriFactuRecordService, HashChainService, QrCodeService          │
│  • SessionContext / StoreContext / UserCapabilities                 │
└──────────────────────────────┬──────────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────────┐
│  Domain Layer                                                       │
│  Aggregates: Product, PriceList, Sale, Invoice, StockLedger,        │
│              Customer, Supplier, Register, Document, Receipt…       │
│  Policies, invariants, domain events                                │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ ports (interfaces)
┌──────────────────────────────▼──────────────────────────────────────┐
│  Infrastructure Layer                                               │
│  • PostgreSQL / SQL Server (write models + read models)             │
│  • Event Store / Outbox for integration & fiscal chaining           │
│  • VeriFactu AEAT client (XML + SOAP/REST submission)               │
│  • Device adapters: printers, scales, cash drawers, scanners,       │
│    biometric, price readers                                         │
│  • Message bus, file exchange, accounting export, reporting engine  │
└─────────────────────────────────────────────────────────────────────┘
```

Key design principles:
- UI is deliberately thin.
- No global singleton configuration or shared mutable data contexts.
- Every sale / invoice / cancellation is an atomic application command that produces immutable domain events and a VeriFactu invoicing record.
- Fiscal compliance is an explicit port, not an afterthought bolted onto document saving.

---

## 2. Startup, Session & Navigation

- Process entry creates a host, registers DI services, loads encrypted configuration, and establishes the active `TenantContext` + `StoreContext` + `UserSession`.
- Configuration is versioned, environment-aware (local / central / multi-store) and never mutated at runtime without an explicit admin command.
- Navigation is driven by capability-based menus (role + store + feature flags). Commands open screens or SPA routes, always receiving an explicit session and the required application services.
- Long-lived data facades are replaced by short-lived unit-of-work / repository scopes and read-model queries.
- Busy indicators, status messages and child-window tracking remain UI concerns only.

---

## 3. Module Map

| Module | Core responsibilities | Main relationships |
|--------|-----------------------|--------------------|
| Foundation & Multi-tenancy | Tenants, companies, stores, users, roles, capabilities, audit log, configuration | Supplies `UserSession` and `StoreContext` to every use-case |
| Product Catalogue | Products, barcodes, units, families, properties, VAT rates, suppliers | Feeds pricing, sales, purchasing and stock |
| Pricing & Promotions | Price lists, cost history, margins, offers, discounts, loyalty points | Deterministic price resolution service used by POS and documents |
| POS / Sales | Cart, barcode scan, tenders, register open/close, cash movements, tickets | Produces Sale aggregate → VeriFactu record → stock movement → register totals |
| Commercial Documents | Quotes, orders, delivery notes, invoices, credit notes | State machine; conversion rules; numbering series; links to tickets |
| Purchasing | Purchase orders, goods receipts, supplier invoices, price updates | Updates costs and can trigger selling-price recalculation |
| Inventory & Logistics | Stock ledger (append-only), counts, adjustments, transfers, shrinkage | Sale and purchase commands emit stock events |
| Customers & Receivables | Customer master, balances, receipts, remittances, payment methods | Invoice → receipt lifecycle |
| Reporting & Analytics | Read models, daily sales, tax, margins, product/store reports, exports | Separate from write path; eventual consistency accepted |
| Devices & Integrations | Printers, scales, scanners, cash drawers, biometric, price readers, SES | Isolated behind ports; failures never corrupt the sale |
| Fiscal Compliance | VeriFactu record generation, hash chain, QR, AEAT submission, declarations | Mandatory for every invoice and simplified invoice (ticket) |
| Operations | Backup, restore, multi-store sync, offline queue, scheduled jobs | Outbox / inbox pattern with idempotent handlers |

---

## 4. Data & Flow Model

```text
UI / Device
    │
    ▼
Application Command (SaleCommand, IssueInvoiceCommand, CancelDocumentCommand…)
    │
    ├─ Domain Aggregate validation & business rules
    ├─ Pricing / Tax / Promotion resolution
    ├─ Stock reservation or ledger entry
    ├─ Payment / tender recording
    │
    ▼
Unit of Work
    ├─ Persist aggregate state + domain events
    ├─ Generate VeriFactu Invoicing Record (RF)
    │     • sequential number within series + SIF
    │     • SHA-256 hash chained to previous record
    │     • timestamp, NIF, totals, VAT breakdown
    ├─ Write Outbox message (AEAT submission, stock sync, accounting…)
    └─ Commit

Async workers
    ├─ Submit RF to AEAT (VERI*FACTU mode) or store securely (non-VERI*FACTU)
    ├─ Generate QR payload and render on ticket / invoice
    ├─ Update read models and register totals
    └─ Publish integration events
```

- Write models use an ORM or explicit repositories with strong transactions.
- Stock is an append-only ledger; balances are derived.
- Reporting uses dedicated read models (materialised views or projections).
- Ad-hoc SQL is forbidden outside controlled query services; all business operations go through typed commands.

---

## 5. Key Workflows (with VeriFactu)

### 5.1 POS Sale (Ticket / Simplified Invoice)

1. Operator selects register, employee, optional customer and price list.
2. Barcode / product lookup → PricingService resolves current price, tax, offers, discounts.
3. Cart is validated; totals and VAT are calculated.
4. Tender is accepted (cash, card, mixed…).
5. `CompleteSaleCommand` runs inside a transaction:
   - Creates immutable Sale aggregate.
   - Emits StockMovement events.
   - Generates next VeriFactu record in the store/register chain (or company-level chain according to chosen SIF boundary).
   - Computes SHA-256 hash including previous hash, NIF, series/number, date, total, VAT.
   - Persists record + outbox entry.
6. Ticket is printed / displayed with:
   - Mandatory QR code (30–40 mm, high contrast, quiet zone ≥ 2 mm).
   - Text “QR tributario” and, when operating in VERI*FACTU mode, “Factura verificable en la sede electrónica de la AEAT” or “VERI*FACTU”.
7. Background worker submits the record to AEAT (if VERI*FACTU mode is active) and records the CSV / acknowledgement.
8. Register totals and daily aggregates are updated.

### 5.2 Full Invoice or Credit Note

Same flow as above, but the document type is a full invoice (or rectificativa).  
Numbering series, customer identification and additional legal fields are required.  
The same hash-chain and QR rules apply.

### 5.3 Cancellation / Rectification

- Never delete or silently mutate a fiscal record.
- Issue a new cancellation or rectification record that references the original and continues the hash chain.
- Both records are submitted / stored according to the active VeriFactu mode.

### 5.4 Inventory Movement

1. Create count, adjustment or transfer header + lines.
2. Compare physical vs system quantities.
3. Emit StockLedger entries with reason codes and source document reference.
4. No fiscal record is generated unless the movement itself produces a commercial document.

### 5.5 Customer Collection / Remittance

Standard receivable workflow; receipts can optionally generate their own fiscal records when required by local rules.

### 5.6 Reporting

Filters → read-model queries → ActiveReports / Excel / PDF / charts.  
Fiscal reports can pull the immutable VeriFactu record store for audit.

---

## 6. Core Domain & Application Components

- **TenantContext / StoreContext / UserSession** – explicit, immutable-for-the-request contexts that replace any global static state.
- **PricingService** – pure function that resolves price, tax inclusion, offers and discounts given product, customer, store, date and quantity.
- **SaleAggregate / InvoiceAggregate** – enforce invariants (totals, tax, stock availability, fiscal status).
- **VeriFactuRecordService** – builds the standardised invoicing record (RF), calculates the chained SHA-256 hash, and prepares the AEAT XML payload.
- **HashChainRepository** – stores the previous hash per SIF / series / store (boundary configurable per company policy).
- **QrCodeService** – generates the official AEAT URL + QR image meeting size, contrast and placement rules.
- **OutboxProcessor** – reliable, idempotent submission to AEAT and other external systems.
- **StockLedger** – append-only; balances are projections.
- **DocumentStateMachine** – quote → order → delivery note → invoice → credit note; fiscal closure is irreversible.
- **CapabilityAuthorizer** – fine-grained checks for price overrides, refunds, void, stock adjustment, etc.
- **DevicePort** implementations – OPOS / ESC-POS / scale protocols isolated behind interfaces.

---

## 7. Technology Stack Recommendations

| Layer | Suggested technologies |
|-------|------------------------|
| Runtime | .NET 8+ (or later LTS) |
| UI | Avalonia / MAUI / Blazor Hybrid for POS; React / Blazor for back-office |
| Persistence | PostgreSQL or SQL Server + EF Core / Dapper; optional EventStoreDB for high-volume fiscal chains |
| Messaging | MassTransit / NServiceBus / RabbitMQ or Azure Service Bus for outbox |
| Reporting | Stimulsoft / FastReport / QuestPDF + Excel export |
| Devices | Abstraction over OPOS, ESC-POS, serial scales, DirectShow cameras |
| VeriFactu | Official AEAT XSD + SOAP/REST client; local XML storage + retry queue |
| Security | Certificate-based AEAT authentication, encrypted configuration, audit log |
| Observability | OpenTelemetry, structured logs, business-event metrics, device health |

---

## 8. VeriFactu Compliance Design

The platform treats VeriFactu as a first-class concern rather than an optional export.

### 8.1 Mandatory Capabilities (all SIF modes)

- Generate a standardised Invoicing Record (Registro de Facturación) for every invoice and every simplified invoice (ticket).
- Calculate a SHA-256 hash over the prescribed fields (NIF, series + number, date, invoice type, VAT amount, total amount, previous hash…).
- Chain records: each new record includes the hash of the immediately preceding record of the same SIF.
- Guarantee integrity and inalterability: once emitted, a record cannot be modified; corrections produce new linked records.
- Persist records in a durable, accessible, readable store with full audit trail.
- Embed a compliant QR code on every printed / electronic invoice and ticket:
  - Size 30 × 30 mm to 40 × 40 mm
  - ISO/IEC 18004, error-correction level M
  - Quiet zone ≥ 2 mm (preferably 6 mm)
  - High contrast
  - Placement at the beginning of the document (top / top-left according to orientation)
  - Preceded by the text “QR tributario”
- When operating in VERI*FACTU mode, add the legend “Factura verificable en la sede electrónica de la AEAT” or “VERI*FACTU”.

### 8.2 VERI*FACTU Mode (optional but recommended)

- Automatic near-real-time submission of every RF to AEAT services.
- Receipt of CSV / acknowledgement and storage of submission status.
- Flow-control (throttling) according to AEAT guidelines.
- Automatic retry with exponential back-off and dead-letter queue for permanent failures.
- Ability to start / stop VERI*FACTU mode with the legal retention period (end of the calendar year in which the mode was active).

### 8.3 Non-VERI*FACTU Mode

- Same integrity, chaining and QR obligations.
- Additional local controls and event logging for chain verification and anomaly detection (as required by the regulation for systems that do not transmit).

### 8.4 SIF Boundary Decision

The platform supports configurable chaining scope:
- One chain per company
- One chain per store
- One chain per independent POS terminal

The chosen boundary is recorded in the responsible declaration and respected by the HashChainRepository.

### 8.5 Producer Obligations

- Electronically signed “Declaración Responsable” stating that the software meets RD 1007/2023 and Orden HAC/1177/2024.
- Versioned release notes and configuration that demonstrate continuous compliance.
- Ability to export the complete record chain and event log for AEAT inspection.

### 8.6 Timeline Awareness (as of August 2026)

- Software vendors must already offer compliant products.
- Corporate taxpayers (Impuesto de Sociedades): systems adapted before 1 January 2027.
- Remaining obligors (including most self-employed): before 1 July 2027.
- The platform is designed so that enabling VeriFactu is a configuration switch, not a rewrite.

---

## 9. Recommended Architecture Principles (recap)

- Keep the UI thin; push all business and fiscal logic into application services and domain aggregates.
- Replace any global static state with explicit, request-scoped contexts injected via DI.
- Model the core concepts (Product, Sale, Invoice, StockMovement, VeriFactuRecord…) as rich domain objects, not as generated DataRows.
- Treat every fiscal emission as an immutable event that continues a cryptographic chain.
- Isolate hardware and external tax services behind ports so that device or network failures never leave a half-written sale.
- Use the transactional outbox pattern for AEAT submission, multi-store synchronisation and accounting exports.
- Prefer an append-only stock ledger and derive balances.
- Enforce document state machines; fiscal closure is irreversible.
- Authorisation is capability-based and audited.
- Observability is built-in: correlation IDs, audit trail, device health, submission status, business metrics.

---

## 10. Implementation Priority

1. **Foundation**  
   Multi-tenancy, stores, users/roles/capabilities, configuration, audit log, database migrations, session contexts, structured error handling.

2. **Product & Tax Master Data**  
   Products, barcodes, units, categories, VAT rules, customers, suppliers, payment methods, numbering series.

3. **Pricing Engine**  
   Price lists, costs, margins, promotions, deterministic resolution service.

4. **Core POS Sale + VeriFactu**  
   Cart, barcode lookup, totals, tax, tender, receipt printing, hash-chain generation, QR rendering, outbox submission.  
   This is the first fiscal-critical milestone.

5. **Stock Ledger**  
   Receiving, adjustments, counts, transfers, sale/purchase integration.

6. **Commercial Documents**  
   Quotes → orders → delivery notes → invoices → credit notes, with full VeriFactu coverage.

7. **Cash & Receivables**  
   Register open/close, cash movements, receipts, remittances, reconciliation.

8. **Reporting Read Models**  
   Daily sales, tax summaries, margins, product/store/customer reports, fiscal record export.

9. **Hardware Abstraction & Multi-store Sync**  
   Scales, printers, scanners, offline queue, reconciliation tools.

10. **Advanced Back-office**  
    Loyalty, promotions designer, accounting export, report designer, imports, scheduled jobs, backup/restore.

11. **Certification & Hardening**  
    Full VeriFactu declaration, AEAT integration tests, security review, performance under peak load, disaster-recovery drills.

---

## Scope & Confidence Notes

- The functional coverage matches the classic retail domains (catalogue, pricing, POS, documents, stock, receivables, reporting, devices).
- Architecture is deliberately independent of any legacy codebase, global statics or typed-DataSet patterns.
- VeriFactu design follows RD 1007/2023, Orden HAC/1177/2024 and the published AEAT technical specifications for hash chaining, QR content and submission.
- Exact AEAT endpoint URLs, XSD versions and throttling parameters must be taken from the current official documentation at implementation time; the architecture isolates these behind the VeriFactu port so they can evolve without affecting the rest of the system.
- Regional variants (TicketBAI for Basque Country, SII for large taxpayers) can be added later as alternative fiscal adapters without changing the core sale / invoice aggregates.

---

*Lumina Commerce Platform – designed for long-term maintainability, multi-store scale and full Spanish fiscal compliance from day one.*
