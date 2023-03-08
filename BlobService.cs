using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace BlobReader;

[ExcludeFromCodeCoverage]
public class BlobService
{
    private readonly string ConnectionString = "DefaultEndpointsProtocol=https;AccountName=stdevpublish;AccountKey=USuFSCQ1kV/Oc/rMC5lf50q1leO+qn44tzQC1fxT2cuijDDaH9XZFtyUL/GwdZCwYvft2K/or7nU291p1lKn5Q==;EndpointSuffix=core.windows.net";
    private readonly ConcurrentDictionary<string, BlobContainerClient> BlobContainerClients;
    
    private readonly string sampleJson = """
[
  {
    "web_pages": [ "https://contoso.edu/" ],
    "alpha_two_code": "US",
    "state-province": null,
    "country": "United States",
    "domains": [ "contoso.edu" ],
    "name": "Contoso Community College"
  },
  {
    "web_pages": [ "http://fabrikam.edu/" ],
    "alpha_two_code": "US",
    "state-province": null,
    "country": "United States",
    "domains": [ "fabrikam.edu" ],
    "name": "Fabrikam Community College"
  },
  {
    "web_pages": [ "http://www.contosouniversity.edu/" ],
    "alpha_two_code": "US",
    "state-province": null,
    "country": "United States",
    "domains": [ "contosouniversity.edu" ],
    "name": "Contoso University"
  },
  {
    "web_pages": [ "http://www.fabrikamuniversity.edu/" ],
    "alpha_two_code": "US",
    "state-province": null,
    "country": "United States",
    "domains": [ "fabrikamuniversity.edu" ],
    "name": "Fabrikam University"
  }
]
""";
    
    private readonly string singleLayerOurJson = """
        {
            "ShareClasses": [
            {
                "val_valuation_date": "2021-12-10",
                "ancdat": "2021-12-10",
                "pospft": "",
                "val_tnascr_vscrcry": "",
                "val_tnsscr": "",
                "intamn_vpubcry": "",
                "val_nav_vscrcry": "340",
                "immobiamn_vpubcry": "",
                "val_tnasfu_vsfucry": "",
                "val_rdmpri_vpubcry": "",
                "val_sbspri_vpubcry": ""
            },
            {
                "val_valuation_date": "2021-12-10",
                "ancdat": "2021-12-10",
                "pospft": "",
                "val_tnascr_vscrcry": "",
                "val_tnsscr": "",
                "intamn_vpubcry": "",
                "val_nav_vscrcry": "270",
                "immobiamn_vpubcry": "",
                "val_tnasfu_vsfucry": "",
                "val_rdmpri_vpubcry": "",
                "val_sbspri_vpubcry": ""
            }]
        }
        """;
        
    private readonly string ourJson = """
        {
            "ShareClasses": [
            {
                    "Nav": {
                        "val_valuation_date": "2021-12-10",
                        "ancdat": "2021-12-10",
                        "pospft": "",
                        "val_tnascr_vscrcry": "",
                        "val_tnsscr": "",
                        "intamn_vpubcry": "",
                        "val_nav_vscrcry": "340",
                        "immobiamn_vpubcry": "",
                        "val_tnasfu_vsfucry": "",
                        "val_rdmpri_vpubcry": "",
                        "val_sbspri_vpubcry": "",
                        "topic": "math"
                    },
                    "topic": {
                        "topic": "math",
                        "pubcry": "EUR"
                    },
                    "ShareClass": {
                        "cod_isin": "KYG0004A1067"
                    },
                    "_index": "0" },
            {
                    "Nav": {
                        "val_valuation_date": "2021-12-10",
                        "ancdat": "2021-12-10",
                        "pospft": "",
                        "val_tnascr_vscrcry": "",
                        "val_tnsscr": "",
                        "intamn_vpubcry": "",
                        "val_nav_vscrcry": "340",
                        "immobiamn_vpubcry": "",
                        "val_tnasfu_vsfucry": "",
                        "val_rdmpri_vpubcry": "",
                        "val_sbspri_vpubcry": ""
                    },
                    "Topic": {
                        "pubcry": "USD"
                    },
                    "ShareClass": {
                        "cod_isin": "KYG0004A1067"
                    },
                    "_index": "0" }]
        }
        """;

    public BlobService(string connectionString)
    {
        ConnectionString = connectionString;
        BlobContainerClients = new ConcurrentDictionary<string, BlobContainerClient>();
    }

    private BlobContainerClient GetBlobContainerClient(string container)
    {
        return BlobContainerClients.GetOrAdd(container, x => new BlobContainerClient(ConnectionString, container));
    }

    public async Task<MemoryStream> GetBlobAsStreamAsync(string container, string blobName)
    {
        var blob = await GetBlobContainerClient(container).GetBlobClient(blobName).DownloadAsync();
        MemoryStream ms = new MemoryStream();
        await blob.Value.Content.CopyToAsync(ms);
        ms.Position = 0;
        return ms;
    }

    public async Task WriteStreamToBlobAsync(string container, string blobName, MemoryStream content)
    {
        content.Position = 0;
        await GetBlobContainerClient(container).GetBlobClient(blobName).UploadAsync(content, true);
    }

    public async Task UploadToBlob(string container, string key, FileStream content)
    {
        BlobContainerClient blobContainerClient = GetBlobContainerClient(container);
        BlobClient blobClient = blobContainerClient.GetBlobClient(key);
        await blobClient.UploadAsync(content, true);
    }

    public async Task UploadToStream(string container, string key, string data)
    {
        BlobContainerClient blobContainerClient = GetBlobContainerClient(container);

        BlockBlobClient blockBlobClient = blobContainerClient.GetBlockBlobClient(key);

        using (Stream stream = await blockBlobClient.OpenWriteAsync(true, new BlockBlobOpenWriteOptions
               {
                   BufferSize = 50_000_000
               }))
        {
            StreamWriter streamWriter = new StreamWriter(stream);
            streamWriter.AutoFlush = true;

            for (int x = 0; x < 10; x++)
            {
                if (x % 1 == 0)
                {
                    Console.WriteLine(x);
                }

                await streamWriter.WriteAsync(data);
            }
        }
    }

    public async Task UploadMassiveJson(string container, string key)
    {
        BlobContainerClient blobContainerClient = GetBlobContainerClient(container);
        BlockBlobClient blockBlobClient = blobContainerClient.GetBlockBlobClient(key);

        await using Stream stream = await blockBlobClient.OpenWriteAsync(true, new BlockBlobOpenWriteOptions
        {
            BufferSize = 50_000_000
        });

        StreamWriter streamWriter = new(stream);
        streamWriter.AutoFlush = true;

        StringBuilder sb = new();
        for (int i = 0; i < 7000; i++)
        {
            sb.Append(CreateJson()+",");
        }

        string data = sb.ToString();
        await streamWriter.WriteAsync("[");

        for (int x = 0; x < 9; x++)
        {
            if (x % 1 == 0)
            {
                Console.WriteLine(x);
            }

            if (x == 9)
            {
                sb.Length--;
            }

            await streamWriter.WriteAsync(data);
        }
        await streamWriter.WriteAsync("]");
    }


    public async Task UploadJsonCollection(string container, string key)
    {
        BlobContainerClient blobContainerClient = GetBlobContainerClient(container);
        BlockBlobClient blockBlobClient = blobContainerClient.GetBlockBlobClient(key);

        await using Stream stream = await blockBlobClient.OpenWriteAsync(true, new BlockBlobOpenWriteOptions
        {
            BufferSize = 50_000_000
        });

        StreamWriter streamWriter = new(stream);
        streamWriter.AutoFlush = true;

        StringBuilder sb = new();
        sb.Append(ourJson);
        sb.Append(',');

        string data = sb.ToString();
        await streamWriter.WriteAsync("[");

        for (int x = 0; x < 9; x++)
        {
            if (x % 1 == 0)
            {
                Console.WriteLine(x);
            }

            if (x == 8)
            {
                data = data.Remove(data.Length - 1);
            }

            await streamWriter.WriteAsync(data);
        }
        await streamWriter.WriteAsync("]");
    }

    public async Task UploadInBlocks(string container, string key, int blockCount, string data)
    {
        BlobContainerClient blobContainerClient = GetBlobContainerClient(container);
        BlockBlobClient blobClient = blobContainerClient.GetBlockBlobClient(key);

        byte[] bytes = Encoding.ASCII.GetBytes(data);
        List<string> blockIdList = new();

        for (int i = 0; i < blockCount; i++)
        {
            Console.WriteLine($"I am working on block {i} are you happy now Thomas");
            using MemoryStream stream = new(bytes);
            //has to be base64 string as query param demands it
            string blockId = Convert.ToBase64String
                (Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));

            blockIdList.Add(blockId);

            await blobClient.StageBlockAsync(blockId, stream);
        }

        Console.WriteLine($"Commiting rn, on god, fr fr");
        await blobClient.CommitBlockListAsync(blockIdList);
    }

    public async Task ReadFromBlob(string container, string key)
    {
        BlobContainerClient blobContainerClient = GetBlobContainerClient(container);
        BlockBlobClient blobClient = blobContainerClient.GetBlockBlobClient(key);
        Console.WriteLine($"Writing file");
        Stream blobStream = await blobClient.OpenReadAsync();
        //FileStream fileStream = File.OpenWrite(@"c:\tmp\new.txt");
        //await blobStream.CopyToAsync(fileStream);
        Console.WriteLine($"{blobStream.Length:###,###}");
        IAsyncEnumerable<JsonNode> enumerable = JsonSerializer.DeserializeAsyncEnumerable<JsonNode>(blobStream);
        await foreach (JsonNode x in enumerable)
        {
            Console.WriteLine(x);
            Console.WriteLine("-------------------------------------------------------------");
        }
    }

    public void UTF8Reader()
    {
        byte[] s_nameUtf8 = "name"u8.ToArray();
        byte[] s_topicUtf8 = "topic"u8.ToArray();
        byte[] s_navUtf8 = "val_nav_vscrcry"u8.ToArray();
        byte[] bytes1 = Encoding.UTF8.GetBytes(sampleJson);
        byte[] bytes2 = Encoding.UTF8.GetBytes(singleLayerOurJson);
        byte[] bytes3 = Encoding.UTF8.GetBytes(ourJson);
        //var stream = new MemoryStream(bytes);

        //byte[] buffer = new byte[4096];
        //stream.Read(buffer);

        /*
        JsonReaderOptions options = new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };
        */

        Utf8JsonReader reader1 = new (bytes1);
        Utf8JsonReader reader2 = new (bytes2);
        Utf8JsonReader reader3 = new (bytes3);

        FilterUTF8(reader1, s_nameUtf8, "University");
        FilterUTF8(reader2, s_navUtf8, "340");
        FilterUTF8(reader3, s_topicUtf8, "math");
    }

    private static void FilterUTF8(Utf8JsonReader reader, byte[] searchItem, string value)
    {
        int count = 0;
        int total = 0;

        while (reader.Read())
        {
            JsonTokenType tokenType = reader.TokenType;
            string charsRead = Encoding.UTF8.GetString(reader.ValueSpan);

            switch (tokenType)
            {
                case JsonTokenType.StartObject:
                    total++;
                    Console.WriteLine($"StartObject: {charsRead}");
                    break;
                case JsonTokenType.PropertyName:
                    if (reader.ValueTextEquals(searchItem))
                    {
                        Console.WriteLine($"PropertyName: {charsRead}");
                        // Assume valid JSON, known schema
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            if (reader.GetString()!.Contains(value))
                            {
                                count++;
                                charsRead = Encoding.UTF8.GetString(reader.ValueSpan);
                                Console.WriteLine($"String: {charsRead}");
                            }   
                        }
                        else if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            total++;
                            charsRead = Encoding.UTF8.GetString(reader.ValueSpan);
                            Console.WriteLine($"StartObject: {charsRead}");
                        }
                    }

                    break;
            }
        }

        Console.WriteLine($"{count} out of {total} matches");
    }
    
    public async Task UploadInBlocksInSeparateFiles(string container, string key, int blockCount, string data)
    {
        BlobContainerClient blobContainerClient = GetBlobContainerClient(container);

        byte[] bytes = Encoding.ASCII.GetBytes(data);

        for (int i = 0; i < blockCount; i++)
        {
            List<string> blockIdList = new();
            BlockBlobClient blobClient = blobContainerClient.GetBlockBlobClient(i + key);
            Console.WriteLine($"I am working on block {i} are you happy now Thomas");
            using MemoryStream stream = new(bytes);
            //has to be base64 string as query param demands it
            string blockId = Convert.ToBase64String
                (Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));

            blockIdList.Add(blockId);

            await blobClient.StageBlockAsync(blockId, stream);

            Console.WriteLine($"Commiting rn, on god, fr fr");
            await blobClient.CommitBlockListAsync(blockIdList);

        }

    }

    public async Task<bool> BlobExists(string container, string key)
    {
        BlobContainerClient blobContainerClient = GetBlobContainerClient(container);
        BlobClient blobClient = blobContainerClient.GetBlobClient(key);
        return await blobClient.ExistsAsync();
    }

    public string CreateJson()
    {
        return """{"name": "jackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjackjack"}""";
    }

    private static void GetMoreBytesFromStream(MemoryStream stream, byte[] buffer, ref Utf8JsonReader reader)
    {
        int bytesRead;
        if (reader.BytesConsumed < buffer.Length)
        {
            ReadOnlySpan<byte> leftover = buffer.AsSpan((int)reader.BytesConsumed);

            if (leftover.Length == buffer.Length)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
                Console.WriteLine($"Increased buffer size to {buffer.Length}");
            }

            leftover.CopyTo(buffer);
            bytesRead = stream.Read(buffer.AsSpan(leftover.Length));
        }
        else
        {
            bytesRead = stream.Read(buffer);
        }
        Console.WriteLine($"String in buffer is: {Encoding.UTF8.GetString(buffer)}");
        reader = new Utf8JsonReader(buffer, isFinalBlock: bytesRead == 0, reader.CurrentState);
    }
}
