using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class RestGetClientService<T> : IRestService<T> where T : class
    {
        private readonly HttpClient _http;

        private readonly string _pathService;

        public RestGetClientService(HttpClient http, string path)
        {
            _http = http;
            _pathService = path;
        }

        public async Task<T> Get()
        {
            T response = await _http.GetFromJsonAsync<T>(_pathService);
            return response;
        }

        
    }

    


    public class RestClientService<T, F, K> : IBaseRestService<T, F, K> where T : class where F : PagingParameterModel, new()
    {
        protected readonly HttpClient _http;

        protected readonly string _pathService;

        // Simple in-flight requests cache to deduplicate concurrent identical GET by id calls
        private readonly ConcurrentDictionary<string, Task<T>> _inFlightGetById = new();

        //protected readonly SignOutSessionStateManager _signOutManager;
        //protected readonly NavigationManager _navigation;

        public RestClientService(HttpClient http, string path)
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

                string key = $"GET:{_pathService}/{id}";

                // If an identical request is already in flight return its task
                if (_inFlightGetById.TryGetValue(key, out var existingTask))
                {
                    try
                    {
                        return await existingTask;
                    }
                    catch
                    {
                        // If the existing task failed, remove it so we can try again below
                        _inFlightGetById.TryRemove(key, out _);
                        throw;
                    }
                }

                // Create the task and add to dictionary before starting the request to ensure other callers share it
                var task = GetInternal(id);
                if (!_inFlightGetById.TryAdd(key, task))
                {
                    // Another thread added it in the meantime, use that one
                    if (_inFlightGetById.TryGetValue(key, out var racedTask))
                    {
                        return await racedTask;
                    }
                }

                try
                {
                    var result = await task;
                    return result;
                }
                finally
                {
                    // remove from cache when finished
                    _inFlightGetById.TryRemove(key, out _);
                }
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                if (ex.Message.Contains("401"))
                {
                   
                }
                return null;
            }
        }

        private async Task<T> GetInternal(K id)
        {
            return await _http.GetFromJsonAsync<T>(_pathService + $"/{id}");
        }

       

        public async Task<BreadCrumb<T>> GetWithBreadCrumb(K id, string root)
        {
            try
            {
                BreadCrumb<T> model = new BreadCrumb<T>();

                if (id == null)
                    return null;

                string path = _pathService + $"/{id}";
                if (root != null)
                {
                    path += $"?$root={root}";
                }
                var response = await _http.GetAsync(path);


                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    model = new BreadCrumb<T>()
                    {
                        Item = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }),
                        Bread = JsonSerializer.Deserialize<List<BreadcrumbModel>>(response.Headers
                            .GetValues(ValuesHelper.BreadcrumbHeader).First(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true })
                    };
                }
                return model;
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

        public async Task<List<T>> Get()
        {
            F data = new F();
            try
            {
                data.PageNumber = 0;

                var response = await _http.GetAsync(_pathService);


                if (response.IsSuccessStatusCode)
                {


                    var content = await response.Content.ReadAsStringAsync();

                    return JsonSerializer.Deserialize<List<T>>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

                   
                }
                else
                    return null;

               

            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<PagingResponse<T>> GetList(F data)
        {
            data.Skip = data.Top = null;

            return await Get(data);
        }

       
        public async Task<PagingResponse<T>> Get(F data, string folder = null)
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
                {
                    path = $"{path}/{folder}";
                }
                var response = await _http.GetAsync(path + qs);


                if (response.IsSuccessStatusCode)
                {

                    
                    var content = await response.Content.ReadAsStringAsync();

                    var pagingResponse = new PagingResponse<T>()
                    {
                        Items = JsonSerializer.Deserialize<List<T>>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }),
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
          

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<List<I>> GetItems<I>(F data) 
        {
            try
            {
                var content = await Filter(data);

                if (content != null)
                {
                    var items = JsonSerializer.Deserialize<List<I>>(content, new JsonSerializerOptions() {  PropertyNameCaseInsensitive = true});
                    return items;
                }
                

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
                if (ex.Message.Contains("401"))
                {

                }
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
                    data.Message = $"{resp.ReasonPhrase}\n\r{await resp.Content.ReadAsStringAsync()}";
                    return null;

                }
                
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return null;
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
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        private async Task<string> Filter(F data, string folder = null)
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
                {
                    path = $"{path}/{folder}";
                }
                var response = await _http.GetAsync(path + qs);


                if (response.IsSuccessStatusCode)
                {


                    var content = await response.Content.ReadAsStringAsync();
                    return content;

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

    }
}
