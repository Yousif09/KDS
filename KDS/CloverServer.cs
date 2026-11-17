using System.Net.Http.Headers;
using System.Net.Http.Json;
using KDS.Models;

public class CloverServer
{
    private readonly HttpClient _httpClient;
    private readonly string _merchantId;
    private readonly string _apiToken; // STORE THIS SOMEWHERE SAFE!!

    public CloverServer(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        // the following are stored in the appsettings.json:
        _merchantId = config["Clover:MerchantId"] ?? "";
        _apiToken = config["Clover:ApiToken"] ?? "";

        // base url where we get all the information.
        _httpClient.BaseAddress = new Uri("https://api.clover.com");
    }

    public string MerchantId => _merchantId;

    // no merchant id / token = demo mode with test orders
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_merchantId) && !string.IsNullOrWhiteSpace(_apiToken);

    // fetching the most recently placed orders: (async method)
    public async Task<List<OrderCard>> GetRecentOrders(CancellationToken cancel = default)
    {
        // get the most recent orders, with their items, modifiers, type and customer
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"v3/merchants/{_merchantId}/orders?orderBy=createdTime+DESC&limit=15&expand=lineItems,lineItems.modifications,orderType,customers");

        // authorizing it now with the bearer token
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

        var response = await _httpClient.SendAsync(request, cancel);
        response.EnsureSuccessStatusCode();

        var cloverData = await response.Content.ReadFromJsonAsync<CloverResponse>(cancel);
        var orderCards = new List<OrderCard>();

        if (cloverData?.Elements == null)
        {
            return orderCards;
        }

        foreach (var cloverOrder in cloverData.Elements)
        {
            orderCards.Add(ToOrderCard(cloverOrder));
        }
        return orderCards;
    }

    // creating a new card for each order
    public static OrderCard ToOrderCard(CloverOrder cloverOrder)
    {
        var id = cloverOrder.id ?? Guid.NewGuid().ToString();
        var card = new OrderCard
        {
            id = id,
            orderNumber = !string.IsNullOrWhiteSpace(cloverOrder.title) ? cloverOrder.title : id[^Math.Min(5, id.Length)..].ToUpper(),
            time = DateTimeOffset.FromUnixTimeMilliseconds(cloverOrder.createdTime),
            service = cloverOrder.orderType?.label ?? "Dine in",
            type = OrderType.From(cloverOrder.orderType?.label),
            paid = cloverOrder.paymentState == "PAID",
            customer = CustomerName(cloverOrder),
            note = cloverOrder.note,
            orderStatus = OrderStatus.inProgress,
            items = new List<OrderItem>()
        };

        if (cloverOrder.lineItems?.Elements != null)
        {
            foreach (var lineItem in cloverOrder.lineItems.Elements)
            {
                int qty = 1;
                // meaning we have an actual item to display:
                if (lineItem.unitQty != null && lineItem.unitQty > 0)
                {
                    qty = Math.Max(1, lineItem.unitQty.Value / 1000);
                }

                var newItem = new OrderItem
                {
                    name = lineItem.name,
                    quantity = qty,
                    notes = string.IsNullOrWhiteSpace(lineItem.note) ? null : lineItem.note
                };

                if (lineItem.modifications?.Elements != null)
                {
                    foreach (var mod in lineItem.modifications.Elements)
                    {
                        if (!string.IsNullOrWhiteSpace(mod.name)) newItem.modifiers.Add(mod.name);
                    }
                }

                //checking for a duplicate (clover sends one line item per unit):
                var existingItem = card.items.FirstOrDefault(i => i.GroupKey == newItem.GroupKey);

                if (existingItem != null)
                {
                    existingItem.quantity += qty;
                }
                else
                {
                    card.items.Add(newItem);
                }
            }
        }
        return card;
    }

    // "Sara K."
    private static string? CustomerName(CloverOrder order)
    {
        var c = order.customers?.Elements.FirstOrDefault();
        if (c == null) return null;
        var last = string.IsNullOrWhiteSpace(c.lastName) ? "" : $" {c.lastName[0]}.";
        var name = $"{c.firstName}{last}".Trim();
        return name.Length > 0 ? name : null;
    }
}
