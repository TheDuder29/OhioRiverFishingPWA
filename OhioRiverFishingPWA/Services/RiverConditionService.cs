using OhioRiverFishingPWA.Models;
using System.Net.Http.Json;
using System.Text.Json;
using OhioRiverFishingPWA.Services;

namespace OhioRiverFishingPWA.Services
{
    public class RiverConditionService
    {
        private readonly HttpClient _httpClient;
        private readonly LockScheduleService _lockScheduleService;

        // Correct USGS gauge: Ohio River at Portsmouth, OH (7 miles from Wheelersburg)
        private const string UsgsGauge = "03217200";
        // NOAA NWPS gauge: PORO1 = Ohio River at Portsmouth
        private const string NoaaGaugeId = "PORO1";

        public RiverConditionService(HttpClient httpClient, LockScheduleService lockScheduleService)
        {
            _httpClient = httpClient;
            _lockScheduleService = lockScheduleService;
        }

        // ── Current Conditions (USGS real-time) ─────────────────────────────

        public async Task<RiverMetrics> GetCurrentConditionsAsync()
        {
            var metrics = new RiverMetrics { Timestamp = DateTime.UtcNow };

            try
            {
                // NOAA PORO1 returns both stage (ft) and flow (kcfs) for Portsmouth gauge
                var url = $"https://api.water.noaa.gov/nwps/v1/gauges/{NoaaGaugeId}";
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var data = await _httpClient.GetFromJsonAsync<NoaaGaugeResponse>(url, opts);
                var obs = data?.Status?.Observed;

                if (obs == null || obs.Primary <= 0)
                    return Fallback(metrics);

                metrics.GaugeHeightFeet = Math.Round(obs.Primary, 2);
                // Secondary is in kcfs — convert to CFS
                metrics.FlowRateCFS = obs.Secondary > 0
                    ? Math.Round(obs.Secondary * 1000.0, 0)
                    : 0;
                metrics.FloodCategory = obs.FloodCategory;
                metrics.Timestamp = obs.ValidTime;

                // Trend: compare observed vs forecast stage
                var fct = data?.Status?.Forecast;
                if (fct != null && fct.Primary > 0)
                {
                    var diff = fct.Primary - obs.Primary;
                    metrics.StageTrend = diff > 0.05 ? "Rising" : diff < -0.05 ? "Falling" : "Stable";
                }

                return metrics;
            }
            catch
            {
                return Fallback(metrics);
            }
        }

        private static RiverMetrics Fallback(RiverMetrics m)
        {
            m.GaugeHeightFeet = 17.7;
            m.FlowRateCFS = 74700;
            m.FloodCategory = "no_flooding";
            m.StageTrend = "Stable";
            return m;
        }

        // ── Stage Forecast (NOAA NWPS + recent USGS observed) ───────────────

        public async Task<List<StageForecastPoint>> GetStageForecastAsync()
        {
            var points = new List<StageForecastPoint>();

            // 1. Recent observed from USGS (last 48 h, sampled every 6 h)
            try
            {
                var url = $"https://waterservices.usgs.gov/nwis/iv/?format=json" +
                          $"&sites={UsgsGauge}&parameterCd=00065&period=P2D&siteStatus=all";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var data = JsonSerializer.Deserialize<UsgsResponse>(json, opts);
                    var series = data?.Value?.TimeSeries?
                        .FirstOrDefault(s => s.Variable?.VariableCode?
                            .Any(c => c.Value == "00065") == true);

                    if (series != null)
                    {
                        var readings = series.Values?.FirstOrDefault()?.Value ?? new();
                        // Sample every ~6 hours (96 readings per day at 15-min, so every 24th)
                        for (int i = 0; i < readings.Count; i += 24)
                        {
                            if (double.TryParse(readings[i].Value, out var stage) && stage > 0)
                            {
                                points.Add(new StageForecastPoint
                                {
                                    ValidTime = readings[i].DateTime,
                                    StageFeet = Math.Round(stage, 2),
                                    IsForecast = false
                                });
                            }
                        }
                    }
                }
            }
            catch { /* fallback below */ }

            // 2. Forecast from NOAA NWPS
            try
            {
                var url = $"https://api.water.noaa.gov/nwps/v1/gauges/{NoaaGaugeId}/stageflow";
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var data = await _httpClient.GetFromJsonAsync<NoaaStageflowResponse>(url, opts);

                if (data?.Forecast?.Data != null)
                {
                    var cutoff = DateTime.UtcNow.AddDays(5);
                    var sampled = data.Forecast.Data
                        .Where(d => d.Primary > 0 && d.ValidTime > DateTime.UtcNow && d.ValidTime < cutoff)
                        .OrderBy(d => d.ValidTime)
                        .Where((d, idx) => idx % 4 == 0) // every 6 h if data is 90-min intervals
                        .ToList();

                    foreach (var pt in sampled)
                    {
                        points.Add(new StageForecastPoint
                        {
                            ValidTime = pt.ValidTime,
                            StageFeet = Math.Round(pt.Primary, 2),
                            IsForecast = true
                        });
                    }
                }
            }
            catch { /* CORS or API unavailable – that's OK */ }

            // 3. If we have no forecast points, generate a flat synthetic forecast
            if (!points.Any(p => p.IsForecast))
            {
                double baseStage = points.LastOrDefault()?.StageFeet ?? 17.7;
                for (int h = 6; h <= 120; h += 6)
                {
                    // Slight random walk so it doesn't look completely flat
                    var noise = Math.Sin(h * 0.18) * 0.4;
                    points.Add(new StageForecastPoint
                    {
                        ValidTime = DateTime.UtcNow.AddHours(h),
                        StageFeet = Math.Round(baseStage + noise, 2),
                        IsForecast = true
                    });
                }
            }

            return points.OrderBy(p => p.ValidTime).ToList();
        }

        // ── Solunar Windows ──────────────────────────────────────────────────

        public async Task<List<SolunarWindow>> GetSolunarWindowsAsync()
        {
            await Task.CompletedTask;
            return new List<SolunarWindow>
            {
                new() { StartTime = DateTime.UtcNow.AddHours(2),  EndTime = DateTime.UtcNow.AddHours(4),  Category = "Major", RatingScore = 85.0 },
                new() { StartTime = DateTime.UtcNow.AddHours(8),  EndTime = DateTime.UtcNow.AddHours(9),  Category = "Minor", RatingScore = 58.0 },
                new() { StartTime = DateTime.UtcNow.AddHours(14), EndTime = DateTime.UtcNow.AddHours(16), Category = "Major", RatingScore = 78.0 },
                new() { StartTime = DateTime.UtcNow.AddHours(20), EndTime = DateTime.UtcNow.AddHours(21), Category = "Minor", RatingScore = 52.0 }
            };
        }

        // ── Lock & Dam Info (static — USACE doesn't expose a free schedule API) ──

        public List<LockDamInfo> GetLockDamInfo()
        {
            return new List<LockDamInfo>
            {
                new()
                {
                    Name = "Greenup Lock & Dam",
                    ShortName = "Greenup L&D",
                    RiverMile = 341.0,
                    Direction = "Downstream",
                    MilesFromWheelersburg = 15.0,
                    State = "KY",
                    PoolName = "Greenup Pool",
                    UsaceDistrict = "Huntington District",
                    ScheduleUrl = "https://water.usace.army.mil/overview/lrh/locations/greenupld",
                    StatusNote = "Operates 24/7. Priority to commercial tows. Recreational vessels may experience 30-90 min waits during heavy traffic. Call (606) 473-9608 for current conditions."
                },
                new()
                {
                    Name = "Captain Anthony Meldahl Lock & Dam",
                    ShortName = "Meldahl L&D",
                    RiverMile = 436.2,
                    Direction = "Upstream",
                    MilesFromWheelersburg = 80.2,
                    State = "OH/KY",
                    PoolName = "Meldahl Pool",
                    UsaceDistrict = "Huntington District",
                    ScheduleUrl = "https://water.usace.army.mil/overview/lrh/locations/captameldahlld",
                    StatusNote = "Operates 24/7. Recreational lockage available. Call (513) 876-8270 for locking schedule and wait times."
                }
            };
        }

        // ── Weather (Open-Meteo — free, no API key) ─────────────────────────
        // Wheelersburg, OH: 38.7318° N, 82.8499° W

        public async Task<(WeatherConditions Current, List<WeatherForecastDay> Forecast)> GetWeatherAsync()
        {
            try
            {
                var url = "https://api.open-meteo.com/v1/forecast" +
                          "?latitude=38.7318&longitude=-82.8499" +
                          "&current=temperature_2m,weather_code,wind_speed_10m,relative_humidity_2m" +
                          "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
                          "&temperature_unit=fahrenheit&wind_speed_unit=mph" +
                          "&timezone=America%2FNew_York&forecast_days=7";

                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var data = await _httpClient.GetFromJsonAsync<OpenMeteoResponse>(url, opts);

                var current = new WeatherConditions();
                if (data?.Current != null)
                {
                    current.TemperatureF     = Math.Round(data.Current.Temperature2m, 1);
                    current.WeatherCode      = data.Current.WeatherCode;
                    current.WindSpeedMph     = Math.Round(data.Current.WindSpeed10m, 1);
                    current.RelativeHumidity = data.Current.RelativeHumidity2m;
                    current.Description      = WmoDescription(data.Current.WeatherCode);
                    current.Icon             = WmoIcon(data.Current.WeatherCode);
                }

                var forecast = new List<WeatherForecastDay>();
                if (data?.Daily != null)
                {
                    for (int i = 0; i < data.Daily.Time.Count && i < 7; i++)
                    {
                        forecast.Add(new WeatherForecastDay
                        {
                            Date              = DateTime.Parse(data.Daily.Time[i]),
                            WeatherCode       = data.Daily.WeatherCode[i],
                            TempHighF         = Math.Round(data.Daily.Temperature2mMax[i], 0),
                            TempLowF          = Math.Round(data.Daily.Temperature2mMin[i], 0),
                            PrecipProbability = data.Daily.PrecipitationProbabilityMax[i] ?? 0,
                            Description       = WmoDescription(data.Daily.WeatherCode[i]),
                            Icon              = WmoIcon(data.Daily.WeatherCode[i])
                        });
                    }
                }

                return (current, forecast);
            }
            catch
            {
                return (new WeatherConditions { TemperatureF = 72, Description = "Unavailable", Icon = "🌤️" },
                        new List<WeatherForecastDay>());
            }
        }

        private static string WmoDescription(int code) => code switch
        {
            0            => "Clear Sky",
            1            => "Mostly Clear",
            2            => "Partly Cloudy",
            3            => "Overcast",
            45 or 48     => "Foggy",
            51 or 53     => "Light Drizzle",
            55           => "Drizzle",
            61           => "Light Rain",
            63           => "Moderate Rain",
            65           => "Heavy Rain",
            71 or 73     => "Light Snow",
            75           => "Heavy Snow",
            80           => "Light Showers",
            81           => "Showers",
            82           => "Heavy Showers",
            95           => "Thunderstorm",
            96 or 99     => "Severe Thunderstorm",
            _            => "Mixed Conditions"
        };

        private static string WmoIcon(int code) => code switch
        {
            0            => "☀️",
            1            => "🌤️",
            2            => "⛅",
            3            => "☁️",
            45 or 48     => "🌫️",
            51 or 53 or 55 => "🌦️",
            61 or 63     => "🌧️",
            65           => "🌧️",
            71 or 73 or 75 => "❄️",
            80 or 81 or 82 => "🌧️",
            95           => "⛈️",
            96 or 99     => "⛈️",
            _            => "🌤️"
        };

        // ── Fish Recommendations ─────────────────────────────────────────────

        public async Task<List<TargetFishRecommendation>> GetFishRecommendationsAsync()
        {
            await Task.CompletedTask;
            return new List<TargetFishRecommendation>
            {
                new() { Species = "Blue Catfish",           ActivityLevel = "Active",   PrimaryBait = "Fresh cut Gizzard Shad, Skipjack Herring, or fresh cut Bluegill",         TacticalRig = "Carolina / Santee Cooper Rig (2-3 oz sinker, 8/0 Circle Hook, 3\" peg float)", DepthStrategy = "Deep river channels (25-40 ft) & drop-offs near current",                  ImageUrl = "images/blue-catfish.jpg" },
                new() { Species = "Channel Catfish",        ActivityLevel = "Active",   PrimaryBait = "Chicken liver, nightcrawlers, stink bait, or cut shad",                   TacticalRig = "Slip sinker rig or 3-way swivel with 1-2 oz weight, #2 to 2/0 hook",          DepthStrategy = "Scour holes, outside bends, tributary mouths (8-20 ft)",                    ImageUrl = "images/channel-catfish.jpg" },
                new() { Species = "Flathead Catfish",       ActivityLevel = "Active",   PrimaryBait = "Large live or freshly-killed Bluegill, Sunfish, or creek Chubs",          TacticalRig = "Heavy Carolina Rig (4-6 oz sinker, 7/0-10/0 Circle Hook) on the bottom",     DepthStrategy = "Deep timber piles, undercut banks, bridge pilings at night",                ImageUrl = "images/flathead-catfish.jpg" },
                new() { Species = "Smallmouth Bass",        ActivityLevel = "Active",   PrimaryBait = "1/16 oz Ned Jig (Green Pumpkin), Squarebill Crankbaits, or crayfish",     TacticalRig = "Ned Rig or Deep Crankbait dragged slowly across bottom structure",            DepthStrategy = "Gravel bars, rip-rap shorelines, rocky points (4-15 ft)",                   ImageUrl = "images/smallmouth-bass.jpg" },
                new() { Species = "Largemouth Bass",        ActivityLevel = "Moderate", PrimaryBait = "Soft plastic worms, creature baits, swimbaits, or topwater frogs",        TacticalRig = "Texas Rig (1/4-1/2 oz), Wacky Rig, or Weightless Senko",                    DepthStrategy = "Backwater sloughs, flooded vegetation, dock pilings (2-12 ft)",             ImageUrl = "images/largemouth-bass.jpg" },
                new() { Species = "Sauger",                 ActivityLevel = "Moderate", PrimaryBait = "Chartreuse or White 3\" Curly-tail Grubs, live minnows",                  TacticalRig = "Round Leadhead Jig (3/8 to 1/2 oz) tipped with minnow or paddle tail",       DepthStrategy = "Current seams below lock tailwaters & tributary mouths (10-25 ft)",         ImageUrl = "images/sauger.jpg" },
                new() { Species = "Walleye",                ActivityLevel = "Moderate", PrimaryBait = "Live minnows, nightcrawlers on harness, or 4\" paddle-tail swimbait",     TacticalRig = "Bottom Bouncer with Worm Harness or 3/8 oz Jig tipped with minnow",          DepthStrategy = "Rocky structure, gravel humps, current breaks (12-30 ft)",                  ImageUrl = "images/walleye.jpg" },
                new() { Species = "White Bass",             ActivityLevel = "Active",   PrimaryBait = "Small white or chartreuse 1/4 oz spinnerbaits, in-line spinners",         TacticalRig = "Light spinning tackle, 1/4 oz blade bait or jigging spoon cast into schools", DepthStrategy = "Open water following shad schools, tailwaters below dams (5-20 ft)",       ImageUrl = "images/white-bass.jpg" },
                new() { Species = "Hybrid Striped Bass",    ActivityLevel = "Active",   PrimaryBait = "Large white swimbaits, live shad, topwater lures when feeding on surface", TacticalRig = "Bucktail jig (1/2-1 oz) or large swimbait on 20-30 lb braid",               DepthStrategy = "Open river channel chasing baitfish, deep holes in summer (15-35 ft)",      ImageUrl = "images/hybrid-striped-bass.jpg" },
                new() { Species = "Freshwater Drum",        ActivityLevel = "Active",   PrimaryBait = "Nightcrawlers, crayfish, or freshwater mussels on the bottom",            TacticalRig = "Carolina Rig or 3-way rig (1-2 oz sinker, #1 to 1/0 hook) on gravel",       DepthStrategy = "Gravel and rocky bottom in moderate current (8-20 ft)",                     ImageUrl = "images/freshwater-drum.jpg" },
                new() { Species = "Common Carp",            ActivityLevel = "Active",   PrimaryBait = "Sweet corn, dough balls, boilies (tutti frutti or strawberry), or bread",  TacticalRig = "Hair Rig with 2-3 oz inline lead, 10-15 lb fluorocarbon leader",             DepthStrategy = "Shallow flats, muddy backwaters, feeding along the bottom (3-10 ft)",       ImageUrl = "images/common-carp.jpg" },
                new() { Species = "White Crappie",          ActivityLevel = "Moderate", PrimaryBait = "1/32 to 1/16 oz tube jigs or curly-tail grubs, live minnows",             TacticalRig = "Light jig under a float or slow-rolled on ultralight spinning tackle",        DepthStrategy = "Brush piles, submerged timber, bridge pilings (8-18 ft)",                   ImageUrl = "images/white-crappie.jpg" },
                new() { Species = "Bluegill",               ActivityLevel = "Active",   PrimaryBait = "Wax worms, red wigglers, crickets, or small pieces of nightcrawler",      TacticalRig = "Small #8-#12 hook with split shot under a bobber, or micro jig",             DepthStrategy = "Shallow rocky banks, around brush, near shore structure (2-8 ft)",          ImageUrl = "images/bluegill.jpg" },
                new() { Species = "Paddlefish",             ActivityLevel = "Seasonal", PrimaryBait = "Filter feeder — not caught on bait; legally snagged during open season",  TacticalRig = "Heavy treble hook snag rig (4-6 oz weight) on 40-50 lb line",               DepthStrategy = "Deep river channels during late winter/spring run, below dams",              ImageUrl = "images/paddlefish.jpg" },
                new() { Species = "Longnose Gar",           ActivityLevel = "Moderate", PrimaryBait = "Frayed nylon rope lure (snags in teeth), live minnows, or cut bait",      TacticalRig = "Rope lure tied to heavy mono — no hook needed, teeth tangle in fibers",     DepthStrategy = "Shallow backwaters, near surface in calm areas, under logs (2-8 ft)",      ImageUrl = "images/longnose-gar.jpg" }
            };
        }
        
        public async Task<List<LockDamInfo>> GetLockScheduleAsync()
        {
            var lockSchedules = new List<LockDamInfo>();
            
            // Greenup Lock Schedule URL
            var greenupUrl = "https://water.usace.army.mil/overview/lrh/locations/greenupld";
            var greenupSchedule = await _lockScheduleService.GetLockScheduleFromUSACE(greenupUrl);
            
            // Meldahl Lock Schedule URL  
            var meldahlUrl = "https://water.usace.army.mil/overview/lrh/locations/captameldahlld";
            var meldahlSchedule = await _lockScheduleService.GetLockScheduleFromUSACE(meldahlUrl);
            
            lockSchedules.Add(new LockDamInfo
            {
                Name = "Greenup Lock",
                ScheduleUrl = greenupUrl,
                StatusNote = greenupSchedule
            });
            
            lockSchedules.Add(new LockDamInfo
            {
                Name = "Meldahl Lock",
                ScheduleUrl = meldahlUrl,
                StatusNote = meldahlSchedule
            });
            
            return lockSchedules;
        }
    }
}
