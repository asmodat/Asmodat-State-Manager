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


