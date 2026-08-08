# CRM RedG — Mappa funzionale

> Documento schematico ricavato dal codice (`CRM/Client/Pages`, `CRM/Server/Controllers`, `CRM/Server/Services`, `CRM/Shared`).
> Aggiornato al 27/07/2026 — branch `upgrade-dotnet-10`.

---

## 1. Architettura in breve

| Livello | Progetto | Tecnologia |
|---|---|---|
| UI operatori | `CRM/Client` | Blazor WebAssembly + Radzen + Syncfusion, PWA/service worker |
| UI cliente finale | `CRM/Client` (layout separato) | `MainLayoutCustomer` + `NavMenuClient` |
| API / host | `CRM/Server` | ASP.NET Core, EF Core, Identity, SignalR |
| Modelli condivisi | `CRM/Shared` | POCO + DTO + enum + risorse di localizzazione |
| Test | `CRM/Tests` | xUnit (schedulazione, fasi, giorni lavorativi, login esterni) |
| Extra | `CRM.Mobile`, `CRM.API`, `AGPdfViewer`, `BlazoringComponents` | MAUI / API separata / componenti |

**Trasversali:** autenticazione Identity + policy per ruolo, localizzazione multilingua, tema chiaro/scuro (`ThemeToggle` + variabili `--crm-*`), notifiche real-time SignalR, web push, modalità manutenzione (app offline programmata), log eventi, archiviazione.

**Ruoli e policy** (`Shared/Authorize.cs`): `Admin` → `SuperUser` → `Standard` → `Client`, in cascata (`PolicyRoles.vPoliyRoles`).

---

## 2. Menu principale (operatori)

```
Dashboard
Agenda
CRM ──────────► Aziende · Contatti
Commerciale ──► Lead · Deals · Forecast · Preventivi · Ordini · Fatture · Automazioni
Service ──────► Ticket · Pianificazione · Feedback clienti (Admin)
Commesse
Catalogo ─────► Catalogo · Prodotti · Articoli
AI ───────────► Assistente · Ricerca AI
Impostazioni (SuperUser)
About
```

**Menu cliente finale:** Home · La mia azienda · Catalogo · Articoli · Ticket · Assistente AI · About.

---

## 3. Moduli funzionali

### 3.1 Dashboard e Agenda
- Dashboard operatore con contatori ticket per stato/assegnazione (`DashBoard/Index`, `DashBoard/Tickets/{TypeSearch}/{IdUser}`).
- Dashboard dedicata cliente (`DashBoardClient`).
- **Agenda** (`/Agenda`): calendario server-side (`CalendarService`) che aggrega attività + ticket, con scope *Mie / Utente / Team*.

### 3.2 Anagrafiche CRM
- **Aziende** (`Companies`): scheda, edit, ricerca, **albero gerarchico** (`Companies/Tree`), tab articoli/ticket/contatti/attività, vista dedicata cliente.
  - Concetto di **azienda madre** (`CompanyType.HeadCompany`), installazione single-tenant.
- **Contatti** (`Contacts`): CRUD, collegamento ad azienda, timeline attività.
- **Utenti azienda** (`/Company/{IdCompany}/Users`): utenti applicativi legati al cliente.
- **Contratti** (`CompanyContracts` + `ContractTypes` + `ContractTypeTicketTypes`): tipi contratto, ticket type ammessi, monte ore/assistenza acquistata.

### 3.3 Commerciale
| Sotto-modulo | Contenuto |
|---|---|
| **Lead** | Stati `New → Contacted → Qualified → Converted / Lost`; sorgenti `Manual, Website, Email, Phone, Referral, Campaign, Social, Event`; conversione in azienda/deal |
| **Deals** | Stati `Open, Suspended, CloseWon, CloseLost, Missing`; fasi `InitialContact → NeedsChecked → DecisionMakingPhase → OfferSubmitted → Obtained`; tab attività e prodotti d'interesse |
| **Forecast** | `/Deals/Forecast` — previsione commerciale (`CommercialForecastDTO`) |
| **Preventivi** | Stati `Draft, Sent, Accepted, Rejected…`; **revisioni** (stesso numero + `rev.N`); generazione PDF (`QuotePdfGenerator`); listini prezzi |
| **Ordini** | Stati `Confirmed, InProduction, Delivered, Invoiced`; righe con `RowProductionStatus` (`None, Ready, InProduction, Closed`) che pilotano la produzione; PDF (`OrderPdfGenerator`) |
| **Fatture** | Stati `Draft, Issued, Sent, Delivered…`; fattura di cortesia PDF + **XML FatturaPA** (`FatturaPaXmlBuilder`); provider SdI astratto (`IEInvoiceProvider`, oggi `NullEInvoiceProvider`) |
| **Listini** | `Settings/PriceList`, `CompanyPriceList`, `CompanyProductPrice`, `PriceListItem` — prezzi dedicati per cliente |
| **Automazioni** | `WorkflowAutomations` + `/Executions`: trigger `LeadCreated, LeadQualified, LeadConverted, DealCreated, DealWon…`, filtri (importo minimo, sorgente, stato, fase), azione `CreateActivity` con scadenza in giorni; esecuzione via `WorkflowAutomationBackgroundService` |

**Ciclo documentale:** Preventivo → Ordine → Fattura, con immutabilità dei documenti a valle.

### 3.4 Attività (timeline)
- Entità `Activity` polimorfica su `Company, Contact, Lead, Deal…`.
- Tipi: `Call, Email, Meeting, Note, Task`; stati `Planned, Done…`.
- Promemoria con `ReminderStatus` (`Pending/Sent/Failed`) e minuti di preavviso configurabili **per tipo attività** (`GlobalSetting.ActivityReminderMinutes*`).
- `ReminderBackgroundService` invia i solleciti; dialog di agenda/completamento attività.

### 3.5 Service / Ticket
Cuore operativo del sistema.

**Ciclo ticket**
- Creazione guidata a step (`TicketCreateSteps`: azienda → tipo → prodotto → data → descrizione → scadenza → assegnazione → conferma → risultato).
- Assegnazione multipla utenti (`TicketUserAssignment`, `Assign.razor`), gruppi e tipi ticket per gruppo/utente.
- Stati configurabili (`TicketStates`), priorità `Low/Medium/High…`, tipi supporto `Phone, Web, OnSite, Office, Remote, Workshop`.
- Filtri predefiniti (`TicketTypeSearch`): tutti, non assegnati, assegnati, scaduti, chiusi, in lavorazione, nuovo messaggio, da fatturare.
- **Blocco/sblocco ticket** con notifica dedicata (`TicketBlockDialog`, `TicketBlockNotificationService`).
- **Chat ticket** (`TicketChats`) con letture per utente e notifiche.
- **Feedback cliente** (`TicketFeedbacks`) con badge non letti in menu e media voti.
- **Pianificazione**: `Tickets/Schedule`, `Scheduler`, `SchedulerTimeline`, `TicketPlanningPicker`, limiti orari e numero massimo ticket/mese da `GlobalSetting`.
- **Preavvisi ticket**: su data appuntamento e su scadenza, minuti configurabili, push+email (`TicketReminderBackgroundService`).
- **Riepilogo AI** del ticket (`TicketSummaryService`).

**Interventi (rapportini)**
- `TicketInterventions`: attività svolte, parti montate, note, start/end, minuti.
- Tempistiche multiple (`TicketInterventionTime`), tecnici multipli (`TicketInterventionUsers`), articoli utilizzati (`TicketInterventionArticles`), tipi intervento multilingua.
- **Firma cliente**: firma su schermo (`SignaturePad`/`SignatureOverlay`), **firma remota** (`/RemoteSignature`, `/ConfirmSignature`) con **OTP via SMS o email** (`SignatureOtpService`, layer SMS provider-neutrale/Twilio); stato firma `Pending/…`; elenco firme in sospeso (`/TicketsIntervention/PendingSignatures`).
- **PDF rapportino** (`InterventionPdfGenerator`) + ReportViewer + upload report.
- **Note spese** (`ExpenseReceipts`): upload scontrino, **estrazione automatica OCR/AI** di importo, IVA, data, esercente, valuta con punteggio di confidenza e conferma manuale (`ReceiptProcessorService`).
- **Tipologia di spesa** (`ExpenseCategory`: vitto, alloggio, trasporti, carburante, pedaggi, parcheggi, rappresentanza, materiali, formazione, telefonia): proposta in tre livelli — sottotipo riconosciuto dall'OCR, dizionario esercenti/righe, e solo se entrambi tacciono il modello AI (`ExpenseCategorizer`, spegnibile da `GlobalSetting.ExpenseCategoryAiEnabled`). Resta sempre una **proposta confermabile**; la proposta viene conservata anche se corretta, per misurarne l'accuratezza. Filtro per tipologia e per spese non classificate, spaccato per voce nel riepilogo.

**API ticket esterna**
- `api/external/tickets` con API key (`ExternalTicketApiKeys`) per apertura/lettura ticket da sistemi terzi.

### 3.6 Produzione / Commesse (MTO)
- Una **Commessa per unità di riga d'ordine** (qty 3 → 3 commesse).
- Stati: `Planned, InProgress, Suspended, Testing, Completed, Delivered`.
- **Fasi a Gantt** clonate da un **template per prodotto** (`GanttPlan` / `GanttPhase`, gestiti in `Settings/GanttPlans`).
- Fasi (`CommessaFase`): stati `Pending, InProgress, …`; modalità di completamento `Manual, AllTicketsClosed, AnyTicketClosed`; **dipendenze vincolanti** `FinishToStart, StartToStart, FinishToFinish`.
- **Fase ↔ ticket 1:1**: la presa in carico apre un ticket precompilato, la chiusura chiude la fase, la riapertura la riapre; auto-creazione ticket `Manual / OnPhaseStart`.
- **Schedulazione all'indietro** su **giorni lavorativi** (`WorkdayTests`, `SchedulingTests`, `RescheduleTests`).
- **Cascata avanzamento**: fasi → commessa → riga d'ordine (`ProductionStatus = Closed`).
- Componente Gantt custom (`RedGGantt`) con interop JS.
- Dialog produzione interna (`InternalProductionDialog`).

### 3.7 Catalogo, Prodotti, Articoli
- **Catalogo** (`Catalog`): vetrina prodotti con asset multimediali (`ProductCatalogAssets`, tipi media), visibile anche al cliente.
- **Prodotti** (`Products`): tipi prodotto, parametri, accessori e tipi accessorio (multilingua), relazioni **padre/figlio** (`ProductParentChild`), import CSV.
- **Articoli** (`Articles`) = macchine/matricole installate presso il cliente: stati e transizioni di stato (`ArticleState`, `ArticleStateTransition`), **domini** (`ArticleDomain`), **eventi** (`ArticleEvent`/`ArticleEventType`), accessori, import CSV, ticket collegati.
- **Licenze articolo** (`ArticleLicense`): feature definibili per tipo prodotto/prodotto (`Bool/Int/String`), chiave macchina, validità, firma RSA (`RsaLicenseService`, `MachineLicenseController`).
- **Knowledge di prodotto** (`ProductKnowledge`).

### 3.8 Macchine / IoT
- **API macchina** `api/machine` protetta da API key con permessi (`MachineParameterApiKeys`, gestite in `Settings/MachineParameterApiKeys`): elenco articoli, download ultimo backup per prodotto/articolo, upload backup.
- **Backup macchina** (`MachineBackups`): versionamento, SHA-256, dimensione, origine `Manual/…`, riferimento esterno.
- **Parametri macchina** (`MachineParameters`, `ArticleParameters`).

### 3.9 Intelligenza artificiale
| Funzione | Dettaglio |
|---|---|
| **Assistente CRM unificato** | `/Tickets/Assistant` — chat in streaming NDJSON con tool-use Claude su dati CRM + soluzioni da ticket chiusi + KB; risposte Markdown con link; tool `create_ticket` con permessi utente e conferma esplicita |
| **Ricerca semantica** | `/Tickets/Search` — embedding OpenAI (`text-embedding-3-small`), similarità ticket, rerank opzionale (`GlobalSetting.TicketRerankMode`) |
| **Knowledge Base** | `/Knowledge` — import documenti, embedding, statistiche, match |
| **Riepilogo ticket** | sintesi automatica del ticket |
| **Triage email in ingresso** | riassunto + verdetto AI, non bloccante |
| **OCR note spese** | estrazione campi da scontrini/fatture |
| **Tipologia note spese** | voce di rimborso proposta in cascata: sottotipo del documento → dizionario esercenti → modello (`ExpenseCategoryAiClient`, opt-in); soglia di confidenza in `GlobalSetting.ExpenseCategoryMinConfidence` |
| **Input vocale** | `VoiceTranscriptionService`, abilitazione via `GlobalSetting.VoiceInputMode` |
| **Log assistente** | `Settings/AssistantLogs` — domande, risposte, feedback pollice su/giù |

Provider configurati in `appsettings.json`: `Anthropic:ChatModel` (chat/summary), `Anthropic:JudgeModel`, `OpenAI:ChatModel`, `OpenAI:EmbeddingModel`.

### 3.10 Email e comunicazioni
**Uscita**
- Canali multipli con failover configurabili da UI (`Settings/Smtps`): SMTP, Brevo, SendGrid (`EmailProvider`).
- **Outbox con retry** (`EmailOutboxBackgroundService`, stati `EmailOutboxStatus`).
- **Template email** multilingua (`Settings/EmailTemplates`) con legenda placeholder, anteprima, invio di test, generazione dei mancanti. Tipi: conferma email/registrazione, invito, invio documento, nuovo ticket, ticket da assegnare, ticket assegnato, nuovo allegato, nuovo utente, nuovo messaggio chat, conferma firma, promemoria, ticket bloccato/sbloccato, reset password.
- **Storico invii** (`EmailsSent`) + **webhook engagement** (`EmailWebhooks`, `EmailEventType`, `EmailEngagementStatus`: aperture, click, bounce).

**Ingresso**
- **Caselle in ingresso** (`Settings/EmailInboxes`): modalità `Imap` o `InboundParseEsp`; azione `ActivityOnContact` o `NewTicket`.
- `EmailInboxBackgroundService` → **email → attività** oppure **email → ticket** con threading tramite token `[#T{id}]`, auto-ack nella lingua rilevata dall'AI, allegati copiati, link bidirezionale.
- **Triage** in `Settings/InboundEmails`.

**Altri canali**
- **Telegram** (`TelegramAppConfigs`, `WTelegramService`, `TelegramCommandsService`, bot utente).
- **Web push** (`PushNotificationService`, `Settings/push-notifications`).
- **SignalR** (`SignalRHub`) per notifiche e aggiornamenti live.
- **SMS** (layer `Server/Services/Sms`, usato per OTP firma).

### 3.11 Documenti e allegati
- **Allegati** (`Attachments`, `AttachmentFiles`): tipi, visibilità (`AttachmentVisibilities`), upload, dettaglio.
- **Cartelle** (`Folders` + `FolderLanguages`) per organizzare la documentazione, multilingua.
- **DocViewer** (`/DocViewer/{Id}`) e visualizzatore PDF (`AGPdfViewer`).
- **Conversione DOCX → PDF** (`LibreOfficeDocxToPdfConverter`), **estrazione testo** (`DocumentTextExtractor`).
- **Loghi** (`Settings/Logos`) usati in report e header sito.
- **Report/stampe** (`Prints`, `Reports`, `ReportTypes`).

### 3.12 Amministrazione e configurazione
Pagina `Settings` a schede, raggruppate in:

| Sezione | Voci |
|---|---|
| Gestione utenti | Utenti (+ ruoli, profilo, conferma) · Gruppi · Utenti-gruppi |
| Prodotti e ticket | Tipi prodotto · Tipi ticket (+ lingue) · Stati ticket · Tipi intervento (+ lingue) · Impostazioni prodotti · Listini |
| Comunicazioni | SMTP/provider · Caselle in ingresso · Email in ingresso · Template email · Telegram |
| Sistema | Lingue · Cartelle · Loghi · **Impostazioni globali** · Log eventi · Chiavi API macchina · **Manutenzione server** |
| AI | Knowledge Base · Log assistente |
| Produzione | Template Gantt (`Settings/GanttPlans`) |

**Impostazioni globali** (`GlobalSetting`): giorni scadenza ticket, fascia oraria pianificazione, max ticket/mese, Telegram on/off, firma remota on/off, logo report e header, colore barra, aliquota IVA di default, regime fiscale, modalità input vocale, modalità rerank AI, preavvisi ticket (appuntamento/scadenza) e preavvisi attività per tipo.

**Manutenzione** (`Settings/Maintenance`): banner di avviso agli utenti e messa offline programmata dell'app (`MaintenanceAppOfflineBackgroundService`, `MaintenanceBanner`).

**Altro:** log eventi di sistema (`LogEvents`), archiviazione (`ArchiveService`), import CSV con mapping colonne (`CSVImportController`), licenze applicative (`LicensesController`).

### 3.13 Portale cliente
Layout e menu separati, con accesso limitato a: home, scheda della propria azienda, catalogo, articoli installati, propri ticket (creazione, chat, allegati, feedback) e assistente AI.

---

## 4. Servizi in background

| Servizio | Compito |
|---|---|
| `ReminderBackgroundService` | promemoria attività |
| `TicketReminderBackgroundService` | preavvisi appuntamento e scadenza ticket |
| `WorkflowAutomationBackgroundService` | esecuzione automazioni commerciali |
| `EmailOutboxBackgroundService` | invio email con retry e failover |
| `EmailInboxBackgroundService` | polling IMAP → attività/ticket |
| `MaintenanceAppOfflineBackgroundService` | messa offline programmata |
| `WTelegramService` | client Telegram |

---

## 5. Stato noto / aree aperte

- **Sicurezza API:** ora fail-closed (`MapControllers().RequireAuthorization()` + whitelist endpoint pubblici) e row-level security su Quotes/Orders/Invoices/Deals. **Restano da fare:** cifratura dei segreti a riposo e copertura di test sull'autorizzazione.
- **Fatturazione elettronica:** manca l'adapter verso un provider SdI reale (oggi `NullEInvoiceProvider`).
- **Attività:** manca il tab attività sulla scheda Contatto.
- **Email:** rifiniture aperte su allegati inbound, threading via header e IMAP IDLE.
- **Tooling EF su .NET 10:** `dotnet ef migrations add` non funziona con l'SDK 10.0.302 — le migration vanno scritte a mano e applicate con `dotnet ef database update --no-build`.
