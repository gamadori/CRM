using AGUtility.Extensions;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Syncfusion.Blazor;
using Syncfusion.Blazor.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Components
{
    public class AGDataAdaptor<T, F>: DataAdaptor where T : class where F: PagingParameterModel
    {
       
        [Inject]
        private IBaseRestService<T, F, int> _service { get; set; }


        [Parameter]
        public string RequestUrl { get; set; }

        [Parameter]
        public F Filter { get; set; }

        [Parameter]
        public string IdParentFieldName { get; set; }

        [Parameter]
        public int? IdParentValue { get; set; }


        [Parameter]
        public List<ParameterSetting> Parameters { get; set; }


        public EventCallback<int> AddEvent { get; set; }

        public override async Task<object> ReadAsync(DataManagerRequest dm, string key = null)
        {
            Filter.PageSize = 0;

            var resp = await _service.Get(Filter);

            IEnumerable<object> data = resp.Items;

            //IEnumerable<object> data = await _http.GetFromJsonAsync<IEnumerable<T>>(RequestUrl) as IEnumerable<object>;

            return dm.RequiresCounts ? new DataResult() { Result = data, Count = data.Count() } : (object)data;
        }

        // Performs CRUD operation
        public override async Task<object> BatchUpdateAsync(DataManager dm, object changedRecords, object addedRecords, object deletedRecords, string keyField, string key, int? dropIndex)

        {

            List<T> addRecord = addedRecords as List<T>;
            List<T> changed = changedRecords as List<T>;
            List<T> deleteRecord = deletedRecords as List<T>;

            if (changed != null)
            {
                for (var i = 0; i < changed.Count(); i++)
                {
                    

                    int id = changed[i].GetPropertyValue<int>("Id"); ;
                    await _service.Post(changed[i]);
                    //await _http.PutAsJsonAsync<T>($"{RequestUrl}/" + id, changed[i] as T);
                }
            }
            if (deleteRecord != null)
            {
                for (var i = 0; i < deleteRecord.Count(); i++)
                {
                    int id = changed[i].GetPropertyValue<int>("Id"); ;
                    await _service.Delete(id);
                    //await _http.DeleteAsync($"{RequestUrl}/" + id);
                }
            }
            if (addRecord != null)
            {


                for (var i = 0; i < addRecord.Count(); i++)
                {
                    SetParameters(addRecord[i]);

                    var id = addRecord[i].GetPropertyValue<int>("Id");

                    addRecord[i].SetPropertyValue("Id", 0);

                    var resp = await _service.Post(addRecord[i]);
                    addRecord[i] = resp.Data;

                }
            }

            //await AddEvent.InvokeAsync(addRecord.Count());

            return (new { addedRecords = addRecord, changedRecords = changed, deletedRecords = deleteRecord });

            //var resp = await _service.Get(Filter);

            //IEnumerable<object> data = resp.Items;

            //return true ? new DataResult() { Result = data, Count = data.Count() } : (object)data;
        }

        private void SetParameters(T record)
        {
            if (Parameters != null)
            {
                foreach (var p in Parameters)
                {
                    record.SetPropertyValue(p.Name, p.Value);
                }
            }
                
        }

    }
    
}
