using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.ImportDataFile
{
    public partial class CSVView<TItem>: ComponentBase
    {
        [Inject]
        public HttpClient HttpClient { get; set; }

        [Parameter]
        public List<string[]> CSVFile { get; set; }

        [CascadingParameter]
        public List<CSVMapping> Mapping { get; set; }

        [Parameter]
        public EventCallback<CSVMapping> OnChange  { get; set; }
       
        private int _numCols = 0;

        private string[] _nameFields;
        protected override void OnInitialized()
        {
            if (CSVFile != null && CSVFile.Any())
            {
                var row = CSVFile.FirstOrDefault();

                if (row != null)
                    _numCols = row.Count();
                else
                    _numCols = 0;

                _nameFields = new string[_numCols];

                if (Mapping != null)
                {
                    for (int i = 0; i < _numCols; i++)
                    {
                        var item = Mapping.Where(x => x.NumCol == i).FirstOrDefault();

                        if (item != null)
                        {
                            _nameFields[i] = item.FieldName;
                        }
                    }
                }
            }
        }

        protected async Task OnChangeEvent(int col, string value)
        {
            _nameFields[col] = value;

            CSVMapping m = new CSVMapping() { NumCol = col, FieldName = value };
            await OnChange.InvokeAsync(m);
        }

      

        
    }

    

}
