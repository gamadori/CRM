using CRM.Shared;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class EnumService: IEnumService
    {
        private readonly IStringLocalizer<CRM.Shared.Resources.Enums.TalkPhases> _localizeTalkPhases;
        private readonly IStringLocalizer<CRM.Shared.Resources.Enums.TalkStates> _localizeTalkStates;
        private readonly IStringLocalizer<CRM.Shared.Resources.Enums.CompanyTypes> _localizeCompanyTypes;
        private readonly IStringLocalizer<CRM.Shared.Resources.Enums.EmailTemplates> _localizeEmailTemplates;

        public EnumService(IStringLocalizer<CRM.Shared.Resources.Enums.TalkPhases> localizeTalkPhases, IStringLocalizer<CRM.Shared.Resources.Enums.TalkStates> localizeTalkStates,
            IStringLocalizer<CRM.Shared.Resources.Enums.CompanyTypes> localizeCompanyTypes, IStringLocalizer<CRM.Shared.Resources.Enums.EmailTemplates> localizeEmailTemplates)
        {
            _localizeTalkPhases = localizeTalkPhases;
            _localizeTalkStates = localizeTalkStates;
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

                    if (typeEnum == typeof(TalkPhases))
                        txt = _localizeTalkPhases[value.ToString()];
                    else if (typeEnum == typeof(TalkStates))
                        txt = _localizeTalkStates[value.ToString()];
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
                if (typeEnum == typeof(TalkPhases))
                    return _localizeTalkPhases[value.ToString()];
                else if (typeEnum == typeof(TalkStates))
                    return _localizeTalkStates[value.ToString()];
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
