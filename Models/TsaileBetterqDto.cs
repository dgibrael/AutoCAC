namespace AutoCAC.Models
{
    public sealed class TsaileBetterqDto
    {
        public long Id { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int WaitTime { get; set; }

        public string Status { get; set; }
        public bool Waiting { get; set; }
        public bool NeedsCounseling { get; set; }
        public bool ControlledSubstance { get; set; }

        public DateTime? LockedDateTime { get; set; }
        public DateTime? LastModifiedDateTime { get; set; }
        public DateTime? CalledDateTime { get; set; }
        public DateOnly? Efdt { get; set; }
        public string PatientName { get; set; }
        public string ChartNumber { get; set; }

        // Keep enum helper if you need it
        public TsaileTicketStatus StatusEnum =>
            Enum.Parse<TsaileTicketStatus>(Status, ignoreCase: true);
    }

}
