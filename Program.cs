using Google.Ads.GoogleAds;
using Google.Ads.GoogleAds.Config;
using Google.Ads.GoogleAds.Lib;
using Google.Ads.GoogleAds.V11.Errors;
using Google.Ads.GoogleAds.V11.Resources;
using Google.Ads.GoogleAds.V11.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;


using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenerateHistoricalMetrics
{
    internal class Program
    {
        private const string GOOGLE_ADS_API_SCOPE = "https://www.googleapis.com/auth/adwords";

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("GenerateHistoricalMetrics clientIdNumber clientPlanString");
                return;
            }
            var clientId = long.Parse(args[0]);
            var clientPlan = args[1];

            Google.Ads.GoogleAds.Util.TraceUtilities.Configure(Google.Ads.GoogleAds.Util.TraceUtilities.DETAILED_REQUEST_LOGS_SOURCE,
                                     Path.Combine(Path.GetTempPath(), $"GenerateHistoricalMetrics_{clientPlan.Replace("/","_")}.log"),
                                     SourceLevels.All);

            var credCFG = (from file in Directory.GetFiles(@"C:\users\bugma\Credentials", "*.cfg") where file.Contains("trev") select file).FirstOrDefault();
            var credJSN = Path.ChangeExtension(credCFG, ".json");
            var devToken = (from line in File.ReadAllLines(credCFG) where line.StartsWith("developer.token") select line.Substring(16)).FirstOrDefault();
            var (adsClient, credential) = AuthoriseFromCFG(credCFG, "7212153394");
            CustomerServiceClient customerService = adsClient.GetService(Services.V11.CustomerService);

            var (customer, exc1) = GetAccountInformation(adsClient, clientId);
            if (exc1 == null)
            {
                Console.WriteLine(clientPlan);
                var (metrics, exc3) = GenerateHistoricalMetrics(adsClient, clientId, clientPlan);
                if (exc3 == null)
                    Console.WriteLine(metrics);
            }
        }


        private static (GenerateHistoricalMetricsResponse response, GoogleAdsException exception) GenerateHistoricalMetrics(GoogleAdsClient client, long customerId, string plan)
        {
            KeywordPlanServiceClient kpServiceClient = client.GetService(Services.V11.KeywordPlanService);

            try
            {
                var response = kpServiceClient.GenerateHistoricalMetrics(plan);
                return (response, null);
            }
            catch (GoogleAdsException e)
            {
                return (null, e);
            }
        }

        private static (GoogleAdsClient adsClient, UserCredential credential) AuthoriseFromCFG(string cfgFile, string loginCustomerId, string scopes = GOOGLE_ADS_API_SCOPE, bool debug = false)
        {
            if (debug) Debugger.Launch();

            var cfgDict = new Dictionary<string, string>();
            foreach (var keyValue in from keyValue in
                                         from line in File.ReadAllLines(cfgFile) select line.Split('=')
                                     where !keyValue[0].StartsWith("#")
                                     select keyValue)
            {
                cfgDict[keyValue[0].Trim()] = keyValue[1].Trim();
            }

            dynamic jsonObj = JsonConvert.DeserializeObject(File.ReadAllText(Path.ChangeExtension(cfgFile, "json")));

            // Load the JSON secrets.
            ClientSecrets secrets = new ClientSecrets()
            {
                ClientId = (string)jsonObj.installed.client_id.Value,
                ClientSecret = (string)jsonObj.installed.client_secret,

            };

            // Authorize the user using desktop application flow.
            Task<UserCredential> task = GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                scopes.Split(','),
                "user",
                CancellationToken.None,
                new FileDataStore("AdsAuth-" + Path.GetFileNameWithoutExtension(cfgFile), false)
            );
            UserCredential credential = task.Result;

            // Store this token for future use.

            // To make a call, set the refreshtoken to the config, and
            // create the GoogleAdsClient.
            GoogleAdsClient client = new GoogleAdsClient(new GoogleAdsConfig
            {
                OAuth2RefreshToken = credential.Token.RefreshToken,
                DeveloperToken = cfgDict["developer.token"],
                LoginCustomerId = loginCustomerId,
                OAuth2ClientId = (string)jsonObj.installed.client_id.Value,
                OAuth2ClientSecret = (string)jsonObj.installed.client_secret
            });
            // var cfgdata = client.Config;
            // Now use the client to create services and make API calls.
            // ...
            return (client, credential);
        }

        private static (Customer customer, GoogleAdsException exception) GetAccountInformation(GoogleAdsClient client, long customerId)
        {
            // Get the GoogleAdsService.
            GoogleAdsServiceClient googleAdsService = client.GetService(
                Services.V11.GoogleAdsService);

            // Construct a query to retrieve the customer.
            // Add a limit of 1 row to clarify that selecting from the customer resource
            // will always return only one row, which will be for the customer
            // ID specified in the request.
            string query = "SELECT customer.id, customer.descriptive_name, " +
                "customer.currency_code, customer.time_zone, customer.tracking_url_template, " +
                "customer.auto_tagging_enabled, customer.status FROM customer LIMIT 1";

            // Executes the query and gets the Customer object from the single row of the response.
            SearchGoogleAdsRequest request = new SearchGoogleAdsRequest()
            {
                CustomerId = customerId.ToString(),
                Query = query
            };

            try
            {
                // Issue the search request.
                Customer customer = googleAdsService.Search(request).First().Customer;

                // Print account information.
                return (customer, null);
            }
            catch (GoogleAdsException e)
            {
                return (null, e);
            }
        }
    }

}