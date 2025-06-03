using Microsoft.EntityFrameworkCore;
using AutoCAC.Models;

namespace AutoCAC.Models
{
    public partial class mainContext
    {
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<ResponseItem>();
            modelBuilder.Ignore<ItemItem>();
        }
    }
}
