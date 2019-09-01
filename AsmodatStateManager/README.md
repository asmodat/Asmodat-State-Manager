# Asmodat State Manger
Allowing For Seemless S3 Versioned Backups of any Files

## Config Example

```
{
	"ManagerConfig": {
		"version": "0.0.1",
		"default-aws-role": "",
		"login": "login",
		"password": "password",
		"diskHealthChecks": { "c": 90, "d": 80 },
		"parallelism": 2,
		"targets": "D:\\sync_target_files_directory\\"
	}
}
```

## Sync Target Example

**Upload Example**
```
{
	"id": "0",
	"description": "out -> files",
	"type": "awsUpload",
	"source": "D:\\out\\source_directory",
	"destination": "bucket_name/files",
	"status": "bucket_name/status",
	"intensity": 5,
	"parallelism": 1,
	"retry": 5,
	"profile": "default",
	"verbose": 1,
	"recursive": true,
	"rotation": 10,
	"retention": 60,
	"timeout": 180000
}
```

**Download Example**

```
{	
	"id":"1",
	"type":"awsDownload",
	"description":"files -> in",
	"source":"bucket_name/files",
	"throwIfSourceNotFound":false,
	"destination":"D:\\in\\destination_directory",
	"role":null,
	"recursive":true,
	"wipe":true,
	"verify":true,
	"retry":5,
	"rotation":3,
	"retention":0,
	"status":"bucket_name/status",
	"intensity":5,
	"parallelism":1,
	"profile":"default",
	"verbose":1,
	"maxTimestamp":20991230121314,
	"minTimestamp":0,
	"timeout":60000,
	"maxSyncCount":1,
	"path":null
}
```

## API Call's examples

```
# healthcheckis
curl -s -w "\n%{http_code}" 'localhost:8080/api/resources/health' | { read body ; read code ; echo $code ; echo $body }
curl -s -w "\n%{http_code}" 'localhost:8080/api/sync/health' | { read body ; read code ; echo $code ; echo $body }

# status checks
curl -X GET -u login:password  {{ localhost:8080/api/resources/disks }} -O | jq '.'
curl -X GET -u login:password  {{ localhost:8080/api/sync/status }} -O | jq '.'
curl -X GET -u login:password {{ localhost:8080/api/sync/targets }} -O | jq '.'
curl -X GET -u login:password {{ localhost:8080/api/sync/targets?id=3 }} -O | jq '.'

# adding new sync jobs
curl -X PUT -u login:password -H "Content-Type: application/json" -d '{"id":"2","description":"files -> in","type":"awsDownload","source":"kira-backup/test/files","destination":"D:\\TMP\\in2","status":"kira-backup/test/status","intensity":5,"parallelism":1,"retry":5,"verbose":1,"profile":"default","maxTimestamp":20991230121314,"minTimestamp":0,"verify":true,"recursive":true,"wipe":true,"timeout":60000,"maxSyncCount":1}' {{ localhost:8080/api/sync/add }} -O | jq '.'
curl -X PUT -u login:password -H "Content-Type: application/json" -d '{"id":"1","description":"files -> in","type":"awsDownload","source":"kira-backup/test/files","destination":"D:\\TMP\\in1","status":"kira-backup/test/status","intensity":5,"parallelism":1,"retry":5,"verbose":1,"profile":"default","maxTimestamp":20991230121314,"minTimestamp":0,"verify":true,"recursive":true,"wipe":true,"timeout":60000,"maxSyncCount":1}' {{ localhost:8080/api/sync/add }} -O | jq '.'

# removing sync jobs
curl -X DELETE -u login:password localhost:8080/api/sync/delete?id=1 
curl -X DELETE -u login:password localhost:8080/api/sync/delete?id=2 
```


## Installation

> Runtime Installation (fast example, do not use in production)

```
DOTNET_SDK_VER="2.1.801" && DOTNET_RUNTIME_VER="2.1.8"

rm -f -r -v /usr/bin/dotnet && cd /usr/local/src && \
 wget https://dotnetcli.azureedge.net/dotnet/Sdk/$DOTNET_SDK_VER/dotnet-sdk-$DOTNET_SDK_VER-linux-x64.tar.gz && \
 mkdir -p /usr/bin/dotnet && tar zxf dotnet-sdk-$DOTNET_SDK_VER-linux-x64.tar.gz -C /usr/bin/dotnet && \
 chmod -R 777 /usr/bin/dotnet && dotnet --version

cd /usr/local/src && \
 wget https://dotnetcli.azureedge.net/dotnet/aspnetcore/Runtime/$DOTNET_RUNTIME_VER/aspnetcore-runtime-$DOTNET_RUNTIME_VER-linux-x64.tar.gz \
 && tar zxf aspnetcore-runtime-$DOTNET_RUNTIME_VER-linux-x64.tar.gz -C /usr/bin/dotnet \
 && chmod -R 777 /usr/bin/dotnet \
 && dotnet --version && dotnet --list-runtimes

echo "export DOTNET_ROOT=/usr/bin/dotnet" >> /etc/profile && \
 echo "export PATH=\$PATH:/usr/bin/dotnet" >> /etc/profile &&
 echo "source /etc/profile" >> $HOME/.bashrc && source /etc/profile
```

> Asmodat State Manger Installation

```
STATE_MGR_VER="0.0.1" && cd /usr/local/src && rm -f -v ./AsmodatStateManager-linux-x64.zip && \
 wget https://github.com/asmodat/Asmodat-State-Manager/releases/download/$STATE_MGR_VER/AsmodatStateManager-linux-x64.zip && \
 rm -rfv /usr/local/bin/AsmodatStateManager && unzip AsmodatStateManager-linux-x64.zip -d /usr/local/bin/AsmodatStateManager && \
 chmod -R 777 /usr/local/bin/AsmodatStateManager

 ln -s /usr/local/bin/AsmodatStateManager/AsmodatStateManager /usr/local/bin/asmanager

 mkdir $HOME/.asmanager && mkdir $HOME/.asmanager/sync
 ```

> Appending, manager config with `nano $HOME/.asmanager/config.json`

_Note: Make sure `echo $HOME` return /home/ubuntu_

```
{
	"ManagerConfig": {
		"version": "0.0.1",
		"default-aws-role": "",
		"login": "login",
		"password": "password",
		"diskHealthChecks": { "/": 90, "c": 90 },
		"parallelism": 2,
		"targets": "/home/ubuntu/.asmanager/sync"
	}
}
```

> Create new service by appending following script using `nano /lib/systemd/system/asmanager.service` command.

```
[Unit]
Description=Asmodat State Manager
After=network.target

[Service]
Type=simple
ExecStart=/usr/local/bin/asmanager 8080 "/home/ubuntu/.asmanager/config.json"
Restart=always
RestartSec=5

[Install]
WantedBy=default.target

```
> Enable service, start and check logs

```
systemctl enable asmanager && systemctl restart asmanager

journalctl --unit=asmanager -n 100 --no-pager
```
