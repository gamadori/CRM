using System;

namespace CRM.Server.Services
{
    /// <summary>
    /// Violazione della sequenza di produzione: la fase esiste e l'utente e' abilitato a eseguirla,
    /// ma i predecessori non sono ancora completati. Non e' un problema di permessi (403, "non
    /// potrai mai") bensi' di stato del processo ("non ancora"): i controller la mappano su 409.
    /// </summary>
    public class ProductionSequenceException : InvalidOperationException
    {
        public ProductionSequenceException(string message) : base(message)
        {
        }
    }
}
