using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Helpers
{
    public static class ConstHelper
    {
        public enum PageState
        {
            Loading,
            Error,
            Loaded
        }

        public static string AboutPath = "api/About";

        public static string AccessoriesPath = "api/Accessories";

        public static string AccessoryTypesPath = "api/AccessoryTypes";

        public static string AccessoryTypeLanguagesPath = "api/AccessoryTypeLanguages";

        public static string CompaniesPath = "api/Companies";

        public static string CustomersPath = "api/Customers";

        public static string UsersPath = "api/Users";

        public static string LogosPath = "api/Logos";

        public static string EmailTemplatePath = "api/EmailTemplates";

        public static string SmtpSettingsPath = "api/SmtpSettings";

        public static string GlobalSettingsPath = "api/GlobalSettings";

        public static string GroupsPath = "api/Groups";

        public static string GroupUsers = "api/GroupUsers";

        public static string Products = "api/Products";

        public static string ProductTypesPath = "api/ProductsTypes";

        public static string ArticlesPath = "api/Articles";

        public static string ArticleBackupsPath = "api/ArticleBackups";

        public static string InterventionTime = "api/TicketInterventionTimes";

        public static string ProductParentChild = "api/ProductParenChild";

        public static string ProductAccTypesPath = "api/ProductAccessoryTypes";

        public static string ProductAccTypeLangsPath = "api/ProductAccessoryTypeLangs";

        public static string ProductParameters = "api/ProductParameters";

        public static string TicketStatesPath = "api/TicketStates";

        public static string TicketPath = "api/Tickets";

        public static string TicketTypesPath = "api/TicketTypes";

        public static string TicketTypesLanguagesPath = "api/TicketTypesLanguages";

        public static string TicketTypesUsersPath = "api/TicketTypeUsers";

        public static string TicketTypesGroupsPath = "api/TicketTypeGroups";

        public static string TicketsDashboardPath = "api/TicketsDashboard";

        public static string TicketsInterventionsPath = "api/TicketInterventions";

        public static string TicketInterventionUsersPath = "api/TicketInterventionUsers";

        public static string TicketInterventionsTypesPath = "api/TicketInterventionsTypes";

        public static string ContractTypeTicketTypesPath = "api/ContractTypeTicketTypes";

        public static string DealsPath = "api/Deals";
       

        public static string RolesPath = "api/Roles";

        public static string UserRolesPath = "api/UserRoles";

        public static string UserSignedPath = "api/UserSigned";

        public static string AttachmentsPath = "api/Attachments";

        public static string AuthPath = "api/Auth";

        public static string ProjectModelsPath = "api/ProjectModels";

        public static string ProjectsPath = "api/Projects";

        public static string TasksProjectsPath = "api/TasksProjects";

        public static string TasksDataPath = "api/TasksData";

        public static string InterventionTypesPath = "api/InterventionTypes";

        public static string InterventionTypeLanguagesPath = "api/InterventionTypeLanguages";

        public static string ContactsPath = "api/Contacts";

        public static string ContractTypesPath = "api/ContractTypes";

        public static string CompanyContractsPath = "api/CompanyContracts";

        public static string TelegramAppConfigsPath = "api/TelegramAppConfigs";

        public static string UserBotPath = "api/UserBot";

        public static string LanguagesPath = "api/Languages";

        public static string LogEventsPath = "api/LogEvents";

        public static string CSVPath = "api/CSVImport";

        public static string CSVMappings = "api/CSVMappings";

        public static string TicketChatsPath = "api/TicketChats";

        public static string EmailsSentPath = "api/EmailsSent";

        public static string ItemEventsPath = "api/ItemEvents";



        public static string PagingHeader = "Paging-Header";

        public static string FileHeader = "File-Header";

        public static string BrunchHeader = "Brunch-Header";

        public static string TaskProject = "TasksProjects";

        public static string CFDoctorsPath = "api/Doctor";

        public static string ClientArticlesPath = "Articles";

        public static string ClientArticleBackupsPath = "ArticleBackups";

        public static string ClientCompaniesPath = "Companies";

        public static string ClientContactsPath = "Contacts";

        public static string ClientDealPath = "Deals";

        public static string ClientAccessoriesPath = "Settings/Accessories";

        public static string ClientAccessoryTypesPath = "Settings/AccessoryTypes";

        public static string ClientAccessoryTypeLangsPath = "Settings/AccessoryTypeLangs";

        public static string ClientProductsSettings = "Settings/Products";

        public static string ClientProductAccTypesPath = "ProductAccTypes";

        public static string ClientProductAccLangsPath = "ProductAccLangs";

        public static string ClientContractTypesPath = "Settings/ContractTypes";

        public static string ClientContractTypeTicketsPath = "Settings/ContractTypeTicketTypes";

        public static string ClientCompanyContractsPath = "CompanyContracts";

        public static string ClientAzurePath = "Settings/AzureSettings";

        public static string ClientProductsPath = "Products";

        public static string ClientProductParametersPath = "ProductParameters";

        public static string ClientEmailTemplatesPath = "Settings/EmailTemplates";

        public static string ClientUsersPath = "Settings/Users";

        public static string ClientSettingsPath = "Settings"; 
        
        public static string ClientTicketsPath = "Tickets";

        

        public static string ClientLogEventsPath = "Settings/LogEvents";
        public static int PageSize = 10;

        public static string PagingSummaryFormatShort = "Page {0} of {1} ({2})";

        public const int MaxAllowSize = 20000000;

    }
}
