using System.ComponentModel.DataAnnotations;

namespace TestScalpingBackend.Models
{

    public class ProfitOutDeals
    {
        public Guid Id { get; set; }
        public double ProfitOut { get; set; }
        public string TimeDifferenceInSeconds { get; set; }
        public ulong PositionID { get; set; }
        public ulong Login { get; set; }
        public string Symbol { get; set; }
        public DateTime OpeningTime { get; set; }
        public DateTime ClosingTime { get; set; }
        public ulong DealId { get; set; }
        public uint Entry { get; set; }
        public uint Action { get; set; }
        public ulong Volume { get; set; }
        public string Comment { get; set; }
    }

    public class RequestModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
        public int page { get; set; } = 1;
        [Range(1, 200, ErrorMessage = "PageSize must be at least 1.")]
        public int pageSize { get; set; } = 50;
        public string SortKey { get; set; } = "";
        public string SortDir { get; set; } = "";
        public string Search { get; set; } = "";
        public string SearchColumn { get; set; } = "";
    }

    public class ProfitRemovalInTimeRange : IValidatableObject
    {
        [Required(ErrorMessage = "Please specify a start time.")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "Please specify an end time.")]
        public DateTime EndTime { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndTime <= StartTime)
            {
                yield return new ValidationResult(
                    "End time must be greater than Start time.",
                    new[] { nameof(EndTime) });
            }

            // if ((EndTime - StartTime).TotalDays > 1)
            // {
            //     yield return new ValidationResult(
            //         "The difference between Start time and End time cannot exceed 1 day.",
            //         new[] { nameof(StartTime), nameof(EndTime) });
            // }
        }
    }

    public class DealDto
    {
        public ulong Login { get; set; }
        public uint Entry { get; set; }
        public double Profit { get; set; }
        public double Added { get; set; }
        public ulong DealId { get; set; }
        public ulong BalanceOperationDealId { get; set; }
        public uint Action { get; set; }
        public long TimeStamp { get; set; }
        public DateTimeOffset ConvertedTime { get; set; }
        public DateTimeOffset CurrentLocalTime { get; set; }
    }

    public class NewDealDto
    {
        public ulong DealId { get; set; }
        public double Profit { get; set; }
        public ulong Login { get; set; }
        public ulong PositionId { get; set; }
        public string Symbol { get; set; }
        public ulong EntryType { get; set; }
        public ulong ActionType { get; set; }
        public DateTime Time { get; set; }
    }
        
}
     
