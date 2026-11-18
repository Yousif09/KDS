using System.Text.RegularExpressions;

namespace KDS.Models;

// represents a single order card that will pop up on the menu.
public class OrderCard
{
    public string id { get; set; } = string.Empty;
    public string orderNumber { get; set; } = string.Empty; // clover's order title, or the end of the id
    public DateTimeOffset time { get; set; }
    public DateTimeOffset addedAt { get; set; } = DateTimeOffset.UtcNow; // when it landed on the board
    public string? service { get; set; } // to go or dine in.. (clover's label)
    public OrderType type { get; set; } = OrderType.From(null);
    public bool paid { get; set; }
    public string? customer { get; set; }
    public string? note { get; set; }
    public OrderStatus orderStatus { get; set; }
    public List<OrderItem> items { get; set; } = new();  // a list of our order items

    public int DoneCount => items.Count(i => i.isComplete);
    public bool IsComplete => items.Count > 0 && DoneCount == items.Count;
}

// class that represents a singular order item and what it will compose of
public class OrderItem
{
    public int quantity { get; set; }
    public string? name { get; set; }
    public string? notes { get; set; } // the line item note: no tomatoes/onions .. etc
    public List<string> modifiers { get; set; } = new(); // clover modifiers: garlic sauce, extra rice..
    public bool isComplete { get; set; }

    // same name + note + modifiers = same row
    public string GroupKey => $"{name}|{notes}|{string.Join(",", modifiers)}";
}

// Enum to determine the status of an order
public enum OrderStatus
{
    inProgress,
    ready
}

public class HistoryEntry
{
    public OrderCard order { get; set; } = new();
    public DateTimeOffset bumpedAt { get; set; }
}

// order type labels are set per merchant in clover, so we match them loosely
public record OrderType(string Key, string Label)
{
    private static readonly (Regex match, string key, string label)[] Rules =
    {
        (new Regex(@"pick\s*-?\s*up|online", RegexOptions.IgnoreCase), "pickup", "Pickup"),
        (new Regex(@"to\s*-?\s*go|take\s*-?\s*out", RegexOptions.IgnoreCase), "to go", "to go"),
        (new Regex(@"deliver|doordash|uber|grubhub", RegexOptions.IgnoreCase), "delivery", "Delivery"),
        (new Regex(@"dine|for here|eat in", RegexOptions.IgnoreCase), "dinein", "Dine in"),
    };

    public static OrderType From(string? cloverLabel)
    {
        var raw = cloverLabel ?? "";
        foreach (var rule in Rules)
        {
            if (rule.match.IsMatch(raw)) return new OrderType(rule.key, rule.label);
        }
        // unknown: show the label as-is
        return new OrderType("other", string.IsNullOrWhiteSpace(raw) ? "No type" : raw);
    }
}






// clovers response (is then formulated into c# objects):
public class CloverResponse
{
    public List<CloverOrder> Elements { get; set; } = new();
}

public class CloverOrder
{
    public string? id { get; set; } // unique id to each order
    public string? title { get; set; }
    public string? note { get; set; }
    public string? paymentState { get; set; } // PAID, OPEN, PARTIALLY_PAID..
    public long createdTime { get; set; }
    public CloverOrderType? orderType { get; set; }
    public CloverLineItems? lineItems { get; set; }
    public CloverCustomers? customers { get; set; }
}

public class CloverOrderType
{
    public string? label { get; set; }
}

public class CloverLineItems
{
    public List<CloverLineItem> Elements { get; set; } = new();
}

public class CloverLineItem
{
    public string? name { get; set; }
    public string? note { get; set; } // any notes
    public int? unitQty { get; set; }
    public CloverModifications? modifications { get; set; }
}

public class CloverModifications
{
    public List<CloverModification> Elements { get; set; } = new();
}

public class CloverModification
{
    public string? name { get; set; }
}

public class CloverCustomers
{
    public List<CloverCustomer> Elements { get; set; } = new();
}

public class CloverCustomer
{
    public string? firstName { get; set; }
    public string? lastName { get; set; }
}
