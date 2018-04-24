#!/bin/bash

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
