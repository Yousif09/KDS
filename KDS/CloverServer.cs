using System.Net.Http.Headers;
using System.Net.Http.Json;
using KDS.Components.Pages; 

public class CloverServer
{
    private readonly HttpClient _httpClient;
    private readonly string _merchantId;
    private readonly string _apiToken; // STORE THIS SOMEWHERE SAFE!!

    public CloverServer(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        // the following are stored in the appsettings.json:
        _merchantId = config["Clover:MerchantId"]!;
        _apiToken = config["Clover:ApiToken"]!;

        // base url where we get all the information. 
        _httpClient.BaseAddress = new Uri("https://apisandbox.dev.clover.com/");
    }

    // fetching the most recently placed order: (async method)
    public async Task<Home.OrderCard?> GetLatestOrder()
    {
        // get the most recent order
        var request = new HttpRequestMessage(HttpMethod.Get, $"v3/merchants/{_merchantId}/orders?orderBy=createdTime+DESC&limit=1&expand=lineItems,payments");

        // authorizing it now with the bearer token
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var cloverData = await response.Content.ReadFromJsonAsync<Home.CloverResponse>();
        var latestOrder = cloverData?.Elements.FirstOrDefault();
        if (latestOrder == null)
        {
            return null;
        }

        var orderCard = new Home.OrderCard
        {
            time = DateTimeOffset.FromUnixTimeMilliseconds(latestOrder.createdTime),
            service = latestOrder.orderType?.label ?? "Dine in",
            orderStatus = Home.OrderStatus.inProgress,
            items = new List<Home.OrderItem>()
        };

        if(latestOrder.lineItems?.Elements != null)
        {
            foreach(var lineItem in latestOrder.lineItems.Elements)
            {
                int qty = 1;
                // meaning we have an actual item to display:
                if (lineItem.unitQty != null && lineItem.unitQty > 0)
                {
                    qty = lineItem.unitQty.Value / 1000; 
                }

                //checking for a duplicate:
                var existingItem = orderCard.items.FirstOrDefault(i =>
                i.name == lineItem.name && i.modifiers.FirstOrDefault() == lineItem.note);


                if(existingItem != null)
                {
                    existingItem.quantity += qty; 
                } else
                {
                    var newItem = new Home.OrderItem
                    {
                        name = lineItem.name,
                        quantity = qty
                    }; 

                    if(!string.IsNullOrWhiteSpace(lineItem.note))
                    {
                        newItem.modifiers.Add(lineItem.note);
                    }
                    orderCard.items.Add(newItem); 
                }
            }
        }
        return orderCard;
    }

}