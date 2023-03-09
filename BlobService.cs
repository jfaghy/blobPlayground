using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace BlobReader;

[ExcludeFromCodeCoverage]
public class BlobService
{
    private readonly string _connectionString = "DefaultEndpointsProtocol=https;AccountName=stdevpublish;AccountKey=USuFSCQ1kV/Oc/rMC5lf50q1leO+qn44tzQC1fxT2cuijDDaH9XZFtyUL/GwdZCwYvft2K/or7nU291p1lKn5Q==;EndpointSuffix=core.windows.net";
    private readonly string _container = "largedata";
    private readonly ConcurrentDictionary<string, BlobContainerClient> _blobContainerClients = new();
    private readonly JsonSampleHelper _jsonSampleHelper = new();

    private BlobContainerClient GetBlobContainerClient(string container)
    {
        return _blobContainerClients.GetOrAdd(container, x => new BlobContainerClient(_connectionString, container));
    }

    public async Task<MemoryStream> GetBlobAsStreamAsync(string container, string blobName)
    {
        Response<BlobDownloadInfo>? blob = await GetBlobContainerClient(container).GetBlobClient(blobName).DownloadAsync();
        MemoryStream ms = new();
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

        await using Stream stream = await blockBlobClient.OpenWriteAsync(true, new BlockBlobOpenWriteOptions { BufferSize = 50_000_000 });
        StreamWriter streamWriter = new(stream);
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
            sb.Append(_jsonSampleHelper.CreateLongJackJsonObject()+",");
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
        sb.Append(_jsonSampleHelper.OurJson);
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

    /// <summary>
    /// Deserialize stream from blob, only works with array value
    /// </summary>
    /// <param name="container"></param>
    /// <param name="key"></param>
    public async Task ReadFromBlobAsyncEnumerable(string container, string key)
    {
        BlobContainerClient blobContainerClient = GetBlobContainerClient(container);
        BlockBlobClient blobClient = blobContainerClient.GetBlockBlobClient(key);
        Stream blobStream = await blobClient.OpenReadAsync();
        
        //Console.WriteLine($"Writing file");
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

    /// <summary>
    /// doesn't work currently, unable to create queue
    /// </summary>
    public void ReadBlobStreamUsingUtf8AndAsyncEnumerable()
    {
        BlobContainerClient blobContainerClient = GetBlobContainerClient(_container);
        BlobClient blobClient = blobContainerClient.GetBlobClient("normaljson.json");
        Stream stream = blobClient.OpenRead();
        
        byte[] buffer = new byte[40];
        int readBytes = stream.Read(buffer);
        Console.WriteLine($"bytes read: [{readBytes}]");
        
        Utf8JsonReader reader = new(buffer, isFinalBlock: false, state: default);
        Console.WriteLine($"1 String in buffer is: {Encoding.UTF8.GetString(buffer)}");
        
        // Search for "Summary" property name
        while (reader.TokenType != JsonTokenType.PropertyName || !reader.ValueTextEquals("ShareClasses"))
        {
            if (!reader.Read())
            {
                // Not enough of the JSON is in the buffer to complete a read.
                GetMoreBytesFromStream(stream, buffer, ref reader);
            }
        }
        
        // Search for "Summary" property name
        while (reader.TokenType != JsonTokenType.StartArray)
        {
            if (!reader.Read())
            {
                // Not enough of the JSON is in the buffer to complete a read.
                GetMoreBytesFromStream(stream, buffer, ref reader);
            }
        }
        
        Console.WriteLine($"2 String in buffer is: {Encoding.UTF8.GetString(buffer)}");

        DeserializeAsyncEnumerable(stream).Wait();
    }

    /// <summary>
    /// DeserializeAsyncEnumerable from stream and write to console 
    /// </summary>
    /// <param name="stream"></param>
    public async Task DeserializeAsyncEnumerable(Stream stream)
    {
        IAsyncEnumerable<JsonNode?> enumerable = JsonSerializer.DeserializeAsyncEnumerable<JsonNode>(stream);

        await foreach (JsonNode? jsonNode in enumerable)
        {
            if (jsonNode is null)
            {
                Console.WriteLine("NULLLL!!!!");
                continue;
            }
            
            Console.WriteLine(jsonNode);
            Console.WriteLine("-------------------------------------------------------------");
        }
    }

    /// <summary>
    /// testing DeserializeAsyncEnumerable
    /// </summary>
    /// <param name="key"></param>
    public async Task DeserializeAsyncEnumerableTest()
    {
        MemoryStream memoryStream = new(Encoding.UTF8.GetBytes(_jsonSampleHelper.SingleLayerOurJson));
        IAsyncEnumerable<JsonNode?> enumerable = JsonSerializer.DeserializeAsyncEnumerable<JsonNode>(memoryStream);

        await foreach (JsonNode? jsonNode in enumerable)
        {
            if (jsonNode is null)
            {
                Console.WriteLine("NULLLL!!!!");
                continue;
            }
            
            Console.WriteLine(jsonNode);
            Console.WriteLine("-------------------------------------------------------------");
        }
    }

    /// <summary>
    /// UTF8 reader tests
    /// </summary>
    public void UTF8Reader()
    {
        byte[] bytes1 = Encoding.UTF8.GetBytes(_jsonSampleHelper.SampleJson);
        byte[] bytes2 = Encoding.UTF8.GetBytes(_jsonSampleHelper.SingleLayerOurJson);
        byte[] bytes3 = Encoding.UTF8.GetBytes(_jsonSampleHelper.OurJson);
        
        // could look at reading directly from stream
        
        Utf8JsonReader reader1 = new (bytes1);
        Utf8JsonReader reader2 = new (bytes2);
        Utf8JsonReader reader3 = new (bytes3);

        FilterUTF8(reader1, "name", "University");
        FilterUTF8(reader2, "val_nav_vscrcry", "340");
        FilterUTF8(reader3, "topic", "math");
    }

    /// <summary>
    /// Use UTF8 reader to search for token name and value, output matches found to console.
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="tokenName">the name of the token being searched for</param>
    /// <param name="valueToMatch">value being searched for</param>
    private static void FilterUTF8(Utf8JsonReader reader, string tokenName, string valueToMatch)
    {
        int count = 0;
        int total = 0;

        byte[] tokenBytes = Encoding.UTF8.GetBytes(tokenName);

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
                    if (reader.ValueTextEquals(tokenBytes))
                    {
                        Console.WriteLine($"PropertyName: {charsRead}");
                        // Assume valid JSON, known schema
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            if (reader.GetString()!.Contains(valueToMatch))
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
    
    /// <summary>
    ///  a bastardisation of using blocks
    /// </summary>
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

    /// <summary>
    /// expanding buffer example
    /// </summary>
    private static void GetMoreBytesFromStream(Stream stream, byte[] buffer, ref Utf8JsonReader reader)
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
