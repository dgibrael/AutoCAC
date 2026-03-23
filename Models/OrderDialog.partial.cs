using AutoCAC.Extensions;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace AutoCAC.Models;

public partial class OrderDialog
{
    [NotMapped]
    public List<Dictionary<string, string>> ChildItems
    {
        get
        {
            var jsonStr = UseItemsList ? Items : Responses;
            return JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonStr)
                ?? new List<Dictionary<string, string>>();
        }
    }
    [NotMapped]
    public List<OrderDialogResponse> ResponsesList
    {
        get
        {
            return JsonSerializer.Deserialize<List<OrderDialogResponse>>(Responses) ?? new List<OrderDialogResponse>();
        }
    }
    [NotMapped]
    public List<OrderDialogItem> ItemsList
    {
        get
        {
            var lst = JsonSerializer.Deserialize<List<OrderDialogItem>>(Items);
            if (lst == null || lst.Count() <= 0) return new List<OrderDialogItem>();
            var maxRow = lst.Max(x => x.Row);
            var maxColumn = lst.Max(x => x.Column);

            var existingSequences = lst
                .Select(x => x.Sequence)
                .ToHashSet();

            for (var row = 1; row <= maxRow; row++)
            {
                for (var column = 1; column <= maxColumn; column++)
                {
                    var seq = $"{row}.{column}";
                    if (!existingSequences.Contains(seq))
                    {
                        lst.Add(new OrderDialogItem
                        {
                            Sequence = seq,
                            DisplayOnly = "EMPTY"
                        });
                    }
                }
            }
            return lst.OrderBy(x => x.Row).ThenBy(x => x.Column).ToList();
        }
    }
    public ILookup<int, OrderDialogItem> ItemsListByRow =>
        ItemsList.ToLookup(x => x.Row);
    public bool UseItemsList => Type is ("menu" or "order set" or "dialog");
    public int LastRow => ItemsList.Max(x => x.Row);
    public int LastColumn => ItemsList.Max(x => x.Column);
}

public class OrderDialogResponse
{
    public string ItemEntry { get; set; }
    public string Dialog { get; set; }
    public string Instance { get; set; }
    public string Value { get; set; }
    public string Text { get; set; }
    public string ValueResolved => string.IsNullOrWhiteSpace(Value) ? Text : Value;
    public string ResponseType => string.IsNullOrWhiteSpace(Dialog) ? "" : Dialog.RemovePrefix("OR GTX ", StringComparison.OrdinalIgnoreCase);
}

public class OrderDialogItem
{
    public string Sequence { get; set; }
    public string OrderDialogID { get; set; }
    public string Item { get; set; }
    public string Mnemonic { get; set; }
    public string DisplayText { get; set; }
    public string DisplayOnly { get; set; }
    public int Row
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Sequence))
                return 0;

            var parts = Sequence.Split('.');
            return parts.Length > 0 && int.TryParse(parts[0], out var row) ? row : 0;
        }
    }

    public int Column
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Sequence))
                return 0;

            var parts = Sequence.Split('.');
            return parts.Length > 1 && int.TryParse(parts[1], out var column) ? column : 1;
        }
    }

}


