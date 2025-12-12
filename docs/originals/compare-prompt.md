The cdc capture code seems to be working now. Here is my test script:

```sql
update SalesOrder set OrderStatus = 'Invoiced', CustomerEmail = 'expeditedorders@acme.com' where SalesOrderId =1
update SalesOrderDetail set ProductDescription = 'Widget Type B' where SalesOrderDetailId = 2

update SalesOrder set OrderStatus = 'Invoiced' where SalesOrderId =2
update SalesOrderDetail set ProductDescription = 'Annual Support' where SalesOrderDetailId = 6
```

this produced a capture with two records. One record per table that had changes:

this is the value of the capture_data field for the SalesOrder table:

```json
[
  {
    "__$table": "dbo.SalesOrder",
    "__$operation": 4,
    "__$start_lsn": "AAAIRQAABHgAAg==",
    "__$primary_key": 1,
    "new_OrderStatus": "Invoiced",
    "old_OrderStatus": "Open",
    "new_CustomerEmail": "expeditedorders@acme.com",
    "old_CustomerEmail": "orders@acme.com"
  },
  {
    "__$table": "dbo.SalesOrder",
    "__$operation": 4,
    "__$start_lsn": "AAAIRQAABKgABA==",
    "__$primary_key": 2,
    "new_OrderStatus": "Invoiced",
    "old_OrderStatus": "Open"
  }
]
```

and this is the capture_data field from the SalesOrderDetail table:

```json
[
  {
    "__$table": "dbo.SalesOrderDetail",
    "__$operation": 4,
    "__$start_lsn": "AAAIRQAABJAABw==",
    "__$primary_key": 2,
    "new_ProductDescription": "Widget Type B",
    "old_ProductDescription": "Standard Widget Type B"
  },
  {
    "__$table": "dbo.SalesOrderDetail",
    "__$operation": 4,
    "__$start_lsn": "AAAIRQAABLAABw==",
    "__$primary_key": 6,
    "new_ProductDescription": "Annual Support",
    "old_ProductDescription": "Annual Support Package"
  }
]
```

Now we need a compare/validation function to compare the capture data for all tables across two captures and return a model/json
that is a array of comparison fails.. the first capture_name will be the 'expected'
or baseline and the second capture_name will be the capture we are pass or failing.
