namespace GesEventSpike.ConsoleHost
{
    public class ItemPurchased
    {
        public readonly string StockKeepingUnit;

        public ItemPurchased(string stockKeepingUnit)
        {
            StockKeepingUnit = stockKeepingUnit;
        }
    }
}