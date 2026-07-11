using CRM.Shared;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Shared
{
    [DataContract]
    public class ApplicationUser: IdentityUser
    {
        [DataMember]
        [NotMapped]
        [Display(Name = nameof(ApplicationUser.Name), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string Name
        {
            get => Contact?.Name ?? string.Empty;
            set
            {
                Contact ??= new Contact();
                Contact.Name = value;
            }
        }

        [DataMember]
        [NotMapped]
        [Display(Name = nameof(ApplicationUser.Surname), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string Surname
        {
            get => Contact?.Surname ?? string.Empty;
            set
            {
                Contact ??= new Contact();
                Contact.Surname = value;
            }
        }

        [DataMember]
        public int? idCustomer { get; set; }

        [DataMember]
        public DateTime? DateSendInvite { get; set; }

        [DataMember]
        public DateTime? DateAcceptInvite { get; set; }

        [DataMember]
        public bool IsDeleted { get; set; }

        [Required]
        [DataMember]
        public override string Email { get; set; }

        [DataMember]
        [ForeignKey("Company")]
        [Display(Name = nameof(ApplicationUser.IdCompany), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public int? IdCompany { get; set; }

        [DataMember]
        [ForeignKey(nameof(Contact))]
        public int? IdContact { get; set; }

        [Display(Name = nameof(ApplicationUser.Enabled), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public bool Enabled { get; set; }

     
        [Display(Name = nameof(ApplicationUser.AdminConfirmed), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public bool AdminConfirmed { get; set; }

        [Display(Name = nameof(ApplicationUser.CompanyPreview), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string CompanyPreview { get; set; }

        [DataMember]
        [Display(Name = nameof(ApplicationUser.Color), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string Color { get; set; }

        [JsonIgnore]
        [DataMember]
        public string Photo { get; set; }

        [Display(Name = nameof(ApplicationUser.Note), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string Note { get; set; }

        [Display(Name = "Attesa Accettazione Invito")]
        public bool WaitConfirmInvite { get; set; }

        [Display(Name = nameof(ApplicationUser.LanguageCode), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string LanguageCode { get; set; }

        /// <summary>
        /// ✅ NUOVO: Subscription Web Push (serializzata JSON)
        /// </summary>
        public string PushSubscription { get; set; }

        /// <summary>
        /// ✅ NUOVO: Data registrazione push subscription
        /// </summary>
        public DateTime? PushSubscriptionDate { get; set; }

        [DataMember]
        [NotMapped]
        public string AvatarTxt { get; set; }

        [DataMember]
        [NotMapped]
        [Display(Name = nameof(ApplicationUser.NameComplete), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string NameComplete { 
            
            get 
            {
                var contactName = Contact?.NameComplete?.Trim();
                if (!string.IsNullOrWhiteSpace(contactName))
                    return contactName;

                return UserName ?? Email ?? string.Empty;
            } 
        }


        [NotMapped]
        public List<string> Roles { get; set; }

        [NotMapped]
        public bool CanManageOtherCompany { get; set; }
        
        [NotMapped]
        public bool CanTicketAssign { get; set; }

        [NotMapped]
        public bool IsAdmin { get; set; }

        [NotMapped]
        public bool IsSuperUser { get; set; }

        [NotMapped]
        public bool IsClient { get; set; }

        [NotMapped]
        public int CompanyPermits { get; set; }

        [NotMapped]
        public int ProductPermits { get; set; }

        [NotMapped]
        public int ArticlePermits { get; set; }

        [NotMapped]
        public int TicketPermits { get; set; }

        [NotMapped]
        public int AccessoryTypesPermits { get; set; }
        [NotMapped]
        public int TicketInterventionPermits { get; set; }

        public virtual ICollection<Group> Groups { get; set; }

        public virtual ICollection<TicketType> TicketTypes { get; set; }

        [JsonIgnore]
        public virtual ICollection<Ticket> Tickets { get; set; }

        [JsonIgnore]
        public virtual ICollection<Ticket> UserAssignedTickets { get; set; }

        [JsonIgnore]
        public virtual ICollection<Ticket> UserClosedTickets { get; set; }

        [JsonIgnore]
        public virtual ICollection<ProjectUser> Projects { get; set; }

        [DataMember]

        public Company Company { get; set; }

        [DataMember]
        public Contact? Contact { get; set; }

       

    }
}
