using System.Net.Http;
using HtmlAgilityPack;
using OhioRiverFishingPWA.Services;

namespace OhioRiverFishingPWA.Services
{
    public class LockScheduleService
    {
        private readonly ExternalApiProxyService _proxyService;
        private readonly HttpClient _httpClient;

        public LockScheduleService(ExternalApiProxyService proxyService, HttpClient httpClient)
        {
            _proxyService = proxyService;
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<string> GetLockScheduleFromUSACE(string url)
        {
            try
            {
                // Use the proxy service to fetch data instead of direct HttpClient call
                var response = await _proxyService.GetExternalDataAsync(url);
                
                // Parse HTML with HtmlAgilityPack
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(response);
                
                // Extract lock schedule information - improved for actual USACE page structure
                var scheduleInfo = "Schedule information not yet fully implemented. Please check the USACE website directly for current lock schedules.";
                
                // Try to extract specific elements from the page based on typical USACE page structures
                // Look for various potential containers that might contain schedule data
                var scheduleNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'lock-schedule') or contains(@class, 'schedule')]") ??
                                  doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'schedule') or contains(@class, 'lock')]") ??
                                  doc.DocumentNode.SelectSingleNode("//div[@id='lock-schedule']") ??
                                  doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'lock-info')]") ??
                                  doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'operational')]") ??
                                  doc.DocumentNode.SelectSingleNode("//div[@class='schedule-content']") ??
                                  doc.DocumentNode.SelectSingleNode("//table");
                
                if (scheduleNode != null)
                {
                    // Clean up the text content
                    var textContent = scheduleNode.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        // Filter out common non-schedule text
                        var lines = textContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(l => l.Trim())
                                              .Where(l => !string.IsNullOrWhiteSpace(l) && 
                                                         !l.Contains("©") && 
                                                         !l.Contains("USACE") && 
                                                         !l.Contains("Army") &&
                                                         !l.Contains("water.usace.army.mil"));
                        
                        scheduleInfo = string.Join("\n", lines.Take(10)); // Take first 10 meaningful lines
                    }
                }
                
                return scheduleInfo;
            }
            catch (Exception ex)
            {
                // Log error and return default message
                return $"Error fetching schedule: {ex.Message}";
            }
        }
    }
}