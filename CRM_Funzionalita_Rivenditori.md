# CRM RedG — Catalogo funzionalità

Documento informativo per rivenditori. Elenco schematico di ciò che il sistema è in grado di fare.

---

## 1. Anagrafiche e relazioni

| Funzionalità | Descrizione |
|---|---|
| Aziende | Anagrafica clienti, fornitori e prospect con dati completi e documenti allegati. |
| Struttura di gruppo | Vista ad albero delle aziende collegate (sedi, filiali, controllate). |
| Contatti | Rubrica persone collegate alle aziende, con storico delle interazioni. |
| Utenti del cliente | Account che il cliente usa per accedere al proprio portale. |
| Contratti di assistenza | Tipologie di contratto, servizi inclusi e monte ore acquistato. |

---

## 2. Area commerciale

| Funzionalità | Descrizione |
|---|---|
| Lead | Raccolta e qualificazione dei contatti in ingresso, con origine tracciata (sito, email, telefono, fiera, campagna…). |
| Conversione lead | Trasformazione del lead qualificato in azienda e trattativa. |
| Trattative (Deal) | Gestione delle opportunità per fase di avanzamento, valore e probabilità di chiusura. |
| Forecast | Previsione di fatturato basata sulle trattative aperte. |
| Preventivi | Creazione, invio e stampa PDF; gestione delle revisioni con numerazione progressiva. |
| Ordini | Conferma d'ordine generata dal preventivo, con stato di avanzamento riga per riga. |
| Fatture | Emissione con stampa di cortesia e file XML per la fatturazione elettronica. |
| Listini personalizzati | Prezzi dedicati per singolo cliente o per listino. |
| Automazioni commerciali | Al verificarsi di un evento (nuovo lead, trattativa vinta) il sistema crea da solo il follow-up. |

**Flusso**: Lead → Trattativa → Preventivo → Ordine → Fattura.

---

## 3. Attività e agenda

| Funzionalità | Descrizione |
|---|---|
| Attività | Chiamate, email, riunioni, note e task collegati ad azienda, contatto, lead o trattativa. |
| Timeline | Storico cronologico di tutto ciò che è successo su un cliente. |
| Agenda | Calendario che unisce attività e ticket, con vista personale, per collega o per team. |
| Promemoria | Avvisi automatici prima della scadenza, con anticipo configurabile per tipo di attività. |

---

## 4. Assistenza e ticket

| Funzionalità | Descrizione |
|---|---|
| Apertura ticket guidata | Percorso a passi: cliente, tipologia, prodotto, data, descrizione, scadenza, assegnazione. |
| Assegnazione | Attribuzione a uno o più tecnici, anche per gruppo di lavoro o competenza. |
| Stati e priorità | Stati del ticket personalizzabili, livelli di priorità e tipologia di supporto (telefono, remoto, on-site, officina…). |
| Filtri rapidi | Viste preconfigurate: da assegnare, assegnati, in lavorazione, scaduti, chiusi, con nuovo messaggio, da fatturare. |
| Chat sul ticket | Conversazione tra tecnico e cliente all'interno del ticket, con notifica dei messaggi non letti. |
| Blocco ticket | Sospensione del ticket in attesa di risposta o ricambio, con avviso alle parti. |
| Pianificazione | Calendario e timeline degli interventi, con fasce orarie di lavoro e limiti configurabili. |
| Promemoria ticket | Avviso automatico prima dell'appuntamento e prima della scadenza. |
| Feedback cliente | Valutazione del servizio a chiusura ticket, con media voti e segnalazione dei feedback da leggere. |
| Ticket da sistemi esterni | Apertura e consultazione ticket tramite collegamento con software di terze parti. |

---

## 5. Interventi tecnici (rapportini)

| Funzionalità | Descrizione |
|---|---|
| Rapportino di intervento | Attività svolte, ricambi montati, note, orari di inizio e fine, tempi di viaggio e di lavoro. |
| Più tecnici | Registrazione di squadre con ore individuali. |
| Ricambi utilizzati | Elenco degli articoli impiegati nell'intervento. |
| Firma del cliente | Firma direttamente a schermo su tablet o smartphone. |
| Firma remota | Se il cliente non è presente, invio del rapportino per la firma a distanza con codice di verifica via SMS o email. |
| Firme in sospeso | Elenco dei rapportini in attesa di firma. |
| Stampa PDF | Rapportino pronto da consegnare o inviare al cliente. |
| Note spese | Foto dello scontrino: il sistema legge da solo importo, IVA, data ed esercente; il tecnico deve solo confermare. |

---

## 6. Produzione e commesse

| Funzionalità | Descrizione |
|---|---|
| Commessa per unità | Da un ordine di 3 macchine nascono automaticamente 3 commesse distinte. |
| Modelli di produzione | Template di fasi predefinite per ogni prodotto, riutilizzabili su ogni nuova commessa. |
| Diagramma di Gantt | Vista grafica delle fasi, delle durate e degli incastri. |
| Dipendenze tra fasi | Vincoli di precedenza: una fase non parte finché la precedente non è conclusa. |
| Pianificazione a ritroso | Calcolo automatico delle date partendo dalla consegna, considerando solo i giorni lavorativi. |
| Fase = ticket | Prendere in carico una fase apre il ticket di lavorazione; chiudere il ticket chiude la fase. |
| Avanzamento a cascata | Il completamento delle fasi aggiorna da solo lo stato della commessa e della riga d'ordine. |

---

## 7. Catalogo e parco macchine

| Funzionalità | Descrizione |
|---|---|
| Catalogo prodotti | Vetrina con schede, immagini e materiali, consultabile anche dal cliente. |
| Prodotti | Tipologie, parametri tecnici, accessori e distinte padre-figlio. |
| Articoli installati | Il parco macchine di ogni cliente: matricole, stato, storico eventi e ticket collegati. |
| Licenze macchina | Attivazione di funzioni a pagamento sulla singola macchina, con scadenza e chiave protetta. |
| Backup macchina | Archiviazione versionata delle configurazioni, scaricabili e ripristinabili. |
| Collegamento con le macchine | Le macchine possono dialogare con il CRM per inviare parametri e backup in automatico. |
| Import dati | Caricamento massivo di prodotti e articoli da file Excel/CSV. |

---

## 8. Intelligenza artificiale

| Funzionalità | Descrizione |
|---|---|
| Assistente AI | Chat che risponde su dati del CRM e su come sono stati risolti i ticket passati; sa anche aprire un ticket su richiesta, previa conferma. |
| Ricerca intelligente | Ricerca per significato e non per parola esatta: trova i casi simili già risolti. |
| Knowledge base | Manuali e documentazione caricati e resi interrogabili in linguaggio naturale. |
| Riepilogo ticket | Sintesi automatica di ticket lunghi e complessi. |
| Smistamento email | Le email in arrivo vengono riassunte e classificate automaticamente. |
| Lettura scontrini | Estrazione automatica dei dati dalle note spese. |
| Dettatura vocale | Inserimento testi a voce. |

---

## 9. Comunicazioni

| Funzionalità | Descrizione |
|---|---|
| Invio email | Più canali di invio configurabili, con reinvio automatico in caso di errore. |
| Modelli email | Template personalizzabili e multilingua per ogni comunicazione automatica. |
| Storico invii | Archivio delle email inviate con tracciamento di aperture e clic. |
| Email → ticket | Le email ricevute su una casella dedicata diventano ticket, con risposta automatica di presa in carico e allegati importati. |
| Email → attività | In alternativa, l'email viene registrata come attività sulla scheda del contatto. |
| Notifiche push | Avvisi immediati su browser e dispositivi. |
| Telegram | Notifiche e comandi tramite bot. |
| SMS | Invio di codici di verifica e comunicazioni. |

---

## 10. Documenti

| Funzionalità | Descrizione |
|---|---|
| Allegati | File collegati a clienti, ticket, interventi e prodotti, con visibilità controllata. |
| Cartelle documentali | Organizzazione della documentazione per categorie, anche multilingua. |
| Visualizzatore integrato | Apertura di PDF e documenti senza scaricarli. |
| Stampe e report | Documenti aziendali con logo e dati personalizzati. |

---

## 11. Portale cliente

| Funzionalità | Descrizione |
|---|---|
| Accesso dedicato | Area riservata con interfaccia semplificata. |
| Le mie macchine | Consultazione del proprio parco installato e della documentazione. |
| I miei ticket | Apertura, consultazione, chat e allegati sulle proprie richieste. |
| Assistente AI | Supporto di primo livello in autonomia, 24 ore su 24. |
| Feedback | Valutazione del servizio ricevuto. |

---

## 12. Amministrazione e sicurezza

| Funzionalità | Descrizione |
|---|---|
| Utenti e ruoli | Quattro livelli di accesso: amministratore, responsabile, operatore, cliente. |
| Gruppi di lavoro | Squadre e competenze per l'assegnazione automatica dei ticket. |
| Personalizzazioni | Stati, tipologie e categorie configurabili senza intervento tecnico. |
| Impostazioni globali | Parametri aziendali: IVA, orari, tempi di preavviso, loghi, colori. |
| Multilingua | Interfaccia e comunicazioni in più lingue. |
| Tema chiaro e scuro | Interfaccia adattabile alle preferenze dell'utente. |
| Log di sistema | Tracciamento delle operazioni effettuate. |
| Modalità manutenzione | Avviso agli utenti e sospensione programmata del servizio per aggiornamenti. |

---

## 13. Caratteristiche generali

| Aspetto | Descrizione |
|---|---|
| Accesso | Applicazione web: si usa da browser su PC, tablet e smartphone, senza installazione. |
| Utilizzo mobile | Interfaccia responsive pensata per i tecnici in trasferta. |
| Aggiornamenti in tempo reale | Notifiche e modifiche visibili immediatamente a tutti gli operatori. |
| Installazione dedicata | Ogni cliente ha la propria installazione con i propri dati. |
