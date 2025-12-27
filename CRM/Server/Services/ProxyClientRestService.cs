using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Net.Http.Json;
using System.Linq;
using CRM.Shared;
using CRM.Client.Models;
using CRM.Client.Helpers;




namespace CRM.Client.Services
{
    public class ProxyRestClientService<T, K, F, S> : IDataService<T, K, F, S>
    {
        protected readonly HttpClient _http;

        protected readonly string _pathService;

        public ProxyRestClientService(HttpClient http, string pathService)
        {
            _http = http;
            _pathService = pathService;
        }


        /// <summary>
        /// Retrieves an item of the specified type from a given service path using the provided identifier.
        /// </summary>
        /// <remarks>This method performs an HTTP GET request to the specified service path combined with
        /// the identifier. Ensure that the service path and identifier are correctly formatted to avoid request
        /// errors.</remarks>
        /// <typeparam name="T">The type of the item to retrieve. Must be a reference type.</typeparam>
        /// <typeparam name="K">The type of the identifier used to locate the item.</typeparam>
        /// <param name="id">The identifier of the item to retrieve. This is appended to the service path to form the request URL.</param>
        /// <param name="pathService">The base path of the service from which to retrieve the item. This should be a valid URL segment.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the item of type <typeparamref
        /// name="T"/> if found; otherwise, <see langword="null"/>.</returns>
        public async Task<T?> GetItemAsync(K id)
        {
            T? response;

            response = await _http.GetFromJsonAsync<T?>($"{_pathService}/{id}");

            return response;
        }

        /// <summary>
        /// Asynchronously retrieves the first object of type <typeparamref name="T"/> from the specified service path.
        /// </summary>
        /// <typeparam name="T">The type of the object to retrieve. Must be a reference type.</typeparam>
        /// <param name="pathService">The service path from which to retrieve the object. Cannot be null or empty.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the object of type <typeparamref
        /// name="T"/> retrieved from the service, or <see langword="null"/> if no object is found.</returns>
        public async Task<T?> GetFirstAsync()
        {

            T? response = await _http.GetFromJsonAsync<T?>($"{_pathService}");

            return response;
        }

        /// <summary>
        /// Asynchronously retrieves paginated data from a specified service endpoint.
        /// </summary>
        /// <remarks>This method constructs a query string from the provided <paramref name="data"/> and
        /// appends it to the <paramref name="pathService"/> to form the request URL. It then sends an HTTP GET request
        /// to the specified service endpoint. If the response is successful, it deserializes the content into an <see
        /// cref="ObjectView{M, S}"/> and extracts the items and metadata to form the <see cref="PagingResponse{M,
        /// S}"/>.</remarks>
        /// <typeparam name="T">The type of the items in the response.</typeparam>
        /// <typeparam name="S">The type of the metadata associated with the response.</typeparam>
        /// <typeparam name="F">The type of the filter or query parameters used to construct the request.</typeparam>
        /// <param name="data">The filter or query parameters used to construct the request. Cannot be null.</param>
        /// <param name="pathService">The service endpoint path to which the request is sent. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="PagingResponse{M,
        /// S}"/> with the items and metadata if the request is successful; otherwise, <see langword="null"/>.</returns>
        public async Task<PagingResponse<T, S>?> GetSummaryAsync(F? data)
        {
            try
            {
                var qs = CreateQueryString(data);

                var response = await _http.GetAsync(_pathService + qs);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    PagingResponse<T, S> item = JsonSerializer.Deserialize<PagingResponse<T, S>>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }) ?? new();
                    
                    return item;
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
        /// <summary>
        /// Asynchronously retrieves a paginated response from the specified service endpoint.
        /// </summary>
        /// <remarks>If the access token is not available, the method will redirect the user to obtain a
        /// new token. Any other exceptions are logged to the console, and the method returns <see
        /// langword="null"/>.</remarks>
        /// <typeparam name="T">The type of the items contained in the paginated response.</typeparam>
        /// <typeparam name="F">The type of the data used to create the query string for the request.</typeparam>
        /// <param name="data">The data used to generate the query string for the request. This data is serialized into query parameters.</param>
        /// <param name="pathService">The service endpoint path to which the request is sent.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a <see
        /// cref="PagingResponse{T}"/> object if the request is successful; otherwise, <see langword="null"/>.</returns>
        public async Task<PagingResponse<T>?> GetPagingAsync(F? data)
        {
            try
            {
                var qs = CreateQueryString(data);

                var response = await _http.GetAsync(_pathService + qs);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    PagingResponse<T> item = JsonSerializer.Deserialize<PagingResponse<T>>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }) ?? new();

                    return item;
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


       

        /// <summary>
        /// Data utilizzare se nel controller è presente un metodo Get che restituisca la lista
        /// dei dati non paginizzata
        /// </summary>
        /// <typeparam name="T">return Data Type</typeparam>
        /// <typeparam name="F">search filter</typeparam>
        /// <param name="data"></param>
        /// <param name="pathService"></param>
        /// <returns></returns>
        public async Task<List<T>?> GetListAsync(F? data)
        {
            try
            {
                var qs = CreateQueryString(data);

                var response = await _http.GetAsync($"{_pathService}/list{qs}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    return JsonSerializer.Deserialize<List<T>>(content, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }) ?? new();
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
        /// Sends an HTTP POST or PUT request asynchronously to the specified service path with the provided item.
        /// </summary>
        /// <remarks>If the item's "Id" property is not null and not equal to zero, a PUT request is sent;
        /// otherwise, a POST request is sent. The method handles exceptions related to access token availability and
        /// general exceptions, returning appropriate response messages.</remarks>
        /// <typeparam name="T">The type of the item to be sent in the request body.</typeparam>
        /// <typeparam name="K">The type of the identifier property of the item.</typeparam>
        /// <param name="item">The item to be sent in the request body. The item must have an "Id" property of type <typeparamref
        /// name="K"/>.</param>
        /// <param name="pathService">The service path to which the request is sent. This should be a valid URI path.</param>
        /// <returns>An <see cref="APIResponseMessage{T}"/> containing the response data, status code, and a success state. If
        /// the request is successful, the response data will contain the deserialized object of type <typeparamref
        /// name="T"/>.</returns>
        public async Task<APIResponseMessage<T>> PostAsync(T item)
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
                    
                    if (resp.StatusCode != System.Net.HttpStatusCode.NoContent)
                    {
                        data = JsonSerializer.Deserialize<APIResponseMessage<T>>(await resp.Content.ReadAsStringAsync(),
                            new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }) ??
                                new APIResponseMessage<T>() { State = false, Message = "null" };
                    }
                    else
                    {
                        data.State = true;
                        data.Message = "OK";
                        data.Code = resp.StatusCode;

                    }
                    return data ;
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

        /// <summary>
        /// Asynchronously deletes a resource identified by the specified ID from the given service path.
        /// </summary>
        /// <remarks>This method sends an HTTP DELETE request to the specified service path. If the access
        /// token is not available, the user is redirected, and the method returns <see langword="false"/>.</remarks>
        /// <typeparam name="K">The type of the identifier used to locate the resource.</typeparam>
        /// <param name="id">The identifier of the resource to be deleted. Cannot be null.</param>
        /// <param name="pathService">The base path of the service from which the resource will be deleted. Cannot be null or empty.</param>
        /// <returns><see langword="true"/> if the resource was successfully deleted; otherwise, <see langword="false"/>.</returns>
        public async Task<bool> DeleteAsync(K id)
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

        /// <summary>
        /// Creates a query string from the properties of the specified object.
        /// </summary>
        /// <remarks>The method converts each property of the object into a query string parameter. 
        /// DateTime properties are formatted as "yyyy-MM-dd". Null property values are represented as empty
        /// strings.</remarks>
        /// <typeparam name="F">The type of the object whose properties are used to create the query string.</typeparam>
        /// <param name="data">The object containing properties to be converted into query string parameters. Cannot be null.</param>
        /// <returns>A query string representing the object's properties and their values. The query string is empty if the
        /// object has no properties.</returns>
        private string CreateQueryString(F? data)
        {
            if (data == null)
                return string.Empty;    

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
                    value = obj?.ToString() ?? "";

                param.Add(field.Name, value);
            }

            var qs = UriHelper.BuildQueryString(param);

            return qs;
        }
    }

    public class ProxyRestClientService<T>: IDataService<T>
        where T : class, new()
    {
        protected readonly HttpClient _http;

        protected readonly string _pathService;

        public ProxyRestClientService(HttpClient http, string pathService)
        {
            _http = http;
            _pathService = pathService;
        }

        public async Task<T?> GetFirstAsync()
        {

            T? response = await _http.GetFromJsonAsync<T?>($"{_pathService}");

            return response;
        }

        public async Task<APIResponseMessage<T>> PostAsync(T item)
        {
            APIResponseMessage<T> data = new APIResponseMessage<T>();
            try
            {
                HttpResponseMessage resp;
                Type myType = typeof(T);

                var id = UtilityHelper.GetPropertyValue<object>(item, "Id");

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
    }
}
