if not exists (select * from sysobjects where name='ItemsPurchased' and xtype='U')
create table dbo.ItemsPurchased(StockKeepingUnit varchar(max) not null)

if not exists (select * from sysobjects where name='StreamCheckpoint' and xtype='U')
create table dbo.StreamCheckpoint(Position int not null)