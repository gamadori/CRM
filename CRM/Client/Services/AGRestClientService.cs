using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Shared;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Net.Http.Json;
using System.Linq;
using CRM.Shared.Helper;
using System.Collections.Concurrent;

namespace CRM.Client.Services
{
    public class AGRestClientService: IAGRestClientService
    {
        protected readonly HttpClient _http;

        // Generic caches to centralize name/id fetching deduplication
        private readonly ConcurrentDictionary<string, object> _itemCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, Task<object>> _inFlightItemRequests = new(StringComparer.OrdinalIgnoreCase);

        public AGRestClientService(HttpClient http)
        {
            _http = http;
        }

        private string BuildCacheKey(string pathService, object id)
        {
            return $"{pathService}:{id}";
        }

        public async Task<T?> GetItem<T, K>(K id, string pathService) where T : class
        {
            if (id == null)
                return null;

            string key = BuildCacheKey(pathService, id);

            if (_itemCache.TryGetValue(key, out var cached) && cached is T cachedT)
                return cachedT;

            if (_inFlightItemRequests.TryGetValue(key, out var inFlight))
            {
                try
                {
                    var obj = await inFlight;
                    return obj as T;
                }
                catch
                {
                    _inFlightItemRequests.TryRemove(key, out _);
                    throw;
                }
            }

            var task = Task.Run(async () =>
            {
                try
                {
                    var result = await _http.GetFromJsonAsync<T>($"{pathService}/{id}");
                    return (object)result;
                }
                catch
                {
                    return (object)null;
                }
            });

            if (!_inFlightItemRequests.TryAdd(key, task))
            {
                if (_inFlightItemRequests.TryGetValue(key, out var existing))
                {
                    var res = await existing;
                    return res as T;
                }
            }

            try
            {
                var obj = await task;
                if (obj != null)
                    _itemCache[key] = obj;
                return obj as T;
            }
            finally
            {
                _inFlightItemRequests.TryRemove(key, out _);
            }
        }

        public async Task<BreadCrumb<T>> GetWithBreadCrumb<T, K>(K id, string root, string pathService)
        {
            try
            {
                BreadCrumb<T> model = new BreadCrumb<T>();

                if (id == null)
                    return null;

                string path = pathService + $"/{id}";

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

        public async Task<T?> GetFirst<T>(string pathService) where T : class
        {

            T? response = await _http.GetFromJsonAsync<T?>($"{pathService}");

            return response;
        }

        public async Task<PagingResponse<M, S>> Get<M, S, F>(F data, string pathService)
        {
            try
            {
                var qs = CreateQueryString<F>(data);

                var response = await _http.GetAsync(pathService + qs);


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

        public async Task<PagingResponse<T>> Get<T, F>(F data, string pathService) where F: PagingParameterModel
        {
            try
            {
                
                
                if  (data.Top == null || data.Skip == null)
                {
                    data.Top = ConstHelper.PageSize;
                    data.Skip = 0;

                }
                else if (data.Top == 0)
                {
                    data.Top = null;
                    data.Skip = null;
                }

                var qs = CreateQueryString<F>(data);


                var response = await _http.GetAsync($"{pathService}/{qs}");


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
        public async Task<List<T>> Get<T>(string pathService)
        {

            try
            {
            

                var response = await _http.GetAsync(pathService);


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

        /// <summary>
        /// Data utilizzare se nel controller è presente un metodo Get che restituisca la lista
        /// dei dati non paginizzata
        /// </summary>
        /// <typeparam name="T">return Data Type</typeparam>
        /// <typeparam name="F">search filter</typeparam>
        /// <param name="data"></param>
        /// <param name="pathService"></param>
        /// <returns></returns>
        public async Task<List<T>> GetList<T, F>(F data, string pathService)
        {

            try
            {
                var qs = CreateQueryString<F>(data);


                var response = await _http.GetAsync($"{pathService}/list{qs}");

                

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
            catch (Exception ex)
            {

                return null;
            }
        }

        public async Task<PagingResponse<T>> GetListPag<F, T>(F data, string pathService) where F : PagingParameterModel, new()
        {
            data.Skip = data.Top = 0;

            return await Get<T, F>(data, pathService);
        }

        public async Task<APIResponseMessage<T>> Post<T, K>(T item, string pathService)
        {
            APIResponseMessage<T> data = new APIResponseMessage<T>();
            try
            {


                HttpResponseMessage resp;
                Type myType = typeof(T);

                var id = UtilityHelper.GetPropertyValue<K>(item, "Id");

                if (id != null && id.ToString() != "0")
                    resp = await _http.PutAsJsonAsync<T>($"{pathService}/{id}", item);
                else
                    resp = await _http.PostAsJsonAsync<T>(pathService, item);



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
                    return data;

                }

            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<T>() { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                data.State = false;
                data.Code = System.Net.HttpStatusCode.NotFound;
                data.Message = ex.Message;
                return data;
            }
        }

        public async Task<bool> Delete<K>(K id, string pathService)
        {
            try
            {
                var resp = await _http.DeleteAsync($"{pathService}/{id}");

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

        private string CreateQueryString<F>(F data)
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

           

            return qs;

        }
    }
}
