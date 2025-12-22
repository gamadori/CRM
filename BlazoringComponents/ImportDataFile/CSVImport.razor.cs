using CRM.Shared;
using Microsoft.AspNetCore.Components;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlazoringComponents.ImportDataFile
{
    public partial class CSVImport<TItem> : ComponentBase
    {
        [Inject]
        public HttpClient httpClient { get; set; }

        [Parameter]
        public string Url { get; set; }

        [Parameter]
        public string UrlImport { get; set; }

        [Parameter]
        public string Delimiter { get; set; }

        [Parameter]

        public Func<List<CSVMapping>, Task> OnSubmit { get; set; }

        [Parameter]
        public EventCallback<bool> OnUploaded { get; set; }

        [Parameter]
        public bool Mapping { get; set; } = false;

        [CascadingParameter]
        public List<CSVMapping> Values { get; set; }

        [Parameter]
        public string TableName { get; set; }

        
        private bool _preview = false;

        private List<string[]> _csvFile = new List<string[]>();

        private string _fileName;


        protected override void OnInitialized()
        {

            base.OnInitialized();
        }
        private async void OnChange(Syncfusion.Blazor.Inputs.UploadChangeEventArgs args)
        {

            _preview = false;
           
            var file = args.Files.FirstOrDefault();

            if (file != null)
            {
                var buf = file.Stream.ToArray();
                file.Stream.Close();
                _fileName = System.IO.Path.GetFileNameWithoutExtension(file.FileInfo.Name);

                CSVFile f = new CSVFile()
                {
                    Content = Convert.ToBase64String(buf),
                    Name = file.FileInfo.Name,
                    Delimiter = Delimiter
                };
                HttpResponseMessage resp = await httpClient.PostAsJsonAsync<CSVFile>(Url, f);

                if (resp.IsSuccessStatusCode)
                {
                    var content = await resp.Content.ReadAsByteArrayAsync();

                    _csvFile = JsonSerializer.Deserialize<List<string[]>>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });


                    _preview = true;

                    StateHasChanged();
                }
                
            }
        }

        protected void UpdateMapping(CSVMapping m)
        {
            if (Values == null)
                Values = new List<CSVMapping>();

            var item = Values.Where(x => x.NumCol == m.NumCol).FirstOrDefault();

            if (item == null)
            {
                item = new CSVMapping();
                Values.Add(item);
            }
            item.TableName = TableName;
            item.NumCol = m.NumCol;
            item.FieldName = m.FieldName;
        }

        protected async void OnClick()
        {
            await OnSubmit(Values);
        }

        protected async Task UploadData()
        {
            var resp = await httpClient.PostAsJsonAsync<List<string[]>>($"{UrlImport}/{_fileName}", _csvFile);

            await OnUploaded.InvokeAsync(resp.IsSuccessStatusCode);
        }
        

    }
}
