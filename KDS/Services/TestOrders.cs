using KDS.Models;

namespace KDS.Services;

// fake orders in clover's format for the "+ Test order" button and demo mode
// menu from tajalirestaurant.com, modifiers/notes are made up
public class TestOrders
{
    private static readonly string[] Sauces = { "Garlic Sauce", "Tzatziki Sauce", "Extra Garlic Sauce", "Hot Sauce" };

    // weight = how often it shows up
    private static readonly (string name, int weight, string[]? mods)[] Menu =
    {
        // Plates
        ("Lamb Tikka", 3, Sauces),
        ("Chicken Tikka", 3, Sauces),
        ("Falafel Plate", 2, new[] { "Tahini", "Hummus Side" }),
        ("Chicken Kebab Plate", 3, Sauces),
        ("Beef Kebab Plate", 3, Sauces),
        ("Gyro Plate", 3, Sauces),
        ("Beef Shawarma Plate (Over Rice)", 2, Sauces),
        ("Chicken Shawarma Plate (Over Rice)", 3, Sauces),
        // Sandwiches
        ("#19 Lamb Tikka Sandwich", 2, Sauces),
        ("#12 Chicken Kebab Sandwich", 3, Sauces),
        ("Cheeseburger & Fries", 1, null),
        ("Chicken Tikka Sandwich", 3, Sauces),
        ("Chicken Shawarma Sandwich", 3, Sauces),
        ("Beef Shawarma Sandwich", 2, Sauces),
        ("Gyro Sandwich", 2, Sauces),
        ("Falafel Sandwich", 3, new[] { "Tahini", "Hot Sauce" }),
        // Specials
        ("Spicy Curry Plate", 1, null),
        ("#30 Spicy Chicken Biryani", 2, null),
        ("Dolma Plate (8 PC)", 1, null),
        ("Kuba Plate", 1, null),
        ("Borek Plate", 1, null),
        ("Taj Ali Combo (2 People)", 1, null),
        ("Kebab Over Rice", 2, null),
        ("Lamb Shank Over Rice", 1, null),
        ("Shawarma SAJ Plate", 1, null),
        // Sides & soup
        ("#46 Pita Bread (2 PC)", 2, null),
        ("#44 Falafels (4 PC)", 1, null),
        ("#39 Fattoush Salad", 1, null),
        ("#37 Taboula Salad", 1, null),
        ("Hummus", 2, null),
        ("Baba Ghanoush", 1, null),
        ("Fries", 2, null),
        ("Rice", 1, null),
        ("Lentil Soup", 2, null),
        // Desserts
        ("Big Baklava", 1, null),
        ("Kunafa Dessert", 1, null),
    };

    private static readonly string[] Notes =
    {
        "No onions", "No tomatoes", "Extra pickles", "Sauce on the side",
        "Well done", "Extra spicy", "No lettuce", "ALLERGY: sesame", "Cut in half",
    };

    // example clover order type labels
    private static readonly (string label, int weight)[] Types =
    {
        ("Dine In", 3), ("To Go", 3), ("Pickup", 3), ("Delivery", 1),
    };

    private static readonly string[] FirstNames = { "Sara", "Omar", "Mike", "Layla", "Jess", "Ahmed", "Chris", "Noor", "Dan", "Hana" };
    private const string LastInitials = "ABCDHKMRST";
    private const string IdChars = "ABCDEFGHJKLMNPQRSTUVWXYZ0123456789";

    private readonly Random _random = new();
    private int _nextNumber = 1043;

    public OrderCard Make(DateTimeOffset? createdAt = null)
    {
        var type = PickWeighted(Types, t => t.weight);
        var hasCustomer = type.label != "Dine In" || _random.NextDouble() < 0.2;

        var order = new CloverOrder
        {
            id = RandomId(),
            title = (_nextNumber++).ToString(),
            createdTime = (createdAt ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds(),
            paymentState = _random.NextDouble() < 0.85 ? "PAID" : "OPEN",
            orderType = new CloverOrderType { label = type.label },
            note = type.label == "Pickup" && _random.NextDouble() < 0.25 ? "Customer waiting in car" : null,
            customers = new CloverCustomers(),
            lineItems = new CloverLineItems(),
        };

        if (hasCustomer)
        {
            order.customers.Elements.Add(new CloverCustomer
            {
                firstName = Pick(FirstNames),
                lastName = LastInitials[_random.Next(LastInitials.Length)].ToString(),
            });
        }

        var lineCount = _random.Next(1, 7);
        for (int i = 0; i < lineCount; i++)
        {
            order.lineItems.Elements.AddRange(MakeLineItems());
        }

        return CloverServer.ToOrderCard(order);
    }

    // one line item per unit (like clover)
    private IEnumerable<CloverLineItem> MakeLineItems()
    {
        var dish = PickWeighted(Menu, d => d.weight);
        var qty = _random.NextDouble() < 0.8 ? 1 : 2;
        var note = _random.NextDouble() < 0.3 ? Pick(Notes) : null;
        var mod = dish.mods != null && _random.NextDouble() < 0.5 ? Pick(dish.mods) : null;

        for (int i = 0; i < qty; i++)
        {
            var item = new CloverLineItem { name = dish.name, note = note, modifications = new CloverModifications() };
            if (mod != null) item.modifications.Elements.Add(new CloverModification { name = mod });
            yield return item;
        }
    }

    private T Pick<T>(IReadOnlyList<T> list) => list[_random.Next(list.Count)];

    private T PickWeighted<T>(IReadOnlyList<T> list, Func<T, int> weight)
    {
        var r = _random.NextDouble() * list.Sum(weight);
        foreach (var x in list)
        {
            r -= weight(x);
            if (r <= 0) return x;
        }
        return list[^1];
    }

    private string RandomId() => new string(Enumerable.Range(0, 13).Select(_ => IdChars[_random.Next(IdChars.Length)]).ToArray());
}
