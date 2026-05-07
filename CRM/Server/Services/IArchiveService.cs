using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public enum ArchiveTypes
    {
        Attachments,
        Interventions,
        Pdf,
        Photo,
        Temp,
        ExpenseReceipts
    }

    public interface IArchiveService
    {
        ArchiveTypes TypeArchive { get; set; }

        string GetPath();

        string GetPath(int id, string ext);

        string GetPath(string name);

        bool SaveAttachments(int id, string ext, byte[] content);

        int SaveAttachments(int id, string ext, string content);

        int Save(string name, string content);


        string GetAttachment64(int id, string ext);

        public byte[]? GetAttachment(int id);

        byte[] GetAttachment(int id, string fileName);

        byte[] GetAttachmentByExt(int id, string ext);
    }
}
