geth --syncmode "fast" --rpc --rpcapi="db,eth,net,web3,personal,txpool" --cache 4096 --maxpeers 50 --verbosity 3 --rpcport 8545 --rpcaddr "127.0.0.1" --rpccorsdomain "*" --rpcvhosts "*"

read -p "Press enter to continue"