using CRM.Server.Data;
using Microsoft.AspNetCore.Hosting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        public byte[] GetAttachment(int id, string ext)
        {
            byte[] content;
            string path = GetPath(id, ext);
            content = File.ReadAllBytes(path);
            return content;
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
