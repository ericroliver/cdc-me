
select count(*) from ArTransaction at2 

select * from ArTransaction at2 
select * from ArTransactionDetail 
select * from ChartOfAccounts 
select * from GlTransaction 
select * from GlTransactionDetail
select * from SalesOrder 
select * from SalesOrderDetail where SalesOrderId = 2

select * from SalesOrder where SalesOrderId = 1
-- testing script baseline
update SalesOrder set OrderStatus = 'Invoiced', CustomerEmail = 'expeditedorders@acme.com' where SalesOrderId =1
update SalesOrderDetail set ProductDescription = 'Widget Type B' where SalesOrderDetailId = 2

update SalesOrder set OrderStatus = 'Invoiced' where SalesOrderId =2
update SalesOrderDetail set ProductDescription = 'Annual Support' where SalesOrderDetailId = 6

-- simulate a difference
update SalesOrder set OrderStatus = 'Invoiced' where SalesOrderId =1
update SalesOrderDetail set ProductDescription = 'Widget Type B' where SalesOrderDetailId = 2

update SalesOrder set OrderStatus = 'Invoiced' where SalesOrderId =2
update SalesOrderDetail set ProductDescription = 'Annual Support - Lump Sum' where SalesOrderDetailId = 6

select * from [cdc].[dbo_SalesOrder_CT];

select * from sys.schemas
select * from sys.tables where schema_id = 5