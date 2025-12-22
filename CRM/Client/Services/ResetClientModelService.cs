using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Shared.Helper;
using CRM.Shared;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Net.Http.Json;
using System.Linq;
using QLNet;
using CRM.Client.Shared;

namespace CRM.Client.Services
{
    public class RestClientModelService<T, M, F, K> : IRestClientModelService<T, M, F, K> where T : class where M: class where F : PagingParameterModel, new()
    {
        protected readonly HttpClient _http;

        protected readonly string _pathService;

        public RestClientModelService(HttpClient http, string path)
        {
            _http = http;
            _pathService = path;
        }

        public async Task<T> Get(K id)
        {
            try
            {
                if (id == null)
                    return null;
                T response = await _http.GetFromJsonAsync <T>(_pathService + $"/{id}");

                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
               
                return null;
            }
        }

        public async Task<M> GetDetails(K id)
        {
            try
            {
                if (id == null)
                    return null;
                M response = await _http.GetFromJsonAsync<M>(_pathService + $"/Details/{id}");

                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<string?> Print(K id)
        {
            try
            {
                if (id == null)
                    return null;
                var s = await _http.GetStringAsync(_pathService + $"/Report/{id}");

                return s;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
        public async Task<M> Get()
        {
            F data = new F();
            try
            {
                data.PageNumber = 0;

                var response = await _http.GetAsync(_pathService);


                if (response.IsSuccessStatusCode)
                {


                    var content = await response.Content.ReadAsStringAsync();

                    return JsonSerializer.Deserialize<M>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });


                }
                else
                    return null;



            }
            catch
            {
                return null;
            }
        }

        public async Task<PagingResponse<M, string>> GetList(F data)
        {
            data.Skip = data.Top = null;

            return await Get<string>(data);
        }

        public async Task<PagingResponse<M, S>> GetList<S>(F data)
        {
            data.Skip = data.Top = null;

            return await Get<S>(data);
        }

        public async Task<PagingResponse<M>> GetItems(F data)
        {
            data.Skip = data.Top = null;

            return await Get(data);
        }

        public async Task<PagingResponse<M>> Get(F data, string folder = null)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();


                Type returnType = data.GetType();
                var fields = returnType.GetProperties();


                foreach (var field in fields)
                {

                    string value;
                    var obj = UtilityHelper.GetPropertyValue<object>(data, field.Name);

                    if (field.PropertyType == typeof(DateTime) || (field.PropertyType == typeof(DateTime?) && obj != null))
                        value = ((DateTime)obj).ToString("yyyy-MM-dd");
                    else
                        value = obj?.ToString();


                    param.Add(field.Name, value);
                }


                var qs = UriHelper.BuildQueryString(param);

                var path = _pathService;

                if (folder != null) 
                    path = $"{path}/{folder}";

                var response = await _http.GetAsync(path + qs);


                if (response.IsSuccessStatusCode)
                {


                    var content = await response.Content.ReadAsStringAsync();

                    var pagingResponse = new PagingResponse<M>()
                    {
                        Items = JsonSerializer.Deserialize<List<M>>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }),
                        MetaData = JsonSerializer.Deserialize<PagingHeaderModel>(response.Headers
                            .GetValues(ConstHelper.PagingHeader).First(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true })
                    };
                    return pagingResponse;
                }
                else
                    return null;
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return null;
            }

        }
        public async Task<PagingResponse<M, S>> Get<S>(F data)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();


                Type returnType = data.GetType();
                var fields = returnType.GetProperties();


                foreach (var field in fields)
                {

                    string value;
                    var obj = UtilityHelper.GetPropertyValue<object>(data, field.Name);

                    if (field.PropertyType == typeof(DateTime) || (field.PropertyType == typeof(DateTime?) && obj != null))
                        value = ((DateTime)obj).ToString("yyyy-MM-dd");
                    else
                        value = obj?.ToString();


                    param.Add(field.Name, value);
                }


                var qs = UriHelper.BuildQueryString(param);

                var response = await _http.GetAsync(_pathService + qs);


                if (response.IsSuccessStatusCode)
                {


                    var content = await response.Content.ReadAsStringAsync();
                    ObjectView<M, S> item = JsonSerializer.Deserialize<ObjectView<M, S>>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

                    var pagingResponse = new PagingResponse<M, S>()
                    {
                        Items = item.Items,
                        MetaData = JsonSerializer.Deserialize<PagingHeaderModel>(response.Headers
                            .GetValues(ConstHelper.PagingHeader).First(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }),
                        Total = item.Total
                    };
                    return pagingResponse;
                }
                else
                    return null;
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return null;
            }


            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }


        public async Task<APIResponseMessage<T>> Post(T item)
        {
            APIResponseMessage<T> data = new APIResponseMessage<T>();
            try
            {


                HttpResponseMessage resp;
                Type myType = typeof(T);

                var id = UtilityHelper.GetPropertyValue<K>(item, "Id");

                if (id != null && id.ToString() != "0")
                    resp = await _http.PutAsJsonAsync<T>($"{_pathService}/{id}", item);
                else
                    resp = await _http.PostAsJsonAsync<T>(_pathService, item);



                if (resp.IsSuccessStatusCode)
                {
                    data.State = true;
                    data.Message = "OK";
                    data.Code = resp.StatusCode;

                    if (resp.StatusCode != System.Net.HttpStatusCode.NoContent)
                    {
                        data.Data = JsonSerializer.Deserialize<T>(await resp.Content.ReadAsStringAsync(),
                            new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                    }
                    return data;
                }
                else
                {
                    data.State = false;
                    data.Code = resp.StatusCode;
                    data.Message = $"{resp.ReasonPhrase} {await resp.Content.ReadAsStringAsync()}";
                    return null;

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                data.State = false;
                data.Code = System.Net.HttpStatusCode.NotFound;
                data.Message = ex.Message;
                return null;
            }
        }

        public async Task<bool> Delete(K id)
        {
            try
            {
                var resp = await _http.DeleteAsync($"{_pathService}/{id}");

                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }


    }
}


