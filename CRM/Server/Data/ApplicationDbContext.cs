using CRM.Server.Controllers;
using CRM.Shared;
using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
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
    public class ApplicationDbContext : ApiAuthorizationDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions options,
            IOptions<OperationalStoreOptions> operationalStoreOptions) : base(options, operationalStoreOptions)
        {
           
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApplicationUser>().HasMany(x => x.Tickets).WithOne(x => x.UserOpened).HasForeignKey(y => y.IdUserOpened);
            modelBuilder.Entity<ApplicationUser>().HasMany(x => x.UserClosedTickets).WithOne(x => x.UserClosed).HasForeignKey(y => y.IdUserClosed);
            
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
            
            base.OnModelCreating(modelBuilder);

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
        public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
        public DbSet<CRM.Shared.Logo> Logos => Set<Logo>();
        public DbSet<CRM.Shared.Group> Groups => Set<Group>();
        public DbSet<CRM.Shared.Product> Products => Set<Product>();

        public DbSet<ProductCatalogAsset> ProductCatalogAssets => Set<ProductCatalogAsset>();

        public DbSet<CRM.Shared.ProductParameter> ProductParameters => Set<ProductParameter>();
        public DbSet<CRM.Shared.Article> Articles => Set<Article>();
        public DbSet<CRM.Shared.TicketState> TicketStates => Set<TicketState>();
        public DbSet<CRM.Shared.Ticket> Tickets => Set<Ticket>();
        public DbSet<CRM.Shared.TicketType> TicketTypes => Set<TicketType>();

        public DbSet<Attachment> Attachments => Set<Attachment>();

        public DbSet<AttachmentFile> AttachmentFiles => Set<AttachmentFile>();


        public DbSet<CRM.Shared.TicketIntervention> TicketsInterventions => Set<TicketIntervention>();
        public DbSet<CRM.Shared.TaskProject> TasksProject => Set<TaskProject>();
        public DbSet<CRM.Shared.ProjectModel> ProjectModels => Set<ProjectModel>();
        public DbSet<CRM.Shared.Project> Projects => Set<Project>();
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

        public DbSet<Attachment> ProjectAttachments => Set<Attachment>();

        public DbSet<Contact> Contacts => Set<Contact>();

        public DbSet<Deal> Deals => Set<Deal>();

        public DbSet<AccessoryType> AccessoryTypes => Set<AccessoryType>();

        public DbSet<AccessoryTypeLanguage> AccessoryTypeLanguages => Set<AccessoryTypeLanguage>();

        public DbSet<Accessory> Accessories => Set<Accessory>();

        public DbSet<ProductAccessoryType> ProductAccessoryTypes => Set<ProductAccessoryType>();

        public DbSet<ProductAccessoryTypeLang> ProductTypeAccessoryLanguages=> Set<ProductAccessoryTypeLang>();

        public DbSet<ContractType> ContractTypes => Set<ContractType>();

        public DbSet<ContractTypeTicketType> ContractTypeTicketTypes =>Set<ContractTypeTicketType>();

        public DbSet<CompanyContract> CompanyContracts => Set<CompanyContract>();

        public DbSet<UserAvatar> UserAvatars => Set<UserAvatar>();

        public DbSet<ProjectUser> ProjectUsers => Set<ProjectUser>();

        public DbSet<ArticleBackup> ArticleBackups => Set<ArticleBackup>();

        public DbSet<BackUpParameter> BackUpParameters => Set<BackUpParameter>();

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

        public DbSet<Folder> Folders => Set<Folder>();

        // ✅ NUOVO: DbSet per le note spese
        public DbSet<ExpenseReceipt> ExpenseReceipts => Set<ExpenseReceipt>();


        public DbSet<FolderLanguage> FolderLanguages => Set<FolderLanguage>();  



    }
}
