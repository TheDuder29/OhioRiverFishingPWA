using OhioRiverFishingPWA.Models;

namespace OhioRiverFishingPWA.Services
{
    public class FishingCalculators
    {
        public double CalculateSolunarRating(DateTime startTime, DateTime endTime)
        {
            // Simple calculation for demo purposes - in a real app this would be more complex
            var now = DateTime.UtcNow;
            var duration = (endTime - startTime).TotalHours;
            
            // Calculate how close we are to the window (0 = not in window, 1 = exactly at start)
            double progress = Math.Max(0, Math.Min(1, (now - startTime).TotalHours / duration));
            
            // Return a score between 1-100
            return Math.Max(1, Math.Min(100, 50 + 50 * (1 - progress)));
        }

        public string GetStageTrendDescription(string stageTrend)
        {
            if (stageTrend == null) 
                return "Unknown stage trend.";
                
            switch (stageTrend.ToLower())
            {
                case "rising":
                    return "Water level is rising, indicating increased flow and potential fishing opportunities.";
                case "falling":
                    return "Water level is falling, which may affect fishing conditions in certain areas.";
                case "steady":
                    return "Water level is stable, providing consistent fishing conditions.";
                default:
                    return "Unknown stage trend. Water conditions are uncertain.";
            }
        }
    }
}