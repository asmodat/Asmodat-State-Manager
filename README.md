# GETHManager
API Enabled GETH Manager Used For Automated Maintenance Of Geth Nodes



# CURL Test Commands:
Examples are using 'login' as default login and 'password' as default password.
Authorization: Basic bG9naW46cGFzc3dvcmQ=

Test Geth Node is Live:
curl --data '{"method":"eth_blockNumber","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST http://localhost:8545 | grep -oh "\w*0x\w*"


Kill Geth:
curl -H "Authorization: Basic bG9naW46cGFzc3dvcmQ=" http://localhost:8000/api/Processes/Kill?id=geth


List Processes:
curl -H "Authorization: Basic bG9naW46cGFzc3dvcmQ=" http://localhost:8000/api/Processes/List

Geth Proccess Info:
curl -H "Authorization: Basic bG9naW46cGFzc3dvcmQ=" http://localhost:8000/api/Processes/GETH

ReStart OS:
curl -H "Authorization: Basic bG9naW46cGFzc3dvcmQ=" http://localhost:8000/api/Processes/Restart

Execute command:
curl -H "Authorization: Basic bG9naW46cGFzc3dvcmQ=" http://localhost:8000/api/Processes/CMD?file=cmd&timeout=5&args=%2FC+echo+%22Hello+World%22


Output Log:
curl -H "Authorization: Basic bG9naW46cGFzc3dvcmQ=" http://localhost:8000/api/Geth/OutputLog?length=1000

Error Log:
curl -H "Authorization: Basic bG9naW46cGFzc3dvcmQ=" http://localhost:8000/api/Geth/ErrorLog?length=1000




HealthCheck:
curl http://localhost:8000/api/HealthCheck/GEth