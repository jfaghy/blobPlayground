using System.Diagnostics;
using BlobReader;

string connectionString = "DefaultEndpointsProtocol=https;AccountName=stdevpublish;AccountKey=USuFSCQ1kV/Oc/rMC5lf50q1leO+qn44tzQC1fxT2cuijDDaH9XZFtyUL/GwdZCwYvft2K/or7nU291p1lKn5Q==;EndpointSuffix=core.windows.net";
BlobService blobService = new (connectionString);
/*bool jackBlobExists = await blobService.BlobExists("jack", "jack");
bool realBlobExists = await blobService.BlobExists("kneip", "Pai/6776d4d1-249a-e811-a965-000d3a2899a3/enabledSubFunds.json");
Console.WriteLine(jackBlobExists);
Console.WriteLine(realBlobExists);
bool newBlobExists = await blobService.BlobExists("largedata", "largedata.json");
Console.WriteLine(newBlobExists);*/

//FileStream fs = File.Create(@"c:\tmp\largedata.txt");
/*MemoryStream ms = new MemoryStream();

uint upperBound = 30_000_000;
//uint upperBound = 300;

StreamWriter streamWriter = new(ms);
streamWriter.AutoFlush = true;

for (int x = 0; x < upperBound; x++)
{
    if (x % 1_000_000 == 0)
    {
        Console.WriteLine(x);
    }

    streamWriter.Write("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890");
    //streamWriter.Flush();
    //Console.WriteLine(fs.Length);
}*/

//FileStream fileStream = File.OpenRead(@"c:\tmp\test.txt");
//await blobService.UploadToBlob("largedata", "largedata.json", fileStream);
//Console.WriteLine(fileStream.Length);
//fileStream.Close();
Stopwatch stopwatch = Stopwatch.StartNew();
string largeString = string.Concat(Enumerable.Repeat("jack", 100_000_000));
//string mediumString = string.Concat(Enumerable.Repeat("jack\n", 100));


//await blobService.WriteStreamToBlobAsync("largedata", "largedata.json", new MemoryStream("jack"u8.ToArray()));
//await blobService.UploadToStream("largedata", "largedata1.json", largeString);
//await blobService.UploadInBlocks("largedata", "largedata2.json", 10, largeString);
//await blobService.UploadMassiveJson("largedata", "largejson.json");
//await blobService.UploadJsonCollection("largedata", "normaljson.json");
//await blobService.UploadInBlocksInSeparateFiles("largedata", "largedata2.json", 10, largeString);
//await blobService.ReadFromBlob("largedata", "normaljson.json");
//blobService.UTF8Reader();


Console.WriteLine("Time taken:" + stopwatch.ElapsedMilliseconds + "ms");
