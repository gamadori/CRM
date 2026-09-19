using System;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared
{
    /// <summary>
    /// Lega una macchina (<see cref="Article"/>) al dispositivo di assistenza remota
    /// registrato sul server di provisioning. Il provisioning identifica ogni pannello
    /// con un GUID; qui si tiene la corrispondenza con la matricola, senza mai salvare
    /// chiavi VPN o password VNC (quelle restano sul VPS).
    /// </summary>
    public class MachineRemoteSupport
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Matricola della macchina nel CRM (FK verso Articles).</summary>
        public int IdArticle { get; set; }

        /// <summary>Identificativo del pannello sul server di provisioning.</summary>
        public Guid DeviceId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
