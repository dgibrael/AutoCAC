namespace AutoCAC.Models;

public class WardstockDto
{
    public long Id { get; set; }
    public DateTime? OrderDateTime { get; set; }
    public DateTime? LastModifiedDateTime { get; set; }
    public string LocationName { get; set; }
    public string Status { get; set; }
    public int LocationId { get; set; }
}
