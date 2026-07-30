using CRM.Server.Controllers;
using CRM.Shared;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Migrations;
using Mono.TextTemplating;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Server.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
           
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApplicationUser>().HasMany(x => x.Tickets).WithOne(x => x.UserOpened).HasForeignKey(y => y.IdUserOpened);
            modelBuilder.Entity<ApplicationUser>().HasMany(x => x.UserClosedTickets).WithOne(x => x.UserClosed).HasForeignKey(y => y.IdUserClosed);
            modelBuilder.Entity<Contact>().HasMany(x => x.ApplicationUsers).WithOne(x => x.Contact).HasForeignKey(x => x.IdContact).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ApplicationUser>().Navigation(x => x.Contact).AutoInclude();

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.OperationalSummaryUpdatedByUser)
                .WithMany()
                .HasForeignKey(t => t.OperationalSummaryUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Deal)
                .WithMany(d => d.Tickets)
                .HasForeignKey(t => t.IdDeal)
                .OnDelete(DeleteBehavior.NoAction);

            // ─── Produzione: Commesse ────────────────────────────────────────────
            // Ticket -> Fase di commessa (opzionale).
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.CommessaFase)
                .WithMany(f => f.Tickets)
                .HasForeignKey(t => t.IdCommessaFase)
                .OnDelete(DeleteBehavior.NoAction);

            // Codice commessa: chiave naturale univoca. Il filtro serve perché in un indice univoco
            // SQL Server considera due NULL uguali, e il codice è nullable (commesse senza codice).
            modelBuilder.Entity<Commessa>()
                .HasIndex(c => c.Code)
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL");

            // Commessa -> riga d'ordine (una commessa per unità). Scollegare la riga non la cancella.
            modelBuilder.Entity<Commessa>()
                .HasOne(c => c.OrderRow)
                .WithMany(r => r.Commesse)
                .HasForeignKey(c => c.IdOrderRow)
                .OnDelete(DeleteBehavior.SetNull);

            // Gerarchia WBS delle fasi (self-reference): niente cascade multiple.
            modelBuilder.Entity<CommessaFase>()
                .HasOne(f => f.Parent)
                .WithMany(f => f.Children)
                .HasForeignKey(f => f.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommessaFase>()
                .HasOne(f => f.Commessa)
                .WithMany(c => c.Phases)
                .HasForeignKey(f => f.IdCommessa)
                .OnDelete(DeleteBehavior.Cascade);

            // Dipendenze tra fasi: due FK verso CommessaFase -> Restrict per evitare cicli di cascade.
            modelBuilder.Entity<CommessaFaseDependency>()
                .HasOne(d => d.Fase)
                .WithMany(f => f.Dependencies)
                .HasForeignKey(d => d.IdFase)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommessaFaseDependency>()
                .HasOne(d => d.PredecessorFase)
                .WithMany()
                .HasForeignKey(d => d.IdPredecessorFase)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommessaFaseTicketPlan>()
                .HasOne(p => p.CommessaFase)
                .WithMany(f => f.TicketPlans)
                .HasForeignKey(p => p.IdCommessaFase)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CommessaFaseTicketPlan>()
                .HasOne(p => p.Ticket)
                .WithMany()
                .HasForeignKey(p => p.IdTicket)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CommessaFaseTicketPlan>()
                .HasOne(p => p.SourceTemplate)
                .WithMany()
                .HasForeignKey(p => p.IdGanttPhaseTicketTemplate)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<CommessaFaseTicketPlan>()
                .HasOne(p => p.TicketType)
                .WithMany()
                .HasForeignKey(p => p.IdTicketType)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<CommessaFaseTicketPlan>()
                .HasOne(p => p.GroupAssigned)
                .WithMany()
                .HasForeignKey(p => p.IdGroupAssigned)
                .OnDelete(DeleteBehavior.NoAction);

            // ─── Template: GanttPlan -> GanttPhase ───────────────────────────────
            modelBuilder.Entity<GanttPhase>()
                .HasOne(p => p.GanttPlan)
                .WithMany(g => g.Phases)
                .HasForeignKey(p => p.IdGanttPlan)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GanttPhase>()
                .HasOne(p => p.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GanttPhaseDependency>()
                .HasOne(d => d.Phase)
                .WithMany(p => p.Dependencies)
                .HasForeignKey(d => d.IdPhase)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GanttPhaseDependency>()
                .HasOne(d => d.PredecessorPhase)
                .WithMany()
                .HasForeignKey(d => d.IdPredecessorPhase)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GanttPhaseTicketTemplate>()
                .HasOne(t => t.GanttPhase)
                .WithMany(p => p.TicketTemplates)
                .HasForeignKey(t => t.IdGanttPhase)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GanttPhaseTicketTemplate>()
                .HasOne(t => t.TicketType)
                .WithMany()
                .HasForeignKey(t => t.IdTicketType)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<GanttPhaseTicketTemplate>()
                .HasOne(t => t.GroupAssigned)
                .WithMany()
                .HasForeignKey(t => t.IdGroupAssigned)
                .OnDelete(DeleteBehavior.NoAction);


            // ⚠️ LEGACY: Relazione 1-to-many tradizionale (mantenuta per compatibilità)
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(x => x.UserAssignedTickets)
                .WithOne(x => x.UserAssigned)
                .HasForeignKey(y => y.IdUserAssigned);

            // ✅ NUOVA: Relazione many-to-many tramite TicketUserAssignment
            modelBuilder.Entity<TicketUserAssignment>()
                .HasOne(tua => tua.Ticket)
                .WithMany(t => t.AssignedUsers)
                .HasForeignKey(tua => tua.IdTicket)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TicketUserAssignment>()
                .HasOne(tua => tua.User)
                .WithMany()
                .HasForeignKey(tua => tua.IdUser)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketIntervention>()
            .HasOne(ti => ti.Ticket)
            .WithMany(t => t.TicketInterventions)
            .HasForeignKey(ti => ti.IdTicket)
            .OnDelete(DeleteBehavior.Restrict);  // or Cascade, but be explicit

            // ✅ Relazione many-to-many per TicketInterventionUser
            modelBuilder.Entity<TicketInterventionUser>()
                .HasOne(tiu => tiu.TicketIntervention)
                .WithMany(ti => ti.AssignedUsers)
                .HasForeignKey(tiu => tiu.IdIntervention)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TicketInterventionUser>()
                .HasOne(tiu => tiu.User)
                .WithMany()
                .HasForeignKey(tiu => tiu.IdUser)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketChat>()
                .HasOne(tc => tc.AttachmentFile)
                .WithMany()
                .HasForeignKey(tc => tc.IdAttachmentFile)
                .OnDelete(DeleteBehavior.SetNull);

            // ✅ NUOVO: Configurazione TicketFeedback
            modelBuilder.Entity<TicketFeedback>()
                .HasOne(tf => tf.Ticket)
                .WithMany()
                .HasForeignKey(tf => tf.IdTicket)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TicketFeedback>()
                .HasOne(tf => tf.User)
                .WithMany()
                .HasForeignKey(tf => tf.IdUser)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ CONFIGURAZIONE STATI ARTICOLO - Previene cicli di cascade
            modelBuilder.Entity<ArticleDomainState>()
                .HasOne(ads => ads.Domain)
                .WithMany()
                .HasForeignKey(ads => ads.DomainId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArticleDomainState>()
                .HasOne(ads => ads.CurrentState)
                .WithMany()
                .HasForeignKey(ads => ads.CurrentStateId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArticleDomainState>()
                .HasOne(ads => ads.LastEvent)
                .WithMany()
                .HasForeignKey(ads => ads.LastEventId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ArticleState>()
                .HasOne(s => s.Domain)
                .WithMany()
                .HasForeignKey(s => s.DomainId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArticleEvent>()
                .HasOne(e => e.Domain)
                .WithMany()
                .HasForeignKey(e => e.DomainId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArticleEvent>()
                .HasOne(e => e.FromState)
                .WithMany()
                .HasForeignKey(e => e.FromStateId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ArticleEvent>()
                .HasOne(e => e.ToState)
                .WithMany()
                .HasForeignKey(e => e.ToStateId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ArticleEvent>()
                .HasOne(e => e.EventType)
                .WithMany()
                .HasForeignKey(e => e.EventTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArticleEvent>()
                .HasOne(e => e.Article)
                .WithMany()
                .HasForeignKey(e => e.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurazioni esistenti
            modelBuilder.Entity<ArticleStateTransition>()
                .HasOne(t => t.EventType)
                .WithMany()
                .HasForeignKey(t => t.EventTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ArticleStateTransition>()
                .HasOne(t => t.Domain)
                .WithMany()
                .HasForeignKey(t => t.DomainId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ArticleEventType>()
                .HasOne(et => et.Domain)
                .WithMany()
                .HasForeignKey(et => et.DomainId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArticleStateTransition>()
                .HasOne(t => t.FromState)
                .WithMany()
                .HasForeignKey(t => t.FromStateId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArticleStateTransition>()
                .HasOne(t => t.ToState)
                .WithMany()
                .HasForeignKey(t => t.ToStateId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ NUOVO: Configurazione ExpenseReceipt
            modelBuilder.Entity<ExpenseReceipt>()
                .HasOne(er => er.TicketIntervention)
                .WithMany(ti => ti.ExpenseReceipts)
                .HasForeignKey(er => er.TicketInterventionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExpenseReceipt>()
                .HasOne(er => er.AttachmentFile)
                .WithMany()
                .HasForeignKey(er => er.AttachmentFileId)
                .OnDelete(DeleteBehavior.SetNull);

            // ✅ FIX: Previene percorsi multipli di cascade verso ExpenseReceipt
            // Configurazione Attachment → AttachmentFile (Restrict invece di Cascade)
            modelBuilder.Entity<AttachmentFile>()
                .HasOne(af => af.Attachment)
                .WithMany(a => a.Files)
                .HasForeignKey(af => af.IdAttachment)
                .OnDelete(DeleteBehavior.Restrict);

            // Configurazione ApplicationUser → Attachment (Restrict invece di Cascade)
            modelBuilder.Entity<Attachment>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.IdUser)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductCatalogAsset>()
                .HasOne(a => a.Product)
                .WithMany()
                .HasForeignKey(a => a.IdProduct)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductCatalogAsset>()
                .HasOne(a => a.AttachmentFile)
                .WithMany()
                .HasForeignKey(a => a.IdAttachmentFile)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MachineParameterApiKey>()
                .HasIndex(x => x.KeyHash)
                .IsUnique();

            modelBuilder.Entity<ExternalTicketApiKey>(entity =>
            {
                entity.HasOne(x => x.Company)
                      .WithMany()
                      .HasForeignKey(x => x.IdCompany)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => x.KeyHash).IsUnique();
                entity.HasIndex(x => x.IdCompany);
            });

            modelBuilder.Entity<MachineBackup>(entity =>
            {
                entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.IdProduct).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Article).WithMany().HasForeignKey(x => x.IdArticle).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(x => new { x.OwnerType, x.IdProduct, x.Version }).IsUnique();
                entity.HasIndex(x => new { x.OwnerType, x.IdArticle, x.Version }).IsUnique();
                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_MachineBackups_Owner",
                    "([OwnerType] = 1 AND [IdProduct] IS NOT NULL AND [IdArticle] IS NULL) OR ([OwnerType] = 2 AND [IdArticle] IS NOT NULL AND [IdProduct] IS NULL)"));
            });
            
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseOpenIddict();

            // C#
            modelBuilder.Entity<ArticleDomainState>()
                .HasOne(ad => ad.CurrentState)
                .WithMany()
                .HasForeignKey(ad => ad.CurrentStateId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ArticleDomainState>()
                .HasOne(ad => ad.Domain)
                .WithMany()
                .HasForeignKey(ad => ad.DomainId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArticleState>(entity =>
            {
                entity.Property(e => e.IsActive)
                      .HasDefaultValue(1);
            });

            // Articolo -> Company destinataria: seconda FK verso Company (oltre a IdCompany, l'acquirente).
            // Configurata esplicitamente perche' con due relazioni verso la stessa entita' la convenzione
            // non riesce ad accoppiare le navigation; Restrict evita "multiple cascade paths" su SQL Server.
            modelBuilder.Entity<Article>()
                .HasOne(a => a.RecipientCompany)
                .WithMany()
                .HasForeignKey(a => a.IdRecipientCompany)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArticleLicense>()
                .HasOne(l => l.Article)
                .WithMany()
                .HasForeignKey(l => l.IdArticle)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ArticleLicense>()
                .HasIndex(l => l.IdArticle)
                .IsUnique();

            modelBuilder.Entity<ArticleLicense>()
                .HasIndex(l => l.MachineKey);

            modelBuilder.Entity<ArticleLicenseFeature>()
                .HasOne(f => f.License)
                .WithMany(l => l.Features)
                .HasForeignKey(f => f.IdLicense)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ArticleLicenseFeature>()
                .HasOne(f => f.FeatureDef)
                .WithMany()
                .HasForeignKey(f => f.IdFeatureDef)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArticleLicenseFeature>()
                .HasIndex(f => new { f.IdLicense, f.IdFeatureDef })
                .IsUnique();

            modelBuilder.Entity<ArticleLicenseFeatureDef>()
                .HasOne(d => d.ProductType)
                .WithMany()
                .HasForeignKey(d => d.IdProductType)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArticleLicenseFeatureDef>()
                .HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.IdProduct)
                .OnDelete(DeleteBehavior.Restrict);

            // Chiave univoca: stessa feature Key non può essere definita due volte per la stessa coppia (ProductType, Product)
            modelBuilder.Entity<ArticleLicenseFeatureDef>()
                .HasIndex(d => new { d.Key, d.IdProductType, d.IdProduct })
                .IsUnique();

            // I tempi degli interventi si leggono sempre nello stesso modo: per intervento e
            // filtrando i fatturabili, poi si sommano le durate per tipo (vedi TicketBillableMinutes).
            // L'indice coprente serve quel piano senza key lookup: sostituisce quello convenzionale
            // sulla sola FK, che ne e' il prefisso e diventerebbe un doppione da mantenere in scrittura.
            modelBuilder.Entity<TicketInterventionTime>()
                .HasIndex(t => new { t.IdTicketIntervention, t.IsBillable })
                .IncludeProperties(t => new { t.TimeType, t.StartDateTime, t.EndDateTime });

            // ---- Preventivi / Offerte ----
            modelBuilder.Entity<Quote>(entity =>
            {
                entity.HasOne(q => q.Company)
                      .WithMany()
                      .HasForeignKey(q => q.IdCompany)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(q => q.Contact)
                      .WithMany()
                      .HasForeignKey(q => q.IdContact)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(q => q.Deal)
                      .WithMany()
                      .HasForeignKey(q => q.IdDeal)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(q => q.User)
                      .WithMany()
                      .HasForeignKey(q => q.IdUser)
                      .OnDelete(DeleteBehavior.Restrict);

                // Catena delle revisioni: la radice non si cancella finché esistono revisioni.
                entity.HasOne(q => q.RootQuote)
                      .WithMany()
                      .HasForeignKey(q => q.IdRootQuote)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(q => q.Number);

                // Le liste filtrano sempre sulla revisione corrente.
                entity.HasIndex(q => q.IsCurrent);
                entity.HasIndex(q => q.IdRootQuote);
            });

            modelBuilder.Entity<QuoteRow>(entity =>
            {
                entity.HasOne(r => r.Quote)
                      .WithMany(q => q.Rows)
                      .HasForeignKey(r => r.IdQuote)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Product)
                      .WithMany()
                      .HasForeignKey(r => r.IdProduct)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Article)
                      .WithMany()
                      .HasForeignKey(r => r.IdArticle)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(r => r.Quantity).HasPrecision(18, 3);
                entity.Property(r => r.DiscountPct).HasPrecision(5, 2);
                entity.Property(r => r.VatRate).HasPrecision(5, 2);
            });

            modelBuilder.Entity<GlobalSetting>()
                .Property(g => g.DefaultVatRate)
                .HasPrecision(5, 2);

            // ---- Ordini ----
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasOne(o => o.Company)
                      .WithMany()
                      .HasForeignKey(o => o.IdCompany)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(o => o.Contact)
                      .WithMany()
                      .HasForeignKey(o => o.IdContact)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(o => o.Quote)
                      .WithMany()
                      .HasForeignKey(o => o.IdQuote)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(o => o.Deal)
                      .WithMany()
                      .HasForeignKey(o => o.IdDeal)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(o => o.User)
                      .WithMany()
                      .HasForeignKey(o => o.IdUser)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(o => o.Number);
            });

            modelBuilder.Entity<OrderRow>(entity =>
            {
                entity.HasOne(r => r.Order)
                      .WithMany(o => o.Rows)
                      .HasForeignKey(r => r.IdOrder)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Product)
                      .WithMany()
                      .HasForeignKey(r => r.IdProduct)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Article)
                      .WithMany()
                      .HasForeignKey(r => r.IdArticle)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(r => r.Quantity).HasPrecision(18, 3);
                entity.Property(r => r.DiscountPct).HasPrecision(5, 2);
                entity.Property(r => r.VatRate).HasPrecision(5, 2);
            });

            // ---- Listini prezzi (prezzo prodotto per cliente) ----
            modelBuilder.Entity<PriceListItem>(entity =>
            {
                entity.HasOne(p => p.Company)
                      .WithMany()
                      .HasForeignKey(p => p.IdCompany)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.Product)
                      .WithMany()
                      .HasForeignKey(p => p.IdProduct)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(p => p.DiscountPct).HasPrecision(5, 2);
                entity.HasIndex(p => new { p.IdCompany, p.IdProduct }).IsUnique();
            });

            // ---- Fatture ----
            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasOne(i => i.Company)
                      .WithMany()
                      .HasForeignKey(i => i.IdCompany)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Contact)
                      .WithMany()
                      .HasForeignKey(i => i.IdContact)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Order)
                      .WithMany()
                      .HasForeignKey(i => i.IdOrder)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(i => i.User)
                      .WithMany()
                      .HasForeignKey(i => i.IdUser)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(i => i.Number);
            });

            modelBuilder.Entity<InvoiceRow>(entity =>
            {
                entity.HasOne(r => r.Invoice)
                      .WithMany(i => i.Rows)
                      .HasForeignKey(r => r.IdInvoice)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Product)
                      .WithMany()
                      .HasForeignKey(r => r.IdProduct)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(r => r.Quantity).HasPrecision(18, 3);
                entity.Property(r => r.DiscountPct).HasPrecision(5, 2);
                entity.Property(r => r.VatRate).HasPrecision(5, 2);
            });

            // ---- Attivita' (timeline + follow-up) ----
            modelBuilder.Entity<Activity>(entity =>
            {
                entity.HasOne(a => a.User)
                      .WithMany()
                      .HasForeignKey(a => a.IdUser)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Assignee)
                      .WithMany()
                      .HasForeignKey(a => a.IdAssignee)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.CompletedBy)
                      .WithMany()
                      .HasForeignKey(a => a.IdCompletedBy)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(a => a.Participants)
                      .WithOne(p => p.Activity)
                      .HasForeignKey(p => p.IdActivity)
                      .OnDelete(DeleteBehavior.Cascade);

                // Email collegata (opzionale): se l'email viene rimossa l'attività resta, con il link azzerato.
                entity.HasOne(a => a.EmailSent)
                      .WithMany()
                      .HasForeignKey(a => a.IdEmailSent)
                      .OnDelete(DeleteBehavior.SetNull);

                // EntityId e' un riferimento polimorfico (nessuna FK): indicizzato per la timeline
                entity.HasIndex(a => new { a.EntityType, a.EntityId });
                entity.HasIndex(a => new { a.ReminderStatus, a.ReminderAt });
                entity.HasIndex(a => a.IdAssignee);
            });

            modelBuilder.Entity<ActivityParticipant>(entity =>
            {
                entity.HasOne(p => p.User)
                      .WithMany()
                      .HasForeignKey(p => p.IdUser)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(p => p.IdActivity);
                entity.HasIndex(p => p.IdUser);
            });

            // ---- Coda di invio email (outbox) ----
            modelBuilder.Entity<EmailOutbox>(entity =>
            {
                // Indice a supporto del polling del worker: prima i Pending/Failed piu' vecchi.
                entity.HasIndex(e => new { e.Status, e.CreatedAt });
            });

            // Un template per (tipo, lingua): consente versioni linguistiche distinte dello stesso tipo.
            modelBuilder.Entity<EmailTemplate>()
                .HasIndex(x => new { x.Tipo, x.Language })
                .IsUnique();

            // ---- Engagement email (Tier 3) ----
            modelBuilder.Entity<EmailSent>(entity =>
            {
                // Correlazione evento->email: i webhook cercano per MessageRef.
                entity.HasIndex(e => e.MessageRef);
            });

            modelBuilder.Entity<EmailEvent>(entity =>
            {
                entity.HasOne(e => e.EmailSent)
                      .WithMany()
                      .HasForeignKey(e => e.IdEmailSent)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.IdEmailSent);
            });

            // ---- Posta in ingresso (Tier 4) ----
            modelBuilder.Entity<InboundEmail>(entity =>
            {
                entity.HasOne(e => e.Inbox)
                      .WithMany()
                      .HasForeignKey(e => e.IdInbox)
                      .OnDelete(DeleteBehavior.Cascade);

                // Deduplica: stessa casella + stesso Message-Id/UID non riprocessati.
                entity.HasIndex(e => new { e.IdInbox, e.MessageId });
                entity.HasIndex(e => new { e.IdInbox, e.Uid });
            });

            modelBuilder.Entity<InboundEmailAttachment>(entity =>
            {
                entity.HasOne(a => a.InboundEmail)
                      .WithMany()
                      .HasForeignKey(a => a.IdInboundEmail)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(a => a.IdInboundEmail);
            });

            // ---- Lead management + workflow automation ----
            modelBuilder.Entity<Lead>(entity =>
            {
                entity.HasOne(l => l.Company)
                      .WithMany()
                      .HasForeignKey(l => l.IdCompany)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(l => l.Contact)
                      .WithMany()
                      .HasForeignKey(l => l.IdContact)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(l => l.User)
                      .WithMany()
                      .HasForeignKey(l => l.IdUser)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(l => l.ConvertedDeal)
                      .WithMany()
                      .HasForeignKey(l => l.ConvertedDealId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(l => l.Status);
                entity.HasIndex(l => l.Source);
                entity.HasIndex(l => l.IdUser);
                entity.HasIndex(l => l.CreatedAt);
            });

            modelBuilder.Entity<LeadProductInterest>(entity =>
            {
                entity.HasOne(x => x.Lead)
                      .WithMany(x => x.ProductInterests)
                      .HasForeignKey(x => x.IdLead)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Product)
                      .WithMany()
                      .HasForeignKey(x => x.IdProduct)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.Property(x => x.Quantity).HasPrecision(18, 3);
                entity.Property(x => x.DiscountPct).HasPrecision(5, 2);
                entity.HasIndex(x => x.IdLead);
                entity.HasIndex(x => x.IdProduct);
            });

            modelBuilder.Entity<Deal>(entity =>
            {
                entity.Property(d => d.Probability).HasDefaultValue(0);
                entity.HasIndex(d => d.ExpectedCloseDate);
                entity.HasIndex(d => d.IdUser);
            });

            modelBuilder.Entity<DealProductInterest>(entity =>
            {
                entity.HasOne(x => x.Deal)
                      .WithMany(x => x.ProductInterests)
                      .HasForeignKey(x => x.IdDeal)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Product)
                      .WithMany()
                      .HasForeignKey(x => x.IdProduct)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.Property(x => x.Quantity).HasPrecision(18, 3);
                entity.Property(x => x.DiscountPct).HasPrecision(5, 2);
                entity.HasIndex(x => x.IdDeal);
                entity.HasIndex(x => x.IdProduct);
            });

            modelBuilder.Entity<WorkflowAutomation>(entity =>
            {
                entity.Property(w => w.MinAmount).HasColumnType("Money");
                entity.Property(w => w.IdAssignee).HasMaxLength(450);
                entity.HasIndex(w => new { w.IsActive, w.Trigger });
                entity.HasIndex(w => w.IdAssignee);
            });

            modelBuilder.Entity<WorkflowAutomationExecution>(entity =>
            {
                entity.HasOne(x => x.WorkflowAutomation)
                      .WithMany()
                      .HasForeignKey(x => x.IdWorkflowAutomation)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Activity)
                      .WithMany()
                      .HasForeignKey(x => x.IdActivity)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(x => x.Error).HasMaxLength(1000);
                entity.HasIndex(x => new { x.IdWorkflowAutomation, x.Trigger, x.EntityType, x.EntityId }).IsUnique();
                entity.HasIndex(x => x.ExecutedAt);
            });

            modelBuilder.Entity<Product>()
                .HasOne(p => p.GanttPlan)
                .WithMany(g => g.Products)
                .HasForeignKey(p => p.IdGanttPlan)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.DefaultCommessaResponsible)
                .WithMany()
                .HasForeignKey(p => p.IdDefaultCommessaResponsible)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.BlockedByUser)
                .WithMany()
                .HasForeignKey(t => t.IdBlockedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.BlockResolvedByUser)
                .WithMany()
                .HasForeignKey(t => t.IdBlockResolvedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Ticket>()
                .HasIndex(t => t.IsBlocked);

            // Smistamento AI: il gruppo suggerito e' un secondo riferimento a Groups, indipendente
            // da quello assegnato. Nessuna cascata: cancellare un gruppo non deve toccare i ticket.
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.AiSuggestedGroup)
                .WithMany()
                .HasForeignKey(t => t.AiSuggestedGroupId)
                .OnDelete(DeleteBehavior.NoAction);

            // Riga unica di configurazione: la chiave e' assegnata dal codice (Id = 1), non dal database.
            modelBuilder.Entity<TicketRoutingSetting>()
                .Property(s => s.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<TicketRoutingSetting>()
                .HasOne(s => s.FallbackGroup)
                .WithMany()
                .HasForeignKey(s => s.IdFallbackGroup)
                .OnDelete(DeleteBehavior.NoAction);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    modelBuilder.Entity<Customer>().ToTable("Customers");
        //    modelBuilder.Entity<Company>().ToTable("Companies");
        //}

        public DbSet<Company> Companies => Set<Company>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<SmtpSetting> SmtpSettings => Set<SmtpSetting>();

        public DbSet<TelegramAppConfig> TelegramAppConfigs => Set<TelegramAppConfig>();
        
        public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();
        public DbSet<TicketRoutingSetting> TicketRoutingSettings => Set<TicketRoutingSetting>();
        public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
        public DbSet<CRM.Shared.Logo> Logos => Set<Logo>();
        public DbSet<CRM.Shared.Group> Groups => Set<Group>();
        public DbSet<CRM.Shared.Product> Products => Set<Product>();
        public DbSet<CRM.Shared.GanttPlan> GanttPlans => Set<GanttPlan>();

        public DbSet<CRM.Shared.ProductKnowledge> ProductKnowledge => Set<ProductKnowledge>();

        public DbSet<ProductCatalogAsset> ProductCatalogAssets => Set<ProductCatalogAsset>();

        public DbSet<MachineBackup> MachineBackups => Set<MachineBackup>();
        public DbSet<MachineParameterApiKey> MachineParameterApiKeys => Set<MachineParameterApiKey>();
        public DbSet<ExternalTicketApiKey> ExternalTicketApiKeys => Set<ExternalTicketApiKey>();
        public DbSet<CRM.Shared.Article> Articles => Set<Article>();
        public DbSet<CRM.Shared.TicketState> TicketStates => Set<TicketState>();
        public DbSet<CRM.Shared.Ticket> Tickets => Set<Ticket>();
        public DbSet<CRM.Shared.TicketType> TicketTypes => Set<TicketType>();

        public DbSet<Attachment> Attachments => Set<Attachment>();

        public DbSet<AttachmentFile> AttachmentFiles => Set<AttachmentFile>();


        public DbSet<CRM.Shared.TicketIntervention> TicketsInterventions => Set<TicketIntervention>();
        public DbSet<CRM.Shared.Commessa> Commesse => Set<Commessa>();
        public DbSet<CRM.Shared.CommessaFase> CommessaFasi => Set<CommessaFase>();
        public DbSet<CRM.Shared.CommessaFaseDependency> CommessaFaseDependencies => Set<CommessaFaseDependency>();
        public DbSet<CRM.Shared.CommessaFaseTicketPlan> CommessaFaseTicketPlans => Set<CommessaFaseTicketPlan>();
        public DbSet<CRM.Shared.GanttPhase> GanttPhases => Set<GanttPhase>();
        public DbSet<CRM.Shared.GanttPhaseDependency> GanttPhaseDependencies => Set<GanttPhaseDependency>();
        public DbSet<CRM.Shared.GanttPhaseTicketTemplate> GanttPhaseTicketTemplates => Set<GanttPhaseTicketTemplate>();
        public DbSet<CRM.Shared.TicketInterventionUser> TicketInterventionUser => Set<TicketInterventionUser>();

        public DbSet<InterventionType> InterventionTypes => Set<InterventionType>();

        public DbSet<Language> Languages => Set<Language>();

        public DbSet<LogEvent> LogEvents => Set<LogEvent>();

        public DbSet<CSVMapping> CSVMappings => Set<CSVMapping>();
        public DbSet<ProductType> ProductTypes => Set<ProductType>();

        public DbSet<TicketChat> TicketChats => Set<TicketChat>();

       
        public DbSet<TicketChatRead> TicketChatReads => Set<TicketChatRead>();

        public DbSet<InterventionTypeLanguage> InterventionTypeLanguages => Set<InterventionTypeLanguage>();

        public DbSet<TicketTypeLanguage> TicketTypesLanguages => Set<TicketTypeLanguage>();

        public DbSet<TicketInterventionArticle> TicketInterventionArticles => Set<TicketInterventionArticle>();

        public DbSet<Translate> Translates => Set<Translate>();

        public DbSet<EmailSent> EmailsSent => Set<EmailSent>();

        public DbSet<EmailOutbox> EmailsOutbox => Set<EmailOutbox>();

        public DbSet<EmailEvent> EmailEvents => Set<EmailEvent>();

        public DbSet<EmailInbox> EmailInboxes => Set<EmailInbox>();

        public DbSet<InboundEmail> InboundEmails => Set<InboundEmail>();

        public DbSet<InboundEmailAttachment> InboundEmailAttachments => Set<InboundEmailAttachment>();

        public DbSet<Attachment> ProjectAttachments => Set<Attachment>();

        public DbSet<Contact> Contacts => Set<Contact>();

        public DbSet<Deal> Deals => Set<Deal>();

        public DbSet<Lead> Leads => Set<Lead>();

        public DbSet<LeadProductInterest> LeadProductInterests => Set<LeadProductInterest>();

        public DbSet<DealProductInterest> DealProductInterests => Set<DealProductInterest>();

        public DbSet<WorkflowAutomation> WorkflowAutomations => Set<WorkflowAutomation>();

        public DbSet<WorkflowAutomationExecution> WorkflowAutomationExecutions => Set<WorkflowAutomationExecution>();

        public DbSet<Quote> Quotes => Set<Quote>();

        public DbSet<QuoteRow> QuoteRows => Set<QuoteRow>();

        public DbSet<QuoteDelivery> QuoteDeliveries => Set<QuoteDelivery>();

        public DbSet<Order> Orders => Set<Order>();

        public DbSet<OrderRow> OrderRows => Set<OrderRow>();

        public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();

        public DbSet<Invoice> Invoices => Set<Invoice>();

        public DbSet<InvoiceRow> InvoiceRows => Set<InvoiceRow>();

        public DbSet<Activity> Activities => Set<Activity>();

        public DbSet<ActivityParticipant> ActivityParticipants => Set<ActivityParticipant>();

        public DbSet<AccessoryType> AccessoryTypes => Set<AccessoryType>();

        public DbSet<AccessoryTypeLanguage> AccessoryTypeLanguages => Set<AccessoryTypeLanguage>();

        public DbSet<Accessory> Accessories => Set<Accessory>();

        public DbSet<ProductAccessoryType> ProductAccessoryTypes => Set<ProductAccessoryType>();

        public DbSet<ProductAccessoryTypeLang> ProductTypeAccessoryLanguages=> Set<ProductAccessoryTypeLang>();

        public DbSet<ContractType> ContractTypes => Set<ContractType>();

        public DbSet<ContractTypeTicketType> ContractTypeTicketTypes =>Set<ContractTypeTicketType>();

        public DbSet<CompanyContract> CompanyContracts => Set<CompanyContract>();

        public DbSet<UserAvatar> UserAvatars => Set<UserAvatar>();

        public DbSet<TicketInterventionTime> TicketInterventionTimes => Set<TicketInterventionTime>();

        public DbSet<CRM.Shared.ArticleAccessory> ArticleAccessory => Set<ArticleAccessory>();

        // ✅ NUOVO: DbSet per la tabella di assegnazione multipla utenti ai ticket
        public DbSet<TicketUserAssignment> TicketUserAssignments => Set<TicketUserAssignment>();

        public DbSet<ArticleDomain> ArticleDomains => Set<ArticleDomain>();
        public DbSet<ArticleState> ArticleStates => Set<ArticleState>();

        public DbSet<ArticleEventType> ArticleEventTypes => Set<ArticleEventType>();

        public DbSet<ArticleStateTransition> ArticleStateTransitions => Set<ArticleStateTransition>();

        public DbSet<ArticleDomainState> ArticleDomainStates => Set<ArticleDomainState>();

        public DbSet<ArticleEvent> ArticleEvents => Set<ArticleEvent>();

        // ✅ NUOVO: DbSet per i feedback dei ticket
        public DbSet<TicketFeedback> TicketFeedbacks => Set<TicketFeedback>();

        // Log Q&A dell'assistente AI + feedback operatore
        public DbSet<AssistantChatLog> AssistantChatLogs => Set<AssistantChatLog>();

        public DbSet<Folder> Folders => Set<Folder>();

        public DbSet<ExpenseReceipt> ExpenseReceipts => Set<ExpenseReceipt>();

        public DbSet<FolderLanguage> FolderLanguages => Set<FolderLanguage>();

        public DbSet<ArticleLicenseFeatureDef> ArticleLicenseFeatureDefs => Set<ArticleLicenseFeatureDef>();
        public DbSet<ArticleLicense> ArticleLicenses => Set<ArticleLicense>();
        public DbSet<ArticleLicenseFeature> ArticleLicenseFeatures => Set<ArticleLicenseFeature>();
    }
}
