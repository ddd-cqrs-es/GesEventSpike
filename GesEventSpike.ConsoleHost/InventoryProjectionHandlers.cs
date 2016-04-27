using System;
using System.Collections.Generic;
using GesEventSpike.ConsoleHost.Scripts;
using Paramol;
using Paramol.SqlClient;

namespace GesEventSpike.ConsoleHost
{
    public static class InventoryProjectionHandlers
    {
        public static SqlNonQueryCommand OnItemPurchased(ItemPurchased purchased, Guid nextEventId)
        {
            return TSql.NonQueryStatement(@"insert into ItemsPurchased (StockKeepingUnit) values (@StockKeepingUnit)", new
            {
                StockKeepingUnit = TSql.VarCharMax(purchased.StockKeepingUnit)
            });
        }

        public static SqlNonQueryCommand OnCheckpoint(int checkpointPosition)
        {
            return TSql.NonQueryStatement(@"insert into StreamCheckpoint (Position) values (@Position)", new
            {
                Position = TSql.Int(checkpointPosition)
            });
        }

        public static SqlNonQueryCommand CreateSchema()
        {
            return TSql.NonQueryStatement(ScriptContents.CreateSchema);
        }
    }
}