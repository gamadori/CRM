using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QLNet;

namespace CRM.Shared.Helper
{
    public static class DateTimeHelper
    {
        public static DateTime AddBusinessDays(this DateTime date, int days)
        {
            Calendar c = new Italy(Italy.Market.Settlement);
            int numDays = 0;
            
            DateTime d = date;

            while (numDays < days)
            {
                if (c.isBusinessDay(d))
                {
                    numDays++;
                }
                d = d.AddDays(1);
            }
            return d;
        }

        /// <summary>
        /// Primo giorno lavorativo da questa data in poi, festivi italiani compresi. Se la data
        /// e' gia' lavorativa torna identica, orario incluso: sposta il giorno, non l'ora.
        /// <para>
        /// Serve alla data proposta quando si apre un ticket nuovo. Aprirlo a Ferragosto e
        /// vedersi proporre Ferragosto non aiuta: quel giorno non ci lavora nessuno, e la
        /// scadenza calcolata a partire da li' nasce gia' storta.
        /// </para>
        /// <para>
        /// Il calendario e' quello di QLNet (Italy.Settlement), gia' usato qui sopra. Il server
        /// ne ha un altro in CRM.Server.Extensions.DateTimeExtensions, basato sul pacchetto
        /// DateTimeExtensions: confrontati giorno per giorno dal 2026 al 2030 dicono le stesse
        /// cose - stessi 39 festivi infrasettimanali, zero differenze - quindi la data proposta
        /// qui e la scadenza calcolata la' non possono contraddirsi.
        /// </para>
        /// </summary>
        public static DateTime NextBusinessDay(this DateTime date)
        {
            Calendar c = new Italy(Italy.Market.Settlement);

            DateTime d = date;
            while (!c.isBusinessDay(d))
                d = d.AddDays(1);

            return d;
        }

        public static int BusinessDaysBetween(DateTime from, DateTime to)
        {
            Calendar c = new Italy(Italy.Market.Settlement);

            return c.businessDaysBetween(from, to);
        }

        public static string MinuteFormat2(int minute)
        {
            TimeSpan time = TimeSpan.FromMinutes(minute);
            
            return time.ToString(@"hh\:mm");
        }

        public static string MinuteFormat(int? value)
        {
            if (value == null)
                return "";
            else
            {
                int m = value.Value % 60;
                int h = value.Value / 60;
                return $"{h.ToString("00")}:{m.ToString("00")}";
            }
        }
    }
}
