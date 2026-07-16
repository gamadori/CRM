using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IAttachmentsService : IDataService<Attachment, AttachmentDTO, int, AttachmentsFilter, object>
    {
        Task<bool> CanDelete(int id);

        Task<bool> CanEdit(int id);

        Task<bool> UploadFiles(int idAttachment, List<AttachmentFile> files);

        Task<bool> DeleteFiles(int id);

        Task<(byte[] Bytes, string ContentType, string FileName)> DownloadFiles(int id);

        /// <summary>Scarica un singolo file di un allegato (per <see cref="AttachmentFile"/> id).</summary>
        Task<(byte[] Bytes, string ContentType, string FileName)> DownloadFile(int idFile);
    }
}
