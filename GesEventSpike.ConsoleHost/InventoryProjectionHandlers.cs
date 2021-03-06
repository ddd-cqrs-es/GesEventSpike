﻿using System;
using System.Collections.Generic;
using GesEventSpike.ConsoleHost.Scripts;
using GesEventSpike.EventStoreIntegration;
using Paramol;
using Paramol.SqlClient;

namespace GesEventSpike.ConsoleHost
{
    public static class InventoryProjectionHandlers
    {
        public static IEnumerable<object> Project(ItemPurchased purchased, Guid nextEventId, int checkpointPosition)
        {
            yield return TSql.NonQueryStatement(@"insert into ItemsPurchased (StockKeepingUnit) values (@StockKeepingUnit)", new
            {
                StockKeepingUnit = TSql.VarCharMax(purchased.StockKeepingUnit)
            });

            yield return TSql.NonQueryStatement(@"insert into StreamCheckpoint (Position) values (@Position)", new
            {
                Position = TSql.Int(checkpointPosition)
            });

            yield return new WriteToStream(nextEventId, "egress", new CheckpointEvent(checkpointPosition));
        }

        public static IEnumerable<SqlNonQueryCommand> CreateSchema()
        {
            yield return TSql.NonQueryStatement(ScriptContents.CreateSchema);
        }
    }
}