using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    public class ItemEvent
    {
        [Key]
        public int ItemEventId { get; set; }
        [ForeignKey("Article")]
        public int ItemId { get; set; }
        public EventType EventType { get; set; }
        public DateTime DataEvento { get; set; }
        public int? ClienteId { get; set; }
        public int? DocumentoId { get; set; }
        public string? Note { get; set; }
        public string? UserId { get; set; }

        public Article Article { get; set; }
    }
}