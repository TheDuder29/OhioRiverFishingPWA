using System.Net.Http.Json;

namespace OhioRiverFishingPWA.Services
{
    public class ExternalApiProxyService
    {
        private readonly HttpClient _httpClient;

        public ExternalApiProxyService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Fetches water data from the external API through a proxy
        /// </summary>
        /// <param name="url">The URL to fetch data from</param>
        /// <returns>Response content as string</returns>
        public async Task<string> GetExternalDataAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch data from {url}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Fetches water data from the external API through a proxy and deserializes to T
        /// </summary>
        /// <typeparam name="T">Type to deserialize response to</typeparam>
        /// <param name="url">The URL to fetch data from</param>
        /// <returns>Deserialized object of type T</returns>
        public async Task<T> GetExternalDataAsync<T>(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch and deserialize data from {url}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Posts data to an external API through a proxy
        /// </summary>
        /// <param name="url">The URL to post to</param>
        /// <param name="content">Content to send</param>
        /// <returns>Response content as string</returns>
        public async Task<string> PostExternalDataAsync(string url, HttpContent content)
        {
            try
            {
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to post data to {url}: {ex.Message}", ex);
            }
        }
    }
}