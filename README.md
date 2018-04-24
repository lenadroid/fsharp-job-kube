# Instructions

This project illustrates how to use Kubernetes jobs to write data in Cassandra using F# as an example.

## Prerequisites

Have Cassandra Stateful Set running on your Kubernetes cluster.

## Get Cassandra Stateful Set running

You may use the following instructions to get Cassandra Stateful Set running before following next steps:

[Running Cassandra on Kubernetes on Azure](https://lenadroid.github.io/posts/stateful-sets-kubernetes-azure.html)

[Deploying Cassandra with Stateful Sets](https://kubernetes.io/docs/tutorials/stateful-application/cassandra/)

## Clone this project

```bash
git clone git@github.com:lenadroid/fsharp-job-kube.git
cd fsharp-job-kube
```

## Get project dependencies using [Paket](https://fsprojects.github.io/Paket/)

```bash
paket install
```

## Build and push a Docker image

```bash
docker build -tag <container registry>/fsharp-job .
docker push <container registry>/fsharp-job
```

## Put correct image address in the fsharp-job.yaml

Open `fsharp-job.yaml` and replace docker image address with yours: `<container registry>/fsharp-job`.

Feel free to also change memory and cpu settings for the jobs.

## Prepare jobs

```bash
jobCount=10
increment=1000
step=10000

jobDir=jobs
if [ -d "$jobDir" ]; then rm -Rf $jobDir; fi
mkdir $jobDir
for ((i=0; i <= $jobCount-1; i++))
do
    startIndex=$(($i * $step))
    echo "Creating a job for rows starting from $startIndex"
    cat fsharp-job.yaml | sed -e "s/\$START/$startIndex/" -e "s/\$STEP/$step/" -e "s/\$INCR/$increment/" > ./$jobDir/job-$startIndex.yaml
done
```

## Run jobs

```bash
kubectl create -f ./jobs
```

## Monitor status of jobs

```bash
kubectl get jobs

kubectl get pods

kubectl logs <pod name>

kubectl describe pod <pod name>
```

## Experimenting

Uncomment certain sections of code to experiment with Cassandra from your local machine. For example, using F# Interactive. Replace `external ip` value with the public IP address of your external cassandra service on Kubernetes.

## Creating keyspaces and tables

There's a commented section at the end of `the HousingData.fsx` file that shows how to create new keyspaces and tables in Cassandra, and also how to make queries. Can be used with [this](https://github.com/lenadroid/goto-cassandra-spark) example.