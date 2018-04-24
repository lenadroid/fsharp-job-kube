#r "packages/CassandraCSharpDriver/lib/net40/Cassandra.dll"
#r "packages/Mono.Posix/lib/net40/Mono.Posix.dll"
#r "packages/FSharp.Data/lib/net45/FSharp.Data.dll"
#r "packages/FSharp.Collections.ParallelSeq/lib/net40/FSharp.Collections.ParallelSeq.dll"

open Cassandra
open System
open FSharp.Data
open FSharp.Collections.ParallelSeq

let startIndex = fsi.CommandLineArgs |> Seq.tail |> Seq.head |> int
let writesPerJob = fsi.CommandLineArgs |> Seq.tail |> Seq.item 1 |> int
let increment = fsi.CommandLineArgs |> Seq.tail |> Seq.item 2 |> int

let startIndex = 0
let writesPerJob = 10000
let increment = 1000


printfn "Start index: %A" startIndex
printfn "Items to add: %A" writesPerJob
printfn "Increment: %A" increment

type Addresses = CsvProvider<"data/address-format.csv",
                             HasHeaders = true,
                             Schema = "LON (decimal), LAT (decimal), NUMBER (string), STREET (string),,,,, POSTCODE (int option),, HASH (string)">

type Address = {
    Lon: decimal
    Lat: decimal
    Number: string
    Street: string
    Postcode: int option
    Hash: string
}

let getAddresses () = Addresses.Load("data/seattle.csv")
let seattleAddresses = getAddresses ()

let connectToCassandraCluster (endpointAddress: string) =
    let queryOptions = QueryOptions().SetConsistencyLevel(ConsistencyLevel.LocalOne).SetPageSize(1000)
    let cluster =
        Cassandra.Cluster.Builder()
            .AddContactPoint(endpointAddress)
            .WithQueryOptions(queryOptions)
            .Build()
    let session = cluster.Connect("system")
    session

let createHosuingKeyspace (session: ISession) =
    session.Execute(@"
        create keyspace if not exists housingdata
        with replication = {'class': 'SimpleStrategy', 'replication_factor': 3};"
        ) |> ignore
    session.Execute(@"use housingdata;")

let createAddressesTable (session: ISession) =
    // LON, LAT, NUMBER, STREET, POSTCODE, HASH
    session.Execute(@"
    CREATE TABLE IF NOT EXISTS addresses (
        lon decimal,
        lat decimal,
        number text,
        street text,
        postcode int,
        hash text,
        PRIMARY KEY (postcode, street, number)) WITH CLUSTERING ORDER BY (street ASC)
    ")

let insertAddress (address:Address) (session: ISession) =
    let query = session.Prepare("insert into addresses "
                + "(lon,lat,number,street,postcode,hash) values ("
                + "?, ?, ?, ?, ?, ?);")
    let postcode =
        match address.Postcode with
        | Some p -> p
        | None -> 0

    query.Bind(address.Lon, address.Lat, address.Number, address.Street, postcode, address.Hash)
    |> session.Execute

let displayAddressRow(row: Row) =
    Console.WriteLine(
        "LON: {0},\tLAT: {1}, \t House Number: {2},\tStreet: {3},\tPostcode: {4},\tHash: {5}",
            row.GetValue<decimal>("lon"),
            row.GetValue<decimal>("lat"),
            row.GetValue<string>("number"),
            row.GetValue<string>("street"),
            row.GetValue<int>("postcode"),
            row.GetValue<string>("hash"))

let getAllAddressRows (session: ISession) =
    session.Execute("select * from addresses;")

let getAddressesByPostCode (session: ISession) =
    session.Execute("select * from addresses where postcode=98118;")

let deleteHousingKeyspace (session: ISession) =
    session.Execute(@"drop keyspace housingdata;") |> ignore

let persist address i session =
    insertAddress address session

let persistAddresses session skip take =
    seattleAddresses.Rows
    |> Seq.skip skip
    |> Seq.take take
    |> Seq.fold (fun addressCount a ->
            let goodAddress = {
                Lon = a.LON
                Lat = a.LAT
                Number = a.NUMBER
                Street = a.STREET
                Postcode = a.POSTCODE
                Hash = a.HASH
            }
            let result = persist goodAddress (addressCount+1) session
            printfn "%A addresses persisted: skip %A, take %A ..." (addressCount+1+skip) skip take
            addressCount+1
        ) 0

let persistAddressesParallel (session: ISession) start count increment =
    let overallCount = seattleAddresses.Rows |> Seq.length
    let howManyToAdd =
        if overallCount - start < count
        then overallCount - start
        else count
    let step = increment
    let executions = howManyToAdd / step
    let getSkipAndTake index =
        match index with
        | e when (e = executions) -> start + index * step, howManyToAdd % step
        | _ -> start + index * step, step
    let till = if howManyToAdd % step = 0 then executions - 1 else executions

    [0..till]
        |> PSeq.map getSkipAndTake
        |> PSeq.map (fun (skip,take) ->
                let resultCount = persistAddresses session skip take
                printfn "Persisted portion %A" (skip/step)
                resultCount
            )
        |> PSeq.sum

// UNCOMMENT TO TEST THIS!

// let session = connectToCassandraCluster "external ip"
// createHosuingKeyspace session
// createAddressesTable session
// persistAddressesParallel session 0 100 10


// USE THIS WHEN IT'S RUN WITHIN KUBERNETES

let session = connectToCassandraCluster "cassandra"
createHosuingKeyspace session
createAddressesTable session
persistAddressesParallel session startIndex writesPerJob increment

// SEE "INSTRUCTIONS" TO CREATE KUBERNETES JOBS


// TV SHOW RECOMMENDATIONS for https://github.com/lenadroid/goto-cassandra-spark

// session.Execute(@"
// CREATE KEYSPACE IF NOT EXISTS showrecommender
// WITH REPLICATION = {'class': 'SimpleStrategy', 'replication_factor': 3};"
// ) |> ignore
// session.Execute(@"USE showrecommender;")

// session.Execute(@"
// CREATE TABLE IF NOT EXISTS recommendations (
//     keyword text,
//     rating int,
//     episode text,
//     PRIMARY KEY (keyword, rating)) WITH CLUSTERING ORDER BY (rating ASC)
// ")

// EXPLORATORY QUERIES

// let displayRecommendationRow(row: Row) =
//     Console.WriteLine("{1} -> {0}",
//         row.GetValue<string>("episode"),
//         row.GetValue<int>("rating"))

// let getRecommendationsByKeyword (session: ISession) =
//     session.Execute("SELECT * FROM recommendations WHERE keyword = 'tyrion' ORDER BY rating ASC;")

// let entry = getRecommendationsByKeyword session
// entry.GetRows() |> Seq.iter displayRecommendationRow