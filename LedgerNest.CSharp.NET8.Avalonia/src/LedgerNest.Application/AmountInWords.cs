namespace LedgerNest.Application;

public static class AmountInWords
{
    private static readonly string[] Small = ["Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"];
    private static readonly string[] Tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    // The round-off setting displays a whole-unit amount; it does not change the ledger total.
    public static string Format(decimal value, bool indian = true)
    {
        var whole = decimal.Round(value, 0, MidpointRounding.AwayFromZero);
        string Words(decimal number)
        {
            if (number < 20) return Small[(int)number];
            if (number < 100) return Tens[(int)(number / 10)] + (number % 10 == 0 ? "" : " " + Words(number % 10));
            var groups = indian
                ? new[] { (10000000m, "Crore"), (100000m, "Lakh"), (1000m, "Thousand"), (100m, "Hundred") }
                : new[] { (1000000000m, "Billion"), (1000000m, "Million"), (1000m, "Thousand"), (100m, "Hundred") };
            foreach (var (scale, label) in groups)
                if (number >= scale) return Words(decimal.Floor(number / scale)) + " " + label + (number % scale == 0 ? "" : (scale == 100 ? " and " : " ") + Words(number % scale));
            return "";
        }
        return (whole < 0 ? "Minus " : "") + Words(decimal.Abs(whole)) + " Only";
    }
}
