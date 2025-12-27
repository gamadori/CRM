using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ActivityModel 
    {
        public static string GetActivityIcon(ActivityType? type) => type switch
        {
            ActivityType.Ticket => "confirmation_number",
            ActivityType.Message => "chat",
            ActivityType.User => "person",
            _ => "info"
        };
        public static string GetActivityColor(ActivityType? type) => type switch
        {
            ActivityType.Ticket => "#1976d2",
            ActivityType.Message => "#43a047",
            ActivityType.User => "#fbc02d",
            _ => "#757575"
        };

        public string Title { get; set; } 
        public string Description { get; set; } 
        public DateTime Date { get; set; } 
        public ActivityType? Type { get; set; } 

        public string Icon => GetActivityIcon(Type);
    }
    
    public enum ActivityType { Ticket, Message, User }
   
}
