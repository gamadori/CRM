using CRM.Server.Data;
using Microsoft.AspNetCore.Hosting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace CRM.Server.Services
{
    public class ArchiveService: IArchiveService
    {
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ApplicationDbContext _context;
        public ArchiveTypes TypeArchive { get; set; } = ArchiveTypes.Attachments;

        public ArchiveService(IWebHostEnvironment hostEnvironment, ApplicationDbContext context)
        {
            _hostEnvironment = hostEnvironment;
            _context = context;
        }

        public int SaveAttachments(int id, string ext, string content)
        {
            try
            {
                string path = GetPath(id, ext);

                byte[] bytes = Convert.FromBase64String(content);

                File.WriteAllBytes(path, bytes);
                return bytes.Length;
            }
            catch
            {
                return 0;
            }
        }
        
        public bool SaveAttachments(int id, string ext, byte[] content)
        {
            try
            {
                string path = GetPath(id, ext);

                File.WriteAllBytes(path, content);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public int Save(string name, string content)
        {
            try
            {
                string path = GetPath(name);

                byte[] bytes = Convert.FromBase64String(content);

                File.WriteAllBytes(path, bytes);
                return bytes.Length;
            }
            catch
            {
                return 0;
            }
        }



        public string GetAttachment64(int id, string ext)
        {
            byte[] content;
            string path = GetPath(id, ext);
            content = File.ReadAllBytes(path);
            return Convert.ToBase64String(content);
        }

        public byte[]? GetAttachment(int id)
        {
            var item = _context.Attachments.FirstOrDefault(a => a.Id == id);

            if (item == null)
                return null;

            var ext = Path.GetExtension(item.Name);

            return GetAttachment(id, ext);
        }
        public byte[] GetAttachment(int id, string name)
        {
            byte[] content;

           var ext = Path.GetExtension(name);

            string path = GetPath(id, ext);
            content = File.ReadAllBytes(path);
            return content;
        }

        public byte[] GetAttachmentByExt(int id, string ext)
        {
            byte[] content;


            string path = GetPath(id, ext);
            content = File.ReadAllBytes(path);
            return content;
        }

        public async Task<(long Size, string Sha256)> SaveStreamAsync(
            int id,
            string fileName,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            var path = GetPath(id, Path.GetExtension(fileName));
            await using var destination = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long size = 0;

            int read;
            while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                hash.AppendData(buffer, 0, read);
                size += read;
            }

            return (size, Convert.ToHexString(hash.GetHashAndReset()));
        }

        public Stream OpenRead(int id, string fileName)
        {
            return new FileStream(
                GetPath(id, Path.GetExtension(fileName)),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }

        public bool Delete(int id, string fileName)
        {
            var path = GetPath(id, Path.GetExtension(fileName));
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }

        public string GetPath()
        {
            string path = Path.Combine(_hostEnvironment.ContentRootPath, "archives", TypeArchive.ToString());
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public string GetPath(int id, string ext)
        {
            string path = GetPath();


            if (ext.Any() && ext.First() != '.')
                ext = "." + ext;
            path = Path.Combine(path, $"{id}{ext}");

           

            return path;
        }

        

        public string GetPath(string name)
        {
            string path = GetPath();
            path = Path.Combine(path, name);



            return path;
        }
        
    }
}
