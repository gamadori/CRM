using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Products
{
    public partial class CatalogAssets : ComponentBase
    {
        [Parameter]
        public int IdProduct { get; set; }

        private readonly List<FileWithName> _filesWithNames = new();
        private readonly Dictionary<int, string> _imageSources = new();
        private List<ProductCatalogAssetDTO> _assets = new();
        private bool _loading = true;
        private bool _waitingUpload;
        private string? _errorMessage;

        protected override async Task OnInitializedAsync()
        {
            await LoadAssets();
        }

        private async Task LoadAssets()
        {
            _loading = true;
            _errorMessage = null;

            var items = await CatalogAssetsService.GetListAsync(new ProductCatalogAssetFilter { IdProduct = IdProduct });
            _assets = items ?? new List<ProductCatalogAssetDTO>();
            await LoadImageSources();

            _loading = false;
        }

        private async Task LoadImageSources()
        {
            _imageSources.Clear();
            foreach (var asset in _assets.Where(x => x.MediaType == ProductCatalogMediaTypes.Image))
            {
                var bytes = await CatalogAssetsService.DownloadFileAsync(asset.Id);
                if (bytes.Length > 0)
                {
                    var contentType = string.IsNullOrWhiteSpace(asset.ContentType) ? "image/jpeg" : asset.ContentType;
                    _imageSources[asset.Id] = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
                }
            }
        }

        private async Task OnInputFileChange(InputFileChangeEventArgs e)
        {
            _errorMessage = null;
            const long maxFileSize = 20 * 1024 * 1024;

            foreach (var file in e.GetMultipleFiles(50))
            {
                var extension = Path.GetExtension(file.Name).ToLowerInvariant();
                if (!IsCatalogFile(extension))
                {
                    _errorMessage = $"File non supportato: {file.Name}";
                    continue;
                }

                if (file.Size > maxFileSize)
                {
                    _errorMessage = $"File {file.Name} troppo grande (max 20MB)";
                    continue;
                }

                using var stream = file.OpenReadStream(maxFileSize);
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);

                _filesWithNames.Add(new FileWithName
                {
                    OriginalFileName = file.Name,
                    NewName = Path.GetFileNameWithoutExtension(file.Name),
                    Extension = extension,
                    FileBytes = memoryStream.ToArray(),
                    ContentType = file.ContentType,
                    Size = file.Size
                });
            }
        }

        private async Task UploadSelectedFiles()
        {
            if (!_filesWithNames.Any())
            {
                return;
            }

            _waitingUpload = true;
            _errorMessage = null;

            var request = new ProductCatalogAssetUploadRequest
            {
                IdProduct = IdProduct,
                IncludeInCatalog = true,
                Files = _filesWithNames.Select(file => new AttachmentFile
                {
                    Name = $"{file.NewName}{file.Extension}",
                    Content = Convert.ToBase64String(file.FileBytes),
                    ContentType = file.ContentType,
                    Size = file.Size
                }).ToList()
            };

            var response = await CatalogAssetsService.UploadAsync(request);
            if (response.State)
            {
                _filesWithNames.Clear();
                await LoadAssets();
            }
            else
            {
                _errorMessage = response.Message;
            }

            _waitingUpload = false;
        }

        private async Task SaveAsset(ProductCatalogAssetDTO asset)
        {
            var response = await CatalogAssetsService.PostAsync(new ProductCatalogAsset
            {
                Id = asset.Id,
                IdProduct = asset.IdProduct,
                IdAttachmentFile = asset.IdAttachmentFile,
                MediaType = asset.MediaType,
                Title = asset.Title,
                Description = asset.Description,
                SortOrder = asset.SortOrder,
                IsCover = asset.IsCover,
                IncludeInCatalog = asset.IncludeInCatalog,
                CreatedOn = asset.CreatedOn
            });

            if (!response.State)
            {
                _errorMessage = response.Message;
            }

            await LoadAssets();
        }

        private async Task SetCover(int id)
        {
            if (await CatalogAssetsService.SetCoverAsync(id))
            {
                await LoadAssets();
            }
        }

        private async Task DeleteAsset(int id)
        {
            if (await DialogService.Confirm("Eliminare il media selezionato?", "Attenzione") == true)
            {
                await CatalogAssetsService.DeleteAsync(id);
                await LoadAssets();
            }
        }

        private void RemoveFile(FileWithName file)
        {
            _filesWithNames.Remove(file);
        }

        private static bool IsCatalogFile(string extension)
        {
            return extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".dxf";
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private class FileWithName
        {
            public string OriginalFileName { get; set; } = string.Empty;

            public string NewName { get; set; } = string.Empty;

            public string Extension { get; set; } = string.Empty;

            public byte[] FileBytes { get; set; } = Array.Empty<byte>();

            public string ContentType { get; set; } = string.Empty;

            public long Size { get; set; }
        }
    }
}
