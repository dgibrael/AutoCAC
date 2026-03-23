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
    public bool UseItemsList => Type is ("menu" or "order set" or "dialog");
    public byte Columns => ColumnWidth switch
    {
        <= 26 => 4,
        <= 39 => 3,
        <= 79 => 2,
        _ => 1 
    };
}
