-- queries on the cdc capture data
select * from "public"."cdc_capture_headers";
select * from "public"."cdc_captures";
select * from "public"."comparison_results";


--delete from "public"."cdc_capture_headers" where capture_header_id = '41c429e9-4ec0-4646-9c07-6b513ea3843a';