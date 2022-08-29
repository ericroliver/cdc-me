1. make sure there is no cdc on the db, disable it
2. restore the db
3. turn on cdc
4. run the application workflow profile
5. run profile capture
6. disable cdc on the db
------
repeat 2 - 6 with new workflow for step 4.

- teardown and destroy the container: 1.5 sec
- build new sql container: 1.5 min
- teardown container, restart, run app = 24sec/30sec 