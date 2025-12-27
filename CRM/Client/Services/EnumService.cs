using CRM.Shared;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class EnumService: IEnumService
    {
        private readonly IStringLocalizer<CRM.Shared.Resources.Enums.DealPhases> _localizeDealPhases;
        private readonly IStringLocalizer<CRM.Shared.Resources.Enums.DealStates> _localizeDealStates;
        private readonly IStringLocalizer<CRM.Shared.Resources.Enums.CompanyTypes> _localizeCompanyTypes;
        private readonly IStringLocalizer<CRM.Shared.Resources.Enums.EmailTemplates> _localizeEmailTemplates;

        public EnumService(IStringLocalizer<CRM.Shared.Resources.Enums.DealPhases> localizeDealPhases, IStringLocalizer<CRM.Shared.Resources.Enums.DealStates> localizeDealstates,
            IStringLocalizer<CRM.Shared.Resources.Enums.CompanyTypes> localizeCompanyTypes, IStringLocalizer<CRM.Shared.Resources.Enums.EmailTemplates> localizeEmailTemplates)
        {
            _localizeDealPhases = localizeDealPhases;
            _localizeDealStates = localizeDealstates;
            _localizeCompanyTypes = localizeCompanyTypes;
            _localizeEmailTemplates = localizeEmailTemplates;
        }
        public List<EnumField> EnumGetList(Type typeEnum, List<int>mask = null) 
        {
            var list = new List<EnumField>();

            Array values = System.Enum.GetValues(typeEnum);
            foreach (var value in values)
            {
                string txt;

                if (mask == null || !mask.Contains((int)value))
                {

                    if (typeEnum == typeof(DealPhases))
                        txt = _localizeDealPhases[value.ToString()];
                    else if (typeEnum == typeof(DealStates))
                        txt = _localizeDealStates[value.ToString()];
                    else if (typeEnum == typeof(CompanyTypes))
                        txt = _localizeCompanyTypes[value.ToString()];
                    else if (typeEnum == typeof(EmailsTypes))
                        txt = _localizeEmailTemplates[value.ToString()];
                    else
                        txt = value.ToString();

                    list.Add(new EnumField { Value = value, Text = txt });
                }
            }

            return list;

        }


        public string Get(Type typeEnum, object value)
        {

            try
            {
                if (typeEnum == typeof(DealPhases))
                    return _localizeDealPhases[value.ToString()];
                else if (typeEnum == typeof(DealStates))
                    return _localizeDealStates[value.ToString()];
                else if (typeEnum == typeof(CompanyTypes))
                    return _localizeCompanyTypes[value.ToString()];
                else if (typeEnum == typeof(EmailsTypes))

                    return _localizeEmailTemplates[value.ToString()];

                else
                    return value.ToString();
            }
            catch(Exception ex)
            {
                return value.ToString();
            }
        }

    }


    public interface IEnumService
    {
        List<EnumField> EnumGetList(Type typeEnum, List<int> m = null);

        string Get(Type typeEnum, object value);
    }
}
