using CRM.Shared;
using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Org.BouncyCastle.Math.EC.Rfc7748;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Duende.IdentityServer.EntityFramework.Options;
using System.Reflection.Metadata;
using System.Text.Json.Serialization;
using CRM.Server.Controllers;

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

            base.OnModelCreating(modelBuilder);
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
        public DbSet<SmtpSettings> SmtpSettings => Set<SmtpSettings>();

        public DbSet<TelegramAppConfig> TelegramAppConfigs => Set<TelegramAppConfig>();
        
        public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();
        public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
        public DbSet<CRM.Shared.Logo> Logos => Set<Logo>();
        public DbSet<CRM.Shared.Group> Groups => Set<Group>();
        public DbSet<CRM.Shared.Product> Products => Set<Product>();

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

        public DbSet<Talk> Talks => Set<Talk>();

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
    }
}
