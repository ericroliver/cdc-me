### Snapshots

```
./bin/Debug/net9.0/cdc-cli snapshot list --database cdctest
./bin/Debug/net9.0/cdc-cli snapshot restore --database cdctest -n snap1
./bin/Debug/net9.0/cdc-cli snapshot create --database cdctest -n snap2
./bin/Debug/net9.0/cdc-cli snapshot delete --database cdctest -n snap2
```

### CDC

```
./bin/Debug/net9.0/cdc-cli cdc start -s "test-cli-session"
./bin/Debug/net9.0/cdc-cli cdc stop -s "test-cli-session" -c "test-cli-capture"
./bin/Debug/net9.0/cdc-cli cdc compare -b "test-cli-capture-baseline" -t "test-cli-capture-test-run-1"
```
