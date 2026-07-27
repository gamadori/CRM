using CRM.Shared;
using MimeKit;
using MimeKit.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CRM.Server.Helpers
{
    public static class EmailHelper
    {
        public enum KeyWords
        {
            Name,
            Url,
            Company,
            Ticket,
            Date,
            Reason,
            Commessa,
            Phase
        }
        public static MimeEntity CreateBody(string html, Logo? logo = null, List<string>? attachments = null)
        {
            BodyBuilder builder = new BodyBuilder();

            builder.TextBody = Regex.Replace(html, "<.*?>", String.Empty); ;

            if (logo != null)
            {
                var image = builder.LinkedResources.Add(logo.Codice, Convert.FromBase64String(logo.InputFile));
                image.ContentId = MimeUtils.GenerateMessageId();

                builder.HtmlBody = string.Format(html, image.ContentId);
            }
            else
                builder.HtmlBody = html;

            if (attachments != null)
            {
                foreach (var a in attachments)
                    builder.Attachments.Add(a);
            }

            return builder.ToMessageBody();
        }

        public static string KeyWord(KeyWords key)
        {
            return $"${key.ToString().ToUpper()}";
           
        }

       
    }
}
