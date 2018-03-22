#r "Newtonsoft.Json"

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// This scenario listens to an event grid event and looks for app settings from a Web App
public static async Task<string> Run(JObject EventGridTrigger, IAsyncCollector<string> outputDocument, TraceWriter log)
{
    log.Info("in");
    var eventType = EventGridTrigger["eventType"].ToString();
    if (eventType!="Microsoft.Resources.ResourceWriteSuccess") 
        return eventType; 

    string[] eventData = EventGridTrigger["subject"].ToString().Split('/'); 
    string token = await GetToken("https://management.azure.com/", "2017-09-01");
    string props = await GetProperties(eventData[2],eventData[4],eventData[8],"2016-08-01",token);
    string outData = MakeEventRecord(eventData[4],eventData[8],props,Guid.NewGuid().ToString(), log);
    await outputDocument.AddAsync(outData); 
    return outData;
}

// Interacting with MSI to get a token for ARM
public static HttpClient tokenClient = InitializeTokenClient();
public static async Task<string> GetToken(string resource, string apiversion)  {
    string endpoint = String.Format("?resource={0}&api-version={1}", resource, apiversion);
    JObject tokenServiceResponse = JsonConvert.DeserializeObject<JObject>(await tokenClient.GetStringAsync(endpoint));
    return tokenServiceResponse["access_token"].ToString();
}


public static HttpClient InitializeTokenClient() {
    var client = new HttpClient() {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("MSI_ENDPOINT"))
    };
    client.DefaultRequestHeaders.Add("Secret", Environment.GetEnvironmentVariable("MSI_SECRET"));
    return client;
}

// Get App Settings for the app that changed
public static async Task<string> GetProperties(string sub, string resource_group, string app_name, string apiversion, string token)  {
    HttpClient ArmClient = new HttpClient();
    string endpoint = String.Format("https://management.azure.com/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Web/sites/{2}/config/appsettings/list?api-version={3}", 
        sub,
        resource_group,
        app_name,
        apiversion
    ); 
    ArmClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
    var result = ArmClient.PostAsync(endpoint,null).Result;
    return result.Content.ReadAsStringAsync().Result;
}

// create json to send to Cosmos DB 
public static string MakeEventRecord(string resource_group, string app_name, string props,string operationId,TraceWriter log)  
{
    string retString = @"{""Id"":""#ID"", ""app"":{""rg"":""#rg"",""app"":""#app""},""settings"":#json}";
    JObject record = JObject.Parse(props);
    JToken propToken = record.SelectToken("properties");
    string propsStr = propToken.ToString(Formatting.None); 
    retString=retString.Replace("#ID",operationId);
    retString=retString.Replace("#rg",resource_group);
    retString=retString.Replace("#app",app_name);
    retString=retString.Replace("#json",propsStr);
    log.Info(propsStr);
    return retString;
}

